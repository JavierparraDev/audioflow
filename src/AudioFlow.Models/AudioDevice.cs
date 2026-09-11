namespace AudioFlow.Models;

/// <summary>
/// A physical or virtual audio endpoint as exposed by the Windows MMDevice API.
/// </summary>
public sealed class AudioDevice
{
    /// <summary>Stable endpoint ID (e.g. "{0.0.0.00000000}.{guid}").</summary>
    public required string Id { get; init; }

    /// <summary>User-visible name, e.g. "Parlantes (Realtek High Definition Audio)".</summary>
    public required string FriendlyName { get; init; }

    /// <summary>Adapter/device friendly name, e.g. "Realtek High Definition Audio".</summary>
    public string? DeviceFriendlyName { get; init; }

    /// <summary>Device instance id (stable across endpoint changes).</summary>
    public string? InstanceId { get; init; }

    /// <summary>Path to the device icon, if any.</summary>
    public string? IconPath { get; init; }

    public AudioDeviceState State { get; init; }

    public AudioFlowDataFlow DataFlow { get; init; } = AudioFlowDataFlow.Render;

    /// <summary>True when this endpoint is the default for any role.</summary>
    public bool IsDefault { get; init; }

    public override string ToString() => FriendlyName;
}
