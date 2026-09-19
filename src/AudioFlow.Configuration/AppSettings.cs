namespace AudioFlow.Configuration;

/// <summary>
/// User settings persisted to %APPDATA%\AudioFlow\settings.json (or the portable
/// data folder). Never stored inside the installation directory.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Configuration schema version, used for migrations.</summary>
    public int ConfigVersion { get; set; } = ConfigMigrator.CurrentVersion;

    /// <summary>UI language code, e.g. "en", "es", "tr". See docs/LOCALIZATION.md.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether AudioFlow checks GitHub for updates on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Update channel: "stable", "beta" or "nightly".</summary>
    public string UpdateChannel { get; set; } = "stable";

    /// <summary>Last successful update check (UTC). Used to avoid frequent polling.</summary>
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    /// <summary>A version the user chose to skip.</summary>
    public string? SkippedVersion { get; set; }

    /// <summary>When true, closing the window stops the session and exits. Off by default (minimize to tray).</summary>
    public bool CloseCompletely { get; set; }
}
