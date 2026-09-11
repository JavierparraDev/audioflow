using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using AudioFlow.Core;
using AudioFlow.Core.Logging;
using AudioFlow.Models;
using AudioFlow.Rules;
using AudioFlow.UI.ViewModels;

namespace AudioFlow.UI.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AudioDeviceManager _deviceManager = new();
    private readonly RuleEngine _ruleEngine = new();
    private readonly AudioSessionManager _sessionManager = new();
    private readonly AudioRoutingManager _routingManager = new();
    private readonly AudioSessionMonitor _monitor;
    private readonly Dispatcher _dispatcher;

    private bool _suppressSideEffects;
    private bool _initialized;

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _monitor = new AudioSessionMonitor(_deviceManager);

        AddToSpeakersCommand = new RelayCommand(p => AddToSpeakers(p as DetectedAppItem));
        RemoveFromSpeakersCommand = new RelayCommand(p => RemoveFromSpeakers(p as SpeakerAppItem));
        StartStopCommand = new RelayCommand(_ => ToggleRouting());
        ApplyNowCommand = new RelayCommand(_ => ApplyAll(manual: true));
        RefreshCommand = new RelayCommand(_ => RefreshDevicesAndSessions());
    }

    public ObservableCollection<DeviceOption> OutputDevices { get; } = new();
    public ObservableCollection<SpeakerAppItem> SpeakerApps { get; } = new();
    public ObservableCollection<DetectedAppItem> DetectedApps { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();

    public ICommand AddToSpeakersCommand { get; }
    public ICommand RemoveFromSpeakersCommand { get; }
    public ICommand StartStopCommand { get; }
    public ICommand ApplyNowCommand { get; }
    public ICommand RefreshCommand { get; }

    private DeviceOption? _speakersDevice;
    public DeviceOption? SpeakersDevice
    {
        get => _speakersDevice;
        set
        {
            if (!SetProperty(ref _speakersDevice, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SpeakersDeviceName));
            if (!_suppressSideEffects)
            {
                PersistSelection();
                RefreshSessions();
            }
        }
    }

    private DeviceOption? _headphonesDevice;
    public DeviceOption? HeadphonesDevice
    {
        get => _headphonesDevice;
        set
        {
            if (!SetProperty(ref _headphonesDevice, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HeadphonesDeviceName));
            if (!_suppressSideEffects)
            {
                PersistSelection();
            }
        }
    }

    private bool _audioLockEnabled;
    public bool AudioLockEnabled
    {
        get => _audioLockEnabled;
        set
        {
            if (!SetProperty(ref _audioLockEnabled, value))
            {
                return;
            }

            if (_suppressSideEffects)
            {
                return;
            }

            if (value)
            {
                UpdateAudioLock();
            }
            else
            {
                _ruleEngine.DisableAudioLock();
            }

            AddLog(value ? "Audio Lock activado." : "Audio Lock desactivado.");
        }
    }

    private bool _routingEnabled;
    public bool RoutingEnabled
    {
        get => _routingEnabled;
        private set
        {
            if (SetProperty(ref _routingEnabled, value))
            {
                OnPropertyChanged(nameof(StartStopText));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StartStopText => RoutingEnabled ? "Detener" : "Iniciar";
    public string StatusText => RoutingEnabled ? "AudioFlow en ejecución" : "AudioFlow detenido";
    public string SpeakersDeviceName => SpeakersDevice?.Name ?? "Sin seleccionar";
    public string HeadphonesDeviceName => HeadphonesDevice?.Name ?? "Sin seleccionar";

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioFlow", "logs");
            Log.UseFile(Path.Combine(logDir, "audioflow.log"));
        }
        catch
        {
            // Logging to file is best effort.
        }

        Log.LineWritten += OnLogLineWritten;
        Log.Info("AudioFlow started");

        RefreshDevices();
        LoadRules();

        _monitor.SessionStarted += OnSessionStarted;
        _monitor.SessionEnded += OnSessionEnded;
        _monitor.SessionsChanged += OnSessionsChanged;
        _monitor.Start();

        RefreshSessions();
    }

    private void OnLogLineWritten(LogLevel level, string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            LogLines.Insert(0, message);
            while (LogLines.Count > 300)
            {
                LogLines.RemoveAt(LogLines.Count - 1);
            }
        });
    }

    private void RefreshDevices()
    {
        var devices = _deviceManager.GetOutputDevices(includeInactive: false);

        var previousSpeakersId = _speakersDevice?.Id;
        var previousHeadphonesId = _headphonesDevice?.Id;

        _suppressSideEffects = true;
        try
        {
            OutputDevices.Clear();
            foreach (var device in devices)
            {
                OutputDevices.Add(DeviceOption.From(device));
            }

            var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();

            // R1: keep the user's manual choice when the device is still present.
            SpeakersDevice = ChooseDevice(previousSpeakersId, devices, preferSpeakers: true, defaultDevice, excludeId: null);
            HeadphonesDevice = ChooseDevice(previousHeadphonesId, devices, preferSpeakers: false, defaultDevice, excludeId: SpeakersDevice?.Id);
        }
        finally
        {
            _suppressSideEffects = false;
        }
    }

    private DeviceOption? ChooseDevice(
        string? previousId,
        IReadOnlyList<AudioDevice> devices,
        bool preferSpeakers,
        AudioDevice? defaultDevice,
        string? excludeId)
    {
        if (!string.IsNullOrWhiteSpace(previousId))
        {
            var kept = FindOption(previousId);
            if (kept is not null)
            {
                return kept;
            }
        }

        var candidate = preferSpeakers
            ? devices.FirstOrDefault(d => ContainsAny(d.FriendlyName, "speaker", "parlante", "altavoz")) ?? defaultDevice
            : devices.FirstOrDefault(d => ContainsAny(d.FriendlyName, "headphone", "audífono", "audifono", "headset"))
              ?? devices.FirstOrDefault(d => !string.Equals(d.Id, excludeId, StringComparison.OrdinalIgnoreCase));

        return candidate is null ? null : FindOption(candidate.Id);
    }

    private DeviceOption? FindOption(string id) =>
        OutputDevices.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    private void LoadRules()
    {
        _suppressSideEffects = true;
        try
        {
            if (string.IsNullOrWhiteSpace(_ruleEngine.Rules.DefaultOutputDeviceId) && HeadphonesDevice is not null)
            {
                _ruleEngine.SetDefaultDevice(HeadphonesDevice.Id);
            }

            SpeakerApps.Clear();
            foreach (var rule in _ruleEngine.Rules.Rules)
            {
                SpeakerApps.Add(new SpeakerAppItem
                {
                    Key = rule.ApplicationIdentifier,
                    Name = rule.ApplicationName,
                    PathHash = rule.PathHash
                });
            }

            _audioLockEnabled = _ruleEngine.Rules.AudioLockEnabled;
            OnPropertyChanged(nameof(AudioLockEnabled));
        }
        finally
        {
            _suppressSideEffects = false;
        }
    }

    private void PersistSelection()
    {
        if (HeadphonesDevice is not null)
        {
            _ruleEngine.SetDefaultDevice(HeadphonesDevice.Id);
        }

        if (SpeakersDevice is not null)
        {
            foreach (var app in SpeakerApps)
            {
                _ruleEngine.SetRule(app.Key, app.Name, SpeakersDevice.Id, app.PathHash);
            }
        }

        if (AudioLockEnabled)
        {
            UpdateAudioLock();
        }
    }

    private void UpdateAudioLock()
    {
        if (SpeakersDevice is null || HeadphonesDevice is null)
        {
            AddLog("Audio Lock requiere un dispositivo de parlantes y uno de audífonos.");
            return;
        }

        _ruleEngine.EnableAudioLock(
            SpeakersDevice.Id,
            HeadphonesDevice.Id,
            SpeakerApps.Select(a => a.Key));
    }

    private void AddToSpeakers(DetectedAppItem? app)
    {
        if (app is null || SpeakersDevice is null)
        {
            AddLog("Selecciona primero el dispositivo de parlantes.");
            return;
        }

        if (SpeakerApps.All(a => !string.Equals(a.Key, app.Key, StringComparison.OrdinalIgnoreCase)))
        {
            SpeakerApps.Add(new SpeakerAppItem { Key = app.Key, Name = app.Name, PathHash = app.PathHash });
        }

        _ruleEngine.SetRule(app.Key, app.Name, SpeakersDevice.Id, app.PathHash);
        app.IsOnSpeakers = true;

        if (AudioLockEnabled)
        {
            UpdateAudioLock();
        }

        AddLog($"Regla añadida: {app.Name} -> {SpeakersDevice.Name}");
        ApplyAll(manual: false);
    }

    private void RemoveFromSpeakers(SpeakerAppItem? app)
    {
        if (app is null)
        {
            return;
        }

        _ruleEngine.RemoveRule(app.Key);
        var existing = SpeakerApps.FirstOrDefault(a => string.Equals(a.Key, app.Key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SpeakerApps.Remove(existing);
        }

        var detected = DetectedApps.FirstOrDefault(a => string.Equals(a.Key, app.Key, StringComparison.OrdinalIgnoreCase));
        if (detected is not null)
        {
            detected.IsOnSpeakers = false;
        }

        if (AudioLockEnabled)
        {
            UpdateAudioLock();
        }

        AddLog($"Regla eliminada: {app.Name} (ahora va a audífonos)");
    }

    private void ToggleRouting()
    {
        if (RoutingEnabled)
        {
            RoutingEnabled = false;
            AddLog("Routing detenido.");
            return;
        }

        RoutingEnabled = true;
        AddLog("Routing iniciado.");
        ApplyAll(manual: false);
    }

    private void RefreshDevicesAndSessions()
    {
        RefreshDevices();
        PersistSelection();
        RefreshSessions();
        AddLog("Dispositivos y sesiones actualizados.");
    }

    private void RefreshSessions()
    {
        IReadOnlyList<AudioSessionInfo> sessions;
        try
        {
            sessions = _sessionManager.GetSessions();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not enumerate sessions");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in sessions)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            seen.Add(key);

            var item = DetectedApps.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                item = new DetectedAppItem
                {
                    Key = key,
                    Name = session.ApplicationName ?? session.ProcessName ?? key,
                    PathHash = session.ApplicationPathHash,
                    ProcessName = session.ProcessName,
                    DeviceName = session.DeviceName
                };
                DetectedApps.Add(item);
            }
            else
            {
                // R2: keep the displayed device/process fresh, never stale.
                item.ProcessName = session.ProcessName;
                item.DeviceName = session.DeviceName;
            }

            item.IsOnSpeakers = SpeakerApps.Any(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        for (var i = DetectedApps.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(DetectedApps[i].Key))
            {
                DetectedApps.RemoveAt(i);
            }
        }
    }

    private void ApplyAll(bool manual)
    {
        if (SpeakersDevice is null || HeadphonesDevice is null)
        {
            if (manual)
            {
                AddLog("Selecciona un dispositivo de parlantes y uno de audífonos.");
            }
            return;
        }

        IReadOnlyList<AudioSessionInfo> sessions;
        try
        {
            sessions = _sessionManager.GetSessions();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not enumerate sessions for routing");
            return;
        }

        var applied = 0;
        var verified = 0;
        var unverified = 0;
        foreach (var session in sessions)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = _ruleEngine.Resolve(key, session.ApplicationPathHash);
            if (string.IsNullOrWhiteSpace(resolution.OutputDeviceId))
            {
                continue;
            }

            var result = _routingManager.Apply(session.ProcessId, key, resolution.OutputDeviceId);
            if (result.Success)
            {
                applied++;
                if (result.Verified)
                {
                    verified++;
                }
                else
                {
                    unverified++;
                }
            }
        }

        if (manual)
        {
            // R6: never report a plain success when verification was not possible.
            AddLog(unverified == 0
                ? $"Routing applied and verified for {verified} session(s)."
                : $"Routing requested for {applied} session(s): {verified} verified, {unverified} not verified (app may need to restart its audio stream).");
        }
    }

    private void OnSessionStarted(object? sender, AudioSessionInfo session)
    {
        _dispatcher.BeginInvoke(() =>
        {
            RefreshSessions();

            if (!RoutingEnabled)
            {
                return;
            }

            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = _ruleEngine.Resolve(key, session.ApplicationPathHash);
            if (!string.IsNullOrWhiteSpace(resolution.OutputDeviceId))
            {
                var result = _routingManager.Apply(session.ProcessId, key, resolution.OutputDeviceId);
                var verdict = result.Verified ? "verified" : "requested (not verified yet)";
                AddLog($"New session routed: {session.ProcessName} -> {resolution.Reason} [{verdict}]");
            }
        });
    }

    private void OnSessionEnded(object? sender, AudioSessionInfo session)
    {
        _dispatcher.BeginInvoke(RefreshSessions);
    }

    private void OnSessionsChanged(object? sender, IReadOnlyList<AudioSessionInfo> sessions)
    {
        _dispatcher.BeginInvoke(RefreshSessions);
    }

    private void AddLog(string message)
    {
        Log.Info(message);
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        Log.LineWritten -= OnLogLineWritten;
        _monitor.Dispose();
        _deviceManager.Dispose();
        _sessionManager.Dispose();
    }
}
