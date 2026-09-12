using AudioFlow.Models;

namespace AudioFlow.Session;

/// <summary>A running process that produces audio, with its stable identity.</summary>
public sealed record SessionProcessInfo(uint ProcessId, ApplicationIdentity Identity);

/// <summary>
/// Abstraction over the Windows audio routing operations. Keeping this behind an
/// interface lets the session state machine be unit-tested without hardware.
/// </summary>
public interface IAudioRoutingBackend
{
    /// <summary>Reads the persisted output endpoint for a process, or null when none was captured.</summary>
    string? GetPersistedEndpoint(uint processId, out string? error);

    /// <summary>Sets the persisted output endpoint for a process.</summary>
    bool SetPersistedEndpoint(uint processId, string deviceId, out string? error);

    /// <summary>Current system default render device id.</summary>
    string? GetDefaultRenderDeviceId();

    /// <summary>True when the given render endpoint is currently active.</summary>
    bool DeviceExists(string deviceId);

    /// <summary>Running processes that currently have an audio session.</summary>
    IReadOnlyList<SessionProcessInfo> GetActiveProcesses();

    /// <summary>Mutes or unmutes every session of a process. Returns the number changed.</summary>
    int SetProcessMute(uint processId, bool mute);
}
