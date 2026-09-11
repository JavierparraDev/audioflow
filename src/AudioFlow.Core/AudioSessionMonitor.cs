using AudioFlow.Applications;
using AudioFlow.Core.Logging;
using AudioFlow.Models;

namespace AudioFlow.Core;

/// <summary>
/// Watches the audio sessions in real time.
///
/// Windows exposes a callback for *new* sessions
/// (IAudioSessionManager2::RegisterSessionNotification) but has no callback for
/// session destruction. AudioFlow therefore combines device-change events with a
/// light periodic refresh (default 1s) to detect started/ended sessions and
/// state changes. The refresh only runs while there is something to report, so
/// it stays cheap.
/// </summary>
public sealed class AudioSessionMonitor : IDisposable
{
    private readonly AudioDeviceManager _deviceManager;
    private readonly ProcessManager _processManager;
    private readonly TimeSpan _interval;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Dictionary<string, AudioSessionInfo> _known = new(StringComparer.OrdinalIgnoreCase);

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>Raised the first time a session is observed.</summary>
    public event EventHandler<AudioSessionInfo>? SessionStarted;

    /// <summary>Raised when a session disappears or expires.</summary>
    public event EventHandler<AudioSessionInfo>? SessionEnded;

    /// <summary>Raised after every refresh with the full snapshot.</summary>
    public event EventHandler<IReadOnlyList<AudioSessionInfo>>? SessionsChanged;

    public AudioSessionMonitor(
        AudioDeviceManager deviceManager,
        ProcessManager? processManager = null,
        TimeSpan? interval = null)
    {
        _deviceManager = deviceManager;
        _processManager = processManager ?? new ProcessManager();
        _interval = interval ?? TimeSpan.FromSeconds(1);
        _deviceManager.DevicesChanged += OnDevicesChanged;
    }

    /// <summary>Latest snapshot of active sessions.</summary>
    public IReadOnlyList<AudioSessionInfo> Current { get; private set; } = Array.Empty<AudioSessionInfo>();

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running || _disposed)
        {
            return;
        }

        _running = true;
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "AudioFlow.SessionMonitor"
        };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _wake.Set();
        _thread?.Join(TimeSpan.FromSeconds(3));
        _thread = null;
    }

    private void Loop()
    {
        using var sessionManager = new AudioSessionManager(_processManager);

        while (_running)
        {
            try
            {
                Refresh(sessionManager);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Session monitor refresh failed");
            }

            _wake.WaitOne(_interval);
        }
    }

    private void Refresh(AudioSessionManager sessionManager)
    {
        var current = sessionManager.GetSessions(includeInactive: false);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in current)
        {
            var key = KeyOf(session);
            seen.Add(key);

            if (!_known.ContainsKey(key))
            {
                _known[key] = session;
                Log.Info($"Audio session detected: {session.ProcessName ?? "?"} (pid {session.ProcessId}) on {session.DeviceName}");
                SessionStarted?.Invoke(this, session);
            }
            else
            {
                _known[key] = session;
            }
        }

        foreach (var key in _known.Keys.ToList())
        {
            if (seen.Contains(key))
            {
                continue;
            }

            var ended = _known[key];
            _known.Remove(key);
            Log.Info($"Audio session ended: {ended.ProcessName ?? "?"} (pid {ended.ProcessId})");
            SessionEnded?.Invoke(this, ended);
        }

        Current = current;
        SessionsChanged?.Invoke(this, current);
    }

    private static string KeyOf(AudioSessionInfo session) =>
        !string.IsNullOrEmpty(session.SessionInstanceIdentifier)
            ? session.SessionInstanceIdentifier!
            : $"{session.SessionIdentifier}|{session.ProcessId}";

    private void OnDevicesChanged(object? sender, AudioDeviceChangedEventArgs e) => _wake.Set();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _deviceManager.DevicesChanged -= OnDevicesChanged;
        _wake.Dispose();
    }
}
