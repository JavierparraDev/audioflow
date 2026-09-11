namespace AudioFlow.Models;

/// <summary>
/// A user-defined routing rule: "application X should play on device Y".
/// </summary>
public sealed class AudioRule
{
    public string RuleId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Stable application key, e.g. "spotify.exe" or "spotify:app".</summary>
    public string ApplicationIdentifier { get; set; } = string.Empty;

    /// <summary>Human readable name for the UI, e.g. "Spotify".</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Endpoint ID of the target output device.</summary>
    public string OutputDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Optional SHA-256 prefix of the executable path. When present it is used
    /// as a secondary match to disambiguate applications that share a file name.
    /// </summary>
    public string? PathHash { get; set; }

    public bool Enabled { get; set; } = true;
}
