using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using AudioFlow.Core;
using AudioFlow.Core.Logging;
using AudioFlow.Models;
using AudioFlow.Rules;
using AudioFlow.UI.Localization;
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

        Languages = new ObservableCollection<LanguageOption>
        {
            new() { Code = "en", Label = Loc.Get("LanguageEnglish") },
            new() { Code = "es", Label = Loc.Get("LanguageSpanish") }
        };
        _selectedLanguage = Languages[0];

        AddToSpeakersCommand = new RelayCommand(p => AddToSpeakers(p as DetectedAppItem));
        RemoveFromSpeakersCommand = new RelayCommand(p => RemoveFromSpeakers(p as SpeakerAppItem));
        StartStopCommand = new RelayCommand(_ => ToggleRouting());
        ApplyNowCommand = new RelayCommand(_ => ApplyAll(manual: true));
        RefreshCommand = new RelayCommand(_ => RefreshDevicesAndSessions());
        OpenLogsCommand = new RelayCommand(_ => OpenLogFolder());
    }

    public ObservableCollection<DeviceOption> OutputDevices { get; } = new();
    public ObservableCollection<SpeakerAppItem> SpeakerApps { get; } = new();
    public ObservableCollection<DetectedAppItem> DetectedApps { get; } = new();
    public ObservableCollection<SessionDiagnostic> Diagnostics { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();
    public ObservableCollection<LanguageOption> Languages { get; }

    public ICommand AddToSpeakersCommand { get; }
    public ICommand RemoveFromSpeakersCommand { get; }
    public ICommand StartStopCommand { get; }
    public ICommand ApplyNowCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenLogsCommand { get; }

    private LanguageOption _selectedLanguage;
    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value) || value is null)
            {
                return;
            }

            Loc.SetLanguage(value.Code);
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StartStopText));
            OnPropertyChanged(nameof(AudioLockStateText));
        }
    }

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

            AddLog(Loc.Get(value ? "AudioLockEnabled" : "AudioLockDisabled"));
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

    public string StartStopText => Loc.Get(RoutingEnabled ? "BtnStop" : "BtnStart");
    public string StatusText => Loc.Get(RoutingEnabled ? "StatusRunning" : "StatusStopped");
    public string AudioLockStateText => Loc.Get(AudioLockEnabled ? "AudioLockOn" : "AudioLockOff");
    public string SpeakersDeviceName => SpeakersDevice?.Name ?? Loc.Get("Default");
    public string HeadphonesDeviceName => HeadphonesDevice?.Name ?? Loc.Get("Default");

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
            ? devices.FirstOrDefault(d => ContainsAny(d.FriendlyName, "speaker", "parlante", "altavoz", "altavoces")) ?? defaultDevice
            : devices.FirstOrDefault(d => ContainsAny(d.FriendlyName, "headphone", "audífono", "audifono", "auricular", "headset"))
              ?? devices.FirstOrDefault(d => !string.Equals(d.Id, excludeId, StringComparison.OrdinalIgnoreCase));

        return candidate is null ? null : FindOption(candidate.Id);
    }

    private DeviceOption? FindOption(string id) =>
        OutputDevices.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));

    private string DeviceNameOf(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Loc.Get("Default");
        }

        var option = FindOption(deviceId);
        return option?.Name ?? deviceId;
    }

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
            AddLog(Loc.Get("AudioLockNeedsDevices"));
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
            AddLog(Loc.Get("SelectSpeakersFirst"));
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

        AddLog(Loc.Format("RuleAdded", app.Name, SpeakersDevice.Name));
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

        AddLog(Loc.Format("RuleRemoved", app.Name));
    }

    private void ToggleRouting()
    {
        if (RoutingEnabled)
        {
            RoutingEnabled = false;
            AddLog(Loc.Get("RoutingStopped"));
            return;
        }

        RoutingEnabled = true;
        AddLog(Loc.Get("RoutingStarted"));
        ApplyAll(manual: false);
    }

    private void RefreshDevicesAndSessions()
    {
        RefreshDevices();
        PersistSelection();
        RefreshSessions();
        AddLog(Loc.Get("DevicesRefreshed"));
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

            var resolution = _ruleEngine.Resolve(key, session.ApplicationPathHash);
            item.TargetDeviceName = DeviceNameOf(resolution.OutputDeviceId);
            item.RuleLabel = Loc.Get(resolution.HasExplicitRule ? "RoutingExplicit" : "RoutingDefault");
        }

        for (var i = DetectedApps.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(DetectedApps[i].Key))
            {
                DetectedApps.RemoveAt(i);
            }
        }

        RebuildDiagnostics(sessions);
    }

    private void RebuildDiagnostics(IReadOnlyList<AudioSessionInfo> sessions)
    {
        Diagnostics.Clear();
        foreach (var session in sessions)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = _ruleEngine.Resolve(key, session.ApplicationPathHash);
            var onTarget = !string.IsNullOrWhiteSpace(resolution.OutputDeviceId) &&
                           string.Equals(session.DeviceId, resolution.OutputDeviceId, StringComparison.OrdinalIgnoreCase);

            Diagnostics.Add(new SessionDiagnostic
            {
                Process = session.ProcessName ?? "?",
                Pid = session.ProcessId,
                Aumid = session.Aumid,
                SessionId = session.SessionInstanceIdentifier ?? session.SessionIdentifier,
                Endpoint = session.DeviceId,
                Device = session.DeviceName,
                State = session.State.ToString(),
                Volume = $"{(int)Math.Round(session.Volume * 100)}%",
                Peak = session.PeakValue.ToString("0.000"),
                Routing = resolution.HasExplicitRule ? Loc.Get("RoutingExplicit") : Loc.Get("RoutingDefault"),
                Verified = onTarget ? Loc.Get("RoutingVerified") : Loc.Get("RoutingUnverified")
            });
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioFlow", "logs");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not open the log folder");
        }
    }

    private void ApplyAll(bool manual)
    {
        if (SpeakersDevice is null || HeadphonesDevice is null)
        {
            if (manual)
            {
                AddLog(Loc.Get("SelectDevices"));
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
                ? Loc.Format("RoutingApplied", verified)
                : Loc.Format("RoutingRequested", applied, verified, unverified));
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
                AddLog($"{session.ProcessName} -> {resolution.Reason} [{verdict}]");
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
