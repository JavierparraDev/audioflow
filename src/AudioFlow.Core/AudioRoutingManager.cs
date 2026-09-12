using AudioFlow.Core.Logging;
using AudioFlow.Core.WindowsAudio;

namespace AudioFlow.Core;

/// <summary>
/// Outcome of applying a route to one process.
///
/// The status distinguishes an accepted request from a verified result (R6):
/// the internal API can accept a write while the process has no active stream,
/// in which case the routing could not be verified.
/// </summary>
public enum RoutingStatus
{
    /// <summary>Write accepted and read-back matched the requested device.</summary>
    AppliedVerified,

    /// <summary>Write accepted but the persisted value could not be confirmed.</summary>
    AppliedUnverified,

    /// <summary>Write failed (no audio on the process, unavailable API, invalid device...).</summary>
    Failed
}

public sealed record RoutingApplyResult(
    uint ProcessId,
    string? ApplicationKey,
    string DeviceId,
    bool Success,
    bool Verified,
    RoutingStatus Status,
    string? Error);

/// <summary>
/// Applies routing decisions to running processes.
///
/// MVP strategy (Phase 5A): Windows' internal AudioPolicyConfig factory sets the
/// persisted output endpoint for a process. This is what the Windows Settings
/// "App volume and device preferences" page does.
///
/// Limitation: the change is picked up when the application (re)initializes its
/// audio stream. A currently playing stream is not moved. AudioFlow verifies the
/// write by reading the persisted value back.
///
/// Phase 5B (experimental) adds the official Process Loopback API for live
/// re-routing without restarting the app's stream.
/// </summary>
public sealed class AudioRoutingManager
{
    /// <summary>
    /// Points a process at the given output device and verifies the write.
    /// </summary>
    public RoutingApplyResult Apply(uint processId, string? applicationKey, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new RoutingApplyResult(
                processId, applicationKey, deviceId, false, false, RoutingStatus.Failed, "No target device.");
        }

        if (!AudioPolicyConfig.TrySetPersistedEndpoint(processId, deviceId, out var error))
        {
            Log.Warn($"Routing failed for pid {processId} ({applicationKey ?? "?"}): {error}");
            return new RoutingApplyResult(
                processId, applicationKey, deviceId, false, false, RoutingStatus.Failed, error);
        }

        var readBack = AudioPolicyConfig.TryGetPersistedEndpoint(processId, out _);
        var verified = string.Equals(readBack, deviceId, StringComparison.OrdinalIgnoreCase);
        var status = verified ? RoutingStatus.AppliedVerified : RoutingStatus.AppliedUnverified;

        if (verified)
        {
            Log.Info(
                $"Routing applied and verified: {applicationKey ?? $"pid {processId}"} -> {deviceId}");
        }
        else
        {
            Log.Warn(
                $"Routing requested for {applicationKey ?? $"pid {processId}"} -> {deviceId}, " +
                "but the output could not be verified (the application may not have an active stream).");
        }

        return new RoutingApplyResult(
            processId, applicationKey, deviceId, true, verified, status, null);
    }

    /// <summary>Reads back the persisted output endpoint for a process.</summary>
    public string? GetPersistedEndpoint(uint processId) =>
        AudioPolicyConfig.TryGetPersistedEndpoint(processId, out _);
}
