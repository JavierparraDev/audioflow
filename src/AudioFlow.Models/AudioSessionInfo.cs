namespace AudioFlow.Models;

/// <summary>
/// Snapshot of an audio session detected on an endpoint.
/// Some fields are optional because Windows does not always expose them.
/// </summary>
public sealed class AudioSessionInfo
{
    /// <summary>From IAudioSessionControl2::GetSessionIdentifier. Stable for an app+device.</summary>
    public string SessionIdentifier { get; set; } = string.Empty;

    /// <summary>From IAudioSessionControl2::GetSessionInstanceIdentifier. Unique per instance.</summary>
    public string? SessionInstanceIdentifier { get; set; }

    /// <summary>From IAudioSessionControl2::GetProcessId.</summary>
    public uint ProcessId { get; set; }

    /// <summary>Resolved via ProcessManager. Optional.</summary>
    public string? ProcessName { get; set; }

    /// <summary>Resolved via ProcessManager. Optional.</summary>
    public string? ProcessPath { get; set; }

    /// <summary>From IAudioSessionControl::GetDisplayName. Often empty.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Endpoint the session was enumerated from. Null when unknown.</summary>
    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    /// <summary>From IAudioSessionControl::GetState.</summary>
    public AudioSessionStateKind State { get; set; } = AudioSessionStateKind.Unknown;

    /// <summary>From ISimpleAudioVolume::GetMasterVolume (0.0 - 1.0).</summary>
    public float Volume { get; set; }

    /// <summary>From ISimpleAudioVolume::GetMute.</summary>
    public bool IsMuted { get; set; }

    public bool IsSystemSoundsSession { get; set; }

    /// <summary>Last time this session was observed by AudioFlow.</summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.Now;

    public override string ToString() =>
        $"{ProcessName ?? "?"} (pid {ProcessId}) [{State}]";
}
