namespace AudioFlow.Models;

/// <summary>
/// Mirrors the Windows <c>DeviceState</c> flags without leaking the NAudio type
/// into the domain model.
/// </summary>
public enum AudioDeviceState
{
    Active = 0x1,
    Disabled = 0x2,
    NotPresent = 0x4,
    Unplugged = 0x8,
    All = Active | Disabled | NotPresent | Unplugged,
    Unknown = 0
}
