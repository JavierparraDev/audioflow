namespace AudioFlow.Models;

/// <summary>
/// Mirrors the Windows <c>AudioSessionState</c> enumeration
/// (Inactive = 0, Active = 1, Expired = 2).
/// </summary>
public enum AudioSessionStateKind
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
    Unknown = 3
}
