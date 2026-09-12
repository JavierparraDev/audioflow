using AudioFlow.Models;

namespace AudioFlow.Session;

/// <summary>
/// Session-scoped routing: capture the original state, apply temporary rules,
/// and always restore on end (idempotent). Restoration matches applications by
/// stable identity, never by PID alone.
/// </summary>
public sealed class AudioFlowSessionManager
{
    private readonly IAudioRoutingBackend _backend;
    private readonly SessionMarker _marker;
    private readonly object _sync = new();
    private AudioRoutingSnapshot? _snapshot;

    public AudioFlowSessionManager(IAudioRoutingBackend backend, SessionMarker? marker = null)
    {
        _backend = backend;
        _marker = marker ?? new SessionMarker();
    }

    public SessionState State { get; private set; } = SessionState.Inactive;

    public AudioRoutingSnapshot? Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public bool HasStaleSession => State != SessionState.Active && _marker.Exists;

    /// <summary>Creates the snapshot and writes the recovery marker before any routing change.</summary>
    public bool StartSession()
    {
        lock (_sync)
        {
            if (State == SessionState.Active)
            {
                return true;
            }

            _snapshot = new AudioRoutingSnapshot
            {
                OwnerProcessId = (uint)Environment.ProcessId,
                DefaultRenderDeviceId = _backend.GetDefaultRenderDeviceId()
            };

            if (!_marker.Write(_snapshot))
            {
                _snapshot = null;
                return false;
            }

            State = SessionState.Active;
            return true;
        }
    }

    /// <summary>
    /// Records the original endpoint (once) and applies the route. The marker is
    /// persisted before the write so recovery is always possible.
    /// </summary>
    public bool ApplyRoute(ApplicationIdentity identity, uint processId, string targetDeviceId, out string? error)
    {
        error = null;

        lock (_sync)
        {
            if (State != SessionState.Active && !StartSession())
            {
                error = "Could not start a session.";
                return false;
            }

            var entry = GetOrCreateEntry(identity);
            if (!entry.ProcessIds.Contains(processId))
            {
                entry.ProcessIds.Add(processId);
            }

            entry.AudioFlowTargetDeviceId = targetDeviceId;
            entry.ModifiedAt = DateTimeOffset.UtcNow;

            if (!entry.OriginalCaptured)
            {
                var original = _backend.GetPersistedEndpoint(processId, out _);
                entry.OriginalDeviceId = original;
                entry.OriginalCaptured = true;
                entry.HadOverride = !string.IsNullOrWhiteSpace(original)
                    && !string.Equals(original, _snapshot!.DefaultRenderDeviceId, StringComparison.OrdinalIgnoreCase);
            }

            if (!_marker.Write(_snapshot!))
            {
                error = "Could not write the session marker.";
                return false;
            }

            return _backend.SetPersistedEndpoint(processId, targetDeviceId, out error);
        }
    }

    public void RecordMute(uint processId)
    {
        lock (_sync)
        {
            if (_snapshot is null || _snapshot.MutedProcessIds.Contains(processId))
            {
                return;
            }

            _snapshot.MutedProcessIds.Add(processId);
            _marker.Write(_snapshot);
        }
    }

    /// <summary>
    /// Restores every application AudioFlow modified. Safe to call multiple
    /// times. Applications that are not running are reported Failed and retried
    /// on the next launch (the marker is kept).
    /// </summary>
    public RestoreReport RestoreAll(bool deleteMarkerOnSuccess = true)
    {
        lock (_sync)
        {
            var snapshot = _snapshot ?? _marker.Read();
            if (snapshot is null)
            {
                _snapshot = null;
                State = SessionState.Inactive;
                return RestoreReport.Empty;
            }

            _snapshot = snapshot;
            snapshot.State = "restoring";
            _marker.Write(snapshot);

            var results = new List<ApplicationRestoreResult>();
            var remaining = new List<AudioApplicationSnapshot>();

            foreach (var app in snapshot.Applications)
            {
                var result = RestoreSingle(app);
                results.Add(result);
                if (result.Status == RestoreStatus.Failed)
                {
                    remaining.Add(app);
                }
            }

            foreach (var pid in snapshot.MutedProcessIds)
            {
                _backend.SetProcessMute(pid, false);
            }

            var report = new RestoreReport(
                snapshot.SessionId,
                remaining.Count == 0,
                results,
                DateTimeOffset.UtcNow);

            if (report.Success)
            {
                if (deleteMarkerOnSuccess)
                {
                    _marker.Delete();
                }

                _snapshot = null;
                State = SessionState.Inactive;
            }
            else
            {
                snapshot.State = "active";
                snapshot.Applications = remaining;
                snapshot.MutedProcessIds.Clear();
                _marker.Write(snapshot);
                State = SessionState.Stale;
            }

            return report;
        }
    }

    /// <summary>
    /// Restores a single application (fail-safe / device-loss handling). Only the
    /// named application is touched.
    /// </summary>
    public ApplicationRestoreResult RestoreApplication(string applicationIdentifier)
    {
        lock (_sync)
        {
            var snapshot = _snapshot ?? _marker.Read();
            if (snapshot is null)
            {
                return new ApplicationRestoreResult(applicationIdentifier, RestoreStatus.Skipped, null, "No active session.");
            }

            _snapshot = snapshot;
            var app = snapshot.Applications.FirstOrDefault(a =>
                string.Equals(a.ApplicationIdentifier, applicationIdentifier, StringComparison.OrdinalIgnoreCase));

            if (app is null)
            {
                return new ApplicationRestoreResult(applicationIdentifier, RestoreStatus.Skipped, null, "Not modified by AudioFlow.");
            }

            var result = RestoreSingle(app);
            if (result.Status != RestoreStatus.Failed)
            {
                snapshot.Applications.Remove(app);
            }

            PersistOrDelete(snapshot);
            return result;
        }
    }

    /// <summary>
    /// Handles a lost output device: restores only the applications routed to it,
    /// using the original device when still available, otherwise the current
    /// Windows default (Fallback). Never touches unrelated applications.
    /// </summary>
    public DeviceLossReport HandleDeviceLost(string deviceId)
    {
        lock (_sync)
        {
            var snapshot = _snapshot ?? _marker.Read();
            if (snapshot is null)
            {
                return DeviceLossReport.None;
            }

            _snapshot = snapshot;
            var affected = snapshot.Applications
                .Where(a => string.Equals(a.AudioFlowTargetDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (affected.Count == 0)
            {
                return new DeviceLossReport(deviceId, Array.Empty<ApplicationRestoreResult>());
            }

            var results = new List<ApplicationRestoreResult>();
            foreach (var app in affected)
            {
                var result = RestoreSingle(app);
                results.Add(result);
                if (result.Status != RestoreStatus.Failed)
                {
                    snapshot.Applications.Remove(app);
                }
            }

            PersistOrDelete(snapshot);
            return new DeviceLossReport(deviceId, results);
        }
    }

    private ApplicationRestoreResult RestoreSingle(AudioApplicationSnapshot app)
    {
        var live = SafeGetActiveProcesses();
        var pid = ResolveLiveProcess(app, live);
        if (pid is null)
        {
            return new ApplicationRestoreResult(app.ApplicationIdentifier, RestoreStatus.Failed, null,
                "Application is not running; restoration will be retried on the next launch.");
        }

        string? target;
        RestoreStatus status;

        if (!string.IsNullOrWhiteSpace(app.OriginalDeviceId) && _backend.DeviceExists(app.OriginalDeviceId))
        {
            target = app.OriginalDeviceId;
            status = RestoreStatus.Exact;
        }
        else
        {
            target = _backend.GetDefaultRenderDeviceId();
            status = RestoreStatus.Fallback;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return new ApplicationRestoreResult(app.ApplicationIdentifier, RestoreStatus.Failed, null,
                "No restore device available.");
        }

        if (!_backend.SetPersistedEndpoint(pid.Value, target, out var error))
        {
            return new ApplicationRestoreResult(app.ApplicationIdentifier, RestoreStatus.Failed, target, error);
        }

        return new ApplicationRestoreResult(app.ApplicationIdentifier, status, target, null);
    }

    private void PersistOrDelete(AudioRoutingSnapshot snapshot)
    {
        if (snapshot.Applications.Count == 0)
        {
            _marker.Delete();
            _snapshot = null;
            State = SessionState.Inactive;
            return;
        }

        snapshot.State = "active";
        _marker.Write(snapshot);
        State = SessionState.Stale;
    }

    public RestoreReport EndSession() => RestoreAll(deleteMarkerOnSuccess: true);

    /// <summary>
    /// If a previous session ended uncleanly, restores Windows audio. Does NOT
    /// re-activate any routing rules.
    /// </summary>
    public SessionRecoveryReport RecoverIfNeeded()
    {
        lock (_sync)
        {
            if (!_marker.Exists)
            {
                State = SessionState.Inactive;
                return SessionRecoveryReport.None;
            }

            var snapshot = _marker.Read();
            if (snapshot is null)
            {
                _marker.Delete();
                return SessionRecoveryReport.None;
            }

            _snapshot = snapshot;
            var report = RestoreAll(deleteMarkerOnSuccess: true);
            if (report.Success)
            {
                State = SessionState.Recovered;
            }

            return new SessionRecoveryReport(true, report);
        }
    }

    private AudioApplicationSnapshot GetOrCreateEntry(ApplicationIdentity identity)
    {
        var entry = _snapshot!.Applications.FirstOrDefault(a =>
            string.Equals(a.ApplicationIdentifier, identity.Key, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            entry = AudioApplicationSnapshot.From(identity);
            _snapshot.Applications.Add(entry);
        }

        return entry;
    }

    private static uint? ResolveLiveProcess(AudioApplicationSnapshot app, IReadOnlyList<SessionProcessInfo> live)
    {
        var match = live.FirstOrDefault(p =>
            string.Equals(p.Identity.Key, app.ApplicationIdentifier, StringComparison.OrdinalIgnoreCase));

        if (match is null && !string.IsNullOrWhiteSpace(app.PathHash))
        {
            match = live.FirstOrDefault(p =>
                string.Equals(p.Identity.PathHash, app.PathHash, StringComparison.OrdinalIgnoreCase));
        }

        if (match is null && !string.IsNullOrWhiteSpace(app.ExecutableName))
        {
            match = live.FirstOrDefault(p =>
                string.Equals(p.Identity.ProcessName, app.ExecutableName, StringComparison.OrdinalIgnoreCase));
        }

        if (match is null && app.ProcessIds.Count > 0)
        {
            match = live.FirstOrDefault(p => app.ProcessIds.Contains(p.ProcessId));
        }

        return match?.ProcessId;
    }

    private IReadOnlyList<SessionProcessInfo> SafeGetActiveProcesses()
    {
        try
        {
            return _backend.GetActiveProcesses();
        }
        catch
        {
            return Array.Empty<SessionProcessInfo>();
        }
    }
}
