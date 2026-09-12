using AudioFlow.Models;

namespace AudioFlow.Session;

/// <summary>Result of restoring one application.</summary>
public enum RestoreStatus
{
    /// <summary>The exact original device was restored.</summary>
    Exact,

    /// <summary>The original could not be restored; the current Windows default was used.</summary>
    Fallback,

    /// <summary>The application routing could not be safely restored.</summary>
    Failed,

    /// <summary>Nothing needed to be restored for this application.</summary>
    Skipped
}

/// <summary>
/// A snapshot of one application's audio routing, keyed by stable identity (never
/// by PID alone).
/// </summary>
public sealed class AudioApplicationSnapshot
{
    public string ApplicationIdentifier { get; set; } = string.Empty;
    public string? Aumid { get; set; }
    public string? ExecutablePath { get; set; }
    public string? PathHash { get; set; }
    public string? ExecutableName { get; set; }

    /// <summary>Device read before AudioFlow changed anything. Null when it could not be read.</summary>
    public string? OriginalDeviceId { get; set; }

    /// <summary>True once AudioFlow attempted to capture the original.</summary>
    public bool OriginalCaptured { get; set; }

    /// <summary>Best-effort: the original differed from the system default (a real per-app override).</summary>
    public bool HadOverride { get; set; }

    /// <summary>The device AudioFlow routed the app to (for diagnostics).</summary>
    public string? AudioFlowTargetDeviceId { get; set; }

    /// <summary>PIDs seen for this application (runtime information only).</summary>
    public List<uint> ProcessIds { get; set; } = new();

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    public static AudioApplicationSnapshot From(ApplicationIdentity identity) => new()
    {
        ApplicationIdentifier = identity.Key,
        Aumid = identity.Aumid,
        ExecutablePath = identity.ExecutablePath,
        PathHash = identity.PathHash,
        ExecutableName = identity.ProcessName
    };
}

/// <summary>The persisted session snapshot (the recovery marker).</summary>
public sealed class AudioRoutingSnapshot
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string State { get; set; } = "active";

    /// <summary>PID of the AudioFlow process that owns this session (to detect stale sessions).</summary>
    public uint OwnerProcessId { get; set; }

    /// <summary>System default render device at session start (for fallback restore).</summary>
    public string? DefaultRenderDeviceId { get; set; }

    public List<AudioApplicationSnapshot> Applications { get; set; } = new();

    /// <summary>Processes AudioFlow muted (must be unmuted on restore).</summary>
    public List<uint> MutedProcessIds { get; set; } = new();
}

public sealed record ApplicationRestoreResult(
    string ApplicationIdentifier,
    RestoreStatus Status,
    string? DeviceId,
    string? Reason);

public sealed record RestoreReport(
    string SessionId,
    bool Success,
    IReadOnlyList<ApplicationRestoreResult> Results,
    DateTimeOffset CompletedAt)
{
    public int ExactCount => Results.Count(r => r.Status == RestoreStatus.Exact);
    public int FallbackCount => Results.Count(r => r.Status == RestoreStatus.Fallback);
    public int FailedCount => Results.Count(r => r.Status == RestoreStatus.Failed);

    public static RestoreReport Empty { get; } =
        new("none", true, Array.Empty<ApplicationRestoreResult>(), DateTimeOffset.UtcNow);

    public string Summary =>
        $"exact={ExactCount} fallback={FallbackCount} failed={FailedCount}";
}

public enum SessionState
{
    Inactive,
    Active,
    Stale,
    Recovered
}

public sealed record SessionRecoveryReport(bool HadStaleSession, RestoreReport Restore)
{
    public static SessionRecoveryReport None { get; } =
        new(false, RestoreReport.Empty);
}
