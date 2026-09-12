using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AudioFlow.Configuration;
using AudioFlow.Core;
using AudioFlow.Core.Logging;
using AudioFlow.Models;
using AudioFlow.Rules;
using AudioFlow.Routing;
using AudioFlow.Session;
using AudioFlow.UI.Localization;
using AudioFlow.UI.Services;
using AudioFlow.UI.ViewModels;
using AudioFlow.Updates;
using Application = System.Windows.Application;
using ApplicationIdentity = AudioFlow.Models.ApplicationIdentity;

namespace AudioFlow.UI.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AudioDeviceManager _deviceManager = new();
    private readonly RuleEngine _ruleEngine = new();
    private readonly AudioSessionManager _sessionManager = new();
    private readonly WindowsAudioRoutingBackend _routingBackend = new();
    private readonly AudioFlowSessionManager _routingSession;
    private readonly VirtualAudioDeviceManager _virtualDevices = new();
    private readonly RoutingBackendRegistry _routingRegistry;
    private readonly AudioSessionMonitor _monitor;
    private readonly Dispatcher _dispatcher;
    private readonly SettingsStore _settingsStore = new();

    private AppSettings _settings = new();
    private UpdateService? _updateService;
    private CancellationTokenSource? _updateCts;

    private bool _suppressSideEffects;
    private bool _initialized;
    private bool _disposed;
    private Process? _guardianProcess;

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _monitor = new AudioSessionMonitor(_deviceManager);
        _routingSession = new AudioFlowSessionManager(_routingBackend);
        _routingRegistry = new RoutingBackendRegistry(_deviceManager, _virtualDevices);

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
        CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync(force: true));
        UpdateNowCommand = new RelayCommand(async _ => await UpdateNowAsync());
        OpenReleasesCommand = new RelayCommand(_ => OpenUrl(UpdateDefaults.RepositoryUrl + "/releases"));
        EmergencyResetCommand = new RelayCommand(_ => EmergencyReset());
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
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand UpdateNowCommand { get; }
    public ICommand OpenReleasesCommand { get; }
    public ICommand EmergencyResetCommand { get; }

    public string SessionStateText => _routingSession.State switch
    {
        AudioFlow.Session.SessionState.Active => Loc.Get("SessionActive"),
        AudioFlow.Session.SessionState.Stale => Loc.Get("SessionStale"),
        AudioFlow.Session.SessionState.Recovered => Loc.Get("SessionRecovered"),
        _ => Loc.Get("SessionInactive")
    };

    public string RoutingModeText => Loc.Get("RoutingModeTemporary");
    public string SafetyText => Loc.Get("SafetyHint");

    public string GuardianStatusText =>
        _guardianProcess is { HasExited: false } ? Loc.Get("GuardianRunning") : Loc.Get("GuardianNotRunning");

    public string BackendNameText => _routingRegistry.SelectPreferred()?.Info.Name ?? "none";
    public string BackendStatusText => _routingRegistry.SelectPreferred()?.Info.Status ?? "N/A";
    public string VirtualEndpointText =>
        _virtualDevices.GetVirtualRenderEndpoint()?.FriendlyName ?? Loc.Get("VirtualNotAvailable");
    public string RoutingEngineNote => Loc.Get("RoutingEngineNote");

    private bool _closeCompletely;
    public bool CloseCompletely
    {
        get => _closeCompletely;
        set
        {
            if (SetProperty(ref _closeCompletely, value))
            {
                _settings.CloseCompletely = value;
                SaveSettings();
            }
        }
    }

    public string AppVersionText => AudioFlowVersion.Current;

    private string _updateCurrentVersion = AudioFlowVersion.Current;
    public string UpdateCurrentVersion
    {
        get => _updateCurrentVersion;
        private set => SetProperty(ref _updateCurrentVersion, value);
    }

    private string _updateLatestVersion = "-";
    public string UpdateLatestVersion
    {
        get => _updateLatestVersion;
        private set => SetProperty(ref _updateLatestVersion, value);
    }

    private string _updateStatus = string.Empty;
    public string UpdateStatusText
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    private string _updateMessage = string.Empty;
    public string UpdateMessage
    {
        get => _updateMessage;
        private set => SetProperty(ref _updateMessage, value);
    }

    private string _lastChecked = "-";
    public string LastChecked
    {
        get => _lastChecked;
        private set => SetProperty(ref _lastChecked, value);
    }

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    private bool _isCheckingUpdate;
    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        private set => SetProperty(ref _isCheckingUpdate, value);
    }

    private bool _checkForUpdatesEnabled = true;
    public bool CheckForUpdatesEnabled
    {
        get => _checkForUpdatesEnabled;
        set
        {
            if (!SetProperty(ref _checkForUpdatesEnabled, value))
            {
                return;
            }

            _settings.CheckForUpdates = value;
            SaveSettings();
        }
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetProperty(ref _startWithWindows, value))
            {
                return;
            }

            StartupRegistration.SetEnabled(value);
            _settings.StartWithWindows = value;
            SaveSettings();
        }
    }

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
            _settings.Language = value.Code;
            SaveSettings();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StartStopText));
            OnPropertyChanged(nameof(AudioLockStateText));
            OnPropertyChanged(nameof(UpdateStatusText));
            OnPropertyChanged(nameof(UpdateMessage));
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
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            Log.UseFile(Path.Combine(AppPaths.LogsDirectory, "audioflow.log"));
        }
        catch
        {
            // Logging to file is best effort.
        }

        Log.LineWritten += OnLogLineWritten;
        Log.Info($"AudioFlow {AudioFlowVersion.Current} started");

        LoadSettingsAndUpdates();

        // Crash recovery FIRST: restore Windows audio, never re-activate routing.
        try
        {
            var recovery = new CrashRecoveryService(_routingSession).Recover();
            if (recovery.HadStaleSession)
            {
                Log.Warn($"Recovered a previous unclean session: {recovery.Restore.Summary}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Crash recovery failed");
        }

        OnPropertyChanged(nameof(SessionStateText));

        RefreshDevices();
        LoadRules();

        _monitor.SessionStarted += OnSessionStarted;
        _monitor.SessionEnded += OnSessionEnded;
        _monitor.SessionsChanged += OnSessionsChanged;
        _deviceManager.DevicesChanged += OnDevicesChanged;
        _monitor.Start();

        RefreshSessions();
    }

    private void LoadSettingsAndUpdates()
    {
        _settings = _settingsStore.Load();

        // Apply the persisted language.
        var language = Languages.FirstOrDefault(l =>
                           string.Equals(l.Code, _settings.Language, StringComparison.OrdinalIgnoreCase))
                       ?? Languages[0];
        _selectedLanguage = language;
        Loc.SetLanguage(language.Code);

        _startWithWindows = StartupRegistration.IsEnabled();
        OnPropertyChanged(nameof(StartWithWindows));

        _checkForUpdatesEnabled = _settings.CheckForUpdates;
        OnPropertyChanged(nameof(CheckForUpdatesEnabled));

        _closeCompletely = _settings.CloseCompletely;
        OnPropertyChanged(nameof(CloseCompletely));

        UpdateCurrentVersion = AudioFlowVersion.Current;
        if (_settings.LastUpdateCheckUtc is { } last)
        {
            LastChecked = last.ToLocalTime().ToString("g");
        }

        InitializeUpdateService();

        if (_settings.CheckForUpdates)
        {
            // Never block startup waiting for GitHub.
            _ = CheckForUpdatesAsync(force: false);
        }
    }

    private void InitializeUpdateService()
    {
        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();
        _updateService?.Dispose();
        _updateService = new UpdateService(
            new GitHubReleaseSource(),
            AppVersion.Parse(AudioFlowVersion.Current),
            UpdateService.ParseChannel(_settings.UpdateChannel));
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

    /// <summary>Stops routing and restores Windows audio (used by the tray menu).</summary>
    public void StopRouting()
    {
        if (RoutingEnabled)
        {
            ToggleRouting();
        }
        else
        {
            _routingSession.EndSession();
            OnPropertyChanged(nameof(SessionStateText));
        }
    }

    private void ToggleRouting()
    {
        if (RoutingEnabled)
        {
            RoutingEnabled = false;
            var report = _routingSession.EndSession();
            AddLog(report.Success
                ? $"{Loc.Get("RoutingStopped")} ({report.Summary})"
                : $"{Loc.Get("RoutingStopped")} - restore incomplete ({report.Summary})");
            OnPropertyChanged(nameof(SessionStateText));
            return;
        }

        if (!_routingSession.StartSession())
        {
            AddLog("Could not start an AudioFlow session.");
            return;
        }

        RoutingEnabled = true;
        OnPropertyChanged(nameof(SessionStateText));
        AddLog(Loc.Get("RoutingStarted"));
        LaunchGuardian();
        ApplyAll(manual: false);
    }

    private void LaunchGuardian()
    {
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "AudioFlow.SessionGuardian.exe");
            if (!File.Exists(exe))
            {
                Log.Warn("Session guardian not found next to the application; continuing without it.");
                OnPropertyChanged(nameof(GuardianStatusText));
                return;
            }

            _guardianProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--owner-pid {Environment.ProcessId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Log.Info("Session guardian started.");
            OnPropertyChanged(nameof(GuardianStatusText));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not start the session guardian");
        }
    }

    private void OnDevicesChanged(object? sender, AudioDeviceChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Reason))
        {
            return;
        }

        // React to disconnects: "removed:<id>" or "state:<id>".
        if (!e.Reason.StartsWith("removed:", StringComparison.OrdinalIgnoreCase) &&
            !e.Reason.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var separator = e.Reason.IndexOf(':');
        if (separator < 0 || separator + 1 >= e.Reason.Length)
        {
            return;
        }

        var deviceId = e.Reason[(separator + 1)..];

        _dispatcher.BeginInvoke(() =>
        {
            var report = _routingSession.HandleDeviceLost(deviceId);
            if (report.AffectedCount == 0)
            {
                return;
            }

            foreach (var result in report.Results)
            {
                AddLog($"DEVICE LOST {result.ApplicationIdentifier} -> {result.Status} ({result.DeviceId})");
            }

            OnPropertyChanged(nameof(SessionStateText));
        });
    }

    private void EmergencyReset()
    {        RoutingEnabled = false;

        try
        {
            var report = _routingSession.EndSession();
            _ruleEngine.DisableAudioLock();
            _audioLockEnabled = false;
            OnPropertyChanged(nameof(AudioLockEnabled));
            OnPropertyChanged(nameof(SessionStateText));
            AddLog(report.Success
                ? $"{Loc.Get("EmergencyResetDone")} ({report.Summary})"
                : $"{Loc.Get("EmergencyResetIncomplete")} ({report.Summary})");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Emergency reset failed");
        }
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
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            Process.Start(new ProcessStartInfo { FileName = AppPaths.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not open the log folder");
        }
    }

    private void SaveSettings()
    {
        _settings.ConfigVersion = ConfigMigrator.CurrentVersion;
        if (!_settingsStore.Save(_settings))
        {
            Log.Warn(_settingsStore.LastError ?? "Could not save settings");
        }
    }

    private async Task CheckForUpdatesAsync(bool force)
    {
        if (_updateService is null || IsCheckingUpdate)
        {
            return;
        }

        IsCheckingUpdate = true;
        UpdateStatusText = Loc.Get("UpdateChecking");

        try
        {
            var token = _updateCts?.Token ?? CancellationToken.None;
            var result = await _updateService.CheckAsync(_settings.LastUpdateCheckUtc, force, token);
            ApplyUpdateResult(result);

            if (result.Status is UpdateStatus.UpToDate or UpdateStatus.UpdateAvailable)
            {
                _settings.LastUpdateCheckUtc = result.CheckedAt;
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update check failed");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    private void ApplyUpdateResult(UpdateCheckResult result)
    {
        UpdateCurrentVersion = result.Current.ToString();
        UpdateLatestVersion = result.Latest?.ToString() ?? "-";
        LastChecked = result.CheckedAt.ToLocalTime().ToString("g");
        IsUpdateAvailable = result.UpdateAvailable;

        UpdateStatusText = result.Status switch
        {
            UpdateStatus.UpToDate => Loc.Get("UpdateUpToDate"),
            UpdateStatus.UpdateAvailable => Loc.Get("UpdateAvailable"),
            UpdateStatus.NoReleaseFound => Loc.Get("UpdateNoRelease"),
            UpdateStatus.Failed => Loc.Get("UpdateUnable"),
            _ => Loc.Get("UpdateUnknown")
        };

        UpdateMessage = result.Status switch
        {
            UpdateStatus.UpToDate => Loc.Get("UpdateUpToDateMessage"),
            UpdateStatus.UpdateAvailable => Loc.Format("UpdateAvailableMessage", result.Latest?.ToString() ?? "?"),
            UpdateStatus.NoReleaseFound => Loc.Get("UpdateNoReleaseMessage"),
            UpdateStatus.Failed => Loc.Get("UpdateUnableMessage"),
            _ => string.Empty
        };
    }

    private async Task UpdateNowAsync()
    {
        if (_updateService is null)
        {
            return;
        }

        if (_updateService.LastResult?.Release is null)
        {
            await CheckForUpdatesAsync(force: true);
        }

        var release = _updateService.LastResult?.Release;
        var installer = release?.Installer;
        if (release is null || installer is null)
        {
            AddLog(Loc.Get("UpdateNoInstaller"));
            return;
        }

        var updaterExe = Path.Combine(AppContext.BaseDirectory, "AudioFlow.Updater.exe");
        if (!File.Exists(updaterExe))
        {
            AddLog(Loc.Get("UpdateNoUpdater"));
            return;
        }

        UpdateStatusText = Loc.Get("UpdateDownloading");
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AudioFlow");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, installer.Name);

            // Resolve the checksum from SHA256SUMS.txt when present.
            string? sha = null;
            var sums = release.Assets.FirstOrDefault(a =>
                a.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            if (sums is not null)
            {
                var content = await _updateService.DownloadStringAsync(sums.DownloadUrl, _updateCts?.Token ?? CancellationToken.None);
                if (content is not null)
                {
                    sha = Checksum.FindInSumsFile(content, installer.Name);
                }
            }

            var progress = new Progress<double>(p =>
                UpdateMessage = $"{Loc.Get("UpdateDownloading")} {(int)(p * 100)}%");

            var download = await _updateService.DownloadAsync(
                installer.DownloadUrl, installerPath, sha, progress, _updateCts?.Token ?? CancellationToken.None);

            if (!download.Success)
            {
                UpdateStatusText = Loc.Get("UpdateFailed");
                UpdateMessage = download.Error ?? string.Empty;
                return;
            }

            var args = $"--wait-pid {Environment.ProcessId} --installer \"{installerPath}\"";
            if (!string.IsNullOrWhiteSpace(sha))
            {
                args += $" --sha256 {sha}";
            }

            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                args += $" --restart \"{Environment.ProcessPath}\"";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = args,
                UseShellExecute = true
            });

            Log.Info("Updater launched; shutting down AudioFlow for the update.");
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update failed");
            UpdateStatusText = Loc.Get("UpdateFailed");
            UpdateMessage = ex.Message;
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Opening a URL is best effort.
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
        var failed = 0;
        foreach (var session in sessions)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = _ruleEngine.Resolve(key, session.ApplicationPathHash);
            if (string.IsNullOrWhiteSpace(resolution.OutputDeviceId))
            {
                continue;
            }

            var identity = new ApplicationIdentity
            {
                Key = key,
                Aumid = session.Aumid,
                ExecutablePath = session.ProcessPath,
                PathHash = session.ApplicationPathHash,
                ProcessName = session.ProcessName,
                DisplayName = session.ApplicationName
            };

            if (_routingSession.ApplyRoute(identity, session.ProcessId, resolution.OutputDeviceId, out _))
            {
                applied++;
            }
            else
            {
                failed++;
            }
        }

        if (manual)
        {
            AddLog(Loc.Format("RoutingAppliedTemporary", applied, failed));
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
                var identity = new ApplicationIdentity
                {
                    Key = key,
                    Aumid = session.Aumid,
                    ExecutablePath = session.ProcessPath,
                    PathHash = session.ApplicationPathHash,
                    ProcessName = session.ProcessName,
                    DisplayName = session.ApplicationName
                };

                var ok = _routingSession.ApplyRoute(identity, session.ProcessId, resolution.OutputDeviceId, out var error);
                AddLog($"{session.ProcessName} -> {resolution.Reason} [{(ok ? "applied" : error)}]");
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Log.LineWritten -= OnLogLineWritten;

        // Always restore Windows audio before exiting.
        try
        {
            var report = _routingSession.EndSession();
            if (!report.Success)
            {
                Log.Warn($"Restore incomplete on exit: {report.Summary}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Restore on exit failed");
        }

        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateService?.Dispose();
        _deviceManager.DevicesChanged -= OnDevicesChanged;
        _monitor.Dispose();
        _routingRegistry.Dispose();
        _deviceManager.Dispose();
        _sessionManager.Dispose();
        _routingBackend.Dispose();

        try
        {
            _guardianProcess?.Dispose();
        }
        catch
        {
            // best effort
        }
    }
}
