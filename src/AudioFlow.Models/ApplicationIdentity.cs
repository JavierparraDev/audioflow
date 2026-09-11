namespace AudioFlow.Models;

public enum ApplicationKind
{
    Unknown = 0,
    Win32 = 1,
    Packaged = 2
}

/// <summary>
/// Stable identity of an application, independent of its process id.
/// The <see cref="Key"/> is what routing rules are stored against.
/// </summary>
public sealed class ApplicationIdentity
{
    /// <summary>
    /// Stable rule key. Examples: "exe:spotify.exe" or "aumid:SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify".
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Friendly name for the UI (file description when available).</summary>
    public string? DisplayName { get; init; }

    /// <summary>Executable file name, e.g. "Spotify.exe".</summary>
    public string? ProcessName { get; init; }

    /// <summary>Full executable path, when accessible.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>SHA-256 (first 16 hex chars) of the normalized executable path.</summary>
    public string? PathHash { get; init; }

    /// <summary>Application User Model ID, for packaged (Store/UWP) apps.</summary>
    public string? Aumid { get; init; }

    public ApplicationKind Kind { get; init; } = ApplicationKind.Unknown;

    public override string ToString() => Key;
}
