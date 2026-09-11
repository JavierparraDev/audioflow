using AudioFlow.Core.Logging;
using AudioFlow.Core.WindowsAudio;

namespace AudioFlow.Core;

/// <summary>Outcome of applying a route to one process.</summary>
public sealed record RoutingApplyResult(
    uint ProcessId,
    string? ApplicationKey,
    string DeviceId,
    bool Success,
    bool Verified,
    string? Error);

/// <summary>
/// Applies routing decisions to running processes.
///
/// MVP strategy (Phase 5A): Windows' internal AudioPolicyConfig factory sets the
/// persisted output endpoint for a process. This is exactly what the Windows
/// Settings "App volume and device preferences" page does.
///
/// Limitation: the change is picked up when the application (re)initializes its
/// audio stream. A currently playing stream is not moved. AudioFlow verifies the
/// write by reading the persisted value back.
///
/// Phase 5B (planned) will add the official Process Loopback API for live
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
            return new RoutingApplyResult(processId, applicationKey, deviceId, false, false, "No target device.");
        }

        if (!AudioPolicyConfig.TrySetPersistedEndpoint(processId, deviceId, out var error))
        {
            Log.Warn($"Routing failed for pid {processId} ({applicationKey ?? "?"}): {error}");
            return new RoutingApplyResult(processId, applicationKey, deviceId, false, false, error);
        }

        var readBack = AudioPolicyConfig.TryGetPersistedEndpoint(processId, out _);
        var verified = string.Equals(readBack, deviceId, StringComparison.OrdinalIgnoreCase);

        Log.Info(
            $"Routing applied: {applicationKey ?? $"pid {processId}"} -> {deviceId} " +
            $"(persisted, verified={verified})");

        return new RoutingApplyResult(processId, applicationKey, deviceId, true, verified, null);
    }

    /// <summary>Reads back the persisted output endpoint for a process.</summary>
    public string? GetPersistedEndpoint(uint processId) =>
        AudioPolicyConfig.TryGetPersistedEndpoint(processId, out _);
}
