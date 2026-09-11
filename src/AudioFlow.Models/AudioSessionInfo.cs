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

    /// <summary>Stable application key used by routing rules, e.g. "exe:spotify.exe".</summary>
    public string? ApplicationKey { get; set; }

    /// <summary>Application User Model ID for packaged apps, when available.</summary>
    public string? Aumid { get; set; }

    /// <summary>Friendly application name for the UI.</summary>
    public string? ApplicationName { get; set; }

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

    /// <summary>From IAudioMeterInformation::GetPeakValue (0.0 - 1.0). Used to detect audible output.</summary>
    public float PeakValue { get; set; }

    public bool IsSystemSoundsSession { get; set; }

    /// <summary>True when the session is active and producing audible output.</summary>
    public bool IsAudible => State == AudioSessionStateKind.Active && PeakValue > 0.0001f;

    /// <summary>Last time this session was observed by AudioFlow.</summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.Now;

    public override string ToString() =>
        $"{ProcessName ?? "?"} (pid {ProcessId}) [{State}]";
}
