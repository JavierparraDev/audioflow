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
            var live = SafeGetActiveProcesses();
            var results = new List<ApplicationRestoreResult>();

            foreach (var app in snapshot.Applications)
            {
                var pid = ResolveLiveProcess(app, live);
                if (pid is null)
                {
                    results.Add(new ApplicationRestoreResult(
                        app.ApplicationIdentifier,
                        RestoreStatus.Failed,
                        null,
                        "Application is not running; restoration will be retried on the next launch."));
                    continue;
                }

                var exact = !string.IsNullOrWhiteSpace(app.OriginalDeviceId);
                var target = exact ? app.OriginalDeviceId : _backend.GetDefaultRenderDeviceId();

                if (string.IsNullOrWhiteSpace(target))
                {
                    results.Add(new ApplicationRestoreResult(
                        app.ApplicationIdentifier, RestoreStatus.Failed, null, "No restore device available."));
                    continue;
                }

                if (!_backend.SetPersistedEndpoint(pid.Value, target, out var error))
                {
                    results.Add(new ApplicationRestoreResult(
                        app.ApplicationIdentifier, RestoreStatus.Failed, target, error));
                    continue;
                }

                results.Add(new ApplicationRestoreResult(
                    app.ApplicationIdentifier,
                    exact ? RestoreStatus.Exact : RestoreStatus.Fallback,
                    target,
                    null));
            }

            foreach (var pid in snapshot.MutedProcessIds)
            {
                _backend.SetProcessMute(pid, false);
            }

            var report = new RestoreReport(
                snapshot.SessionId,
                results.All(r => r.Status != RestoreStatus.Failed),
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
                // Keep only the failed entries so recovery can retry them.
                snapshot.Applications = snapshot.Applications
                    .Where(a => results.Any(r =>
                        r.ApplicationIdentifier == a.ApplicationIdentifier && r.Status == RestoreStatus.Failed))
                    .ToList();
                snapshot.MutedProcessIds.Clear();
                _marker.Write(snapshot);
                State = SessionState.Stale;
            }

            return report;
        }
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
