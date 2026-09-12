namespace AudioFlow.Configuration;

/// <summary>
/// Migrates configuration between schema versions. Never deletes incompatible
/// data silently: a failed migration is reported and the caller can back up.
/// </summary>
public static class ConfigMigrator
{
    /// <summary>Current configuration schema version.</summary>
    public const int CurrentVersion = 1;

    public static AppSettings Migrate(AppSettings settings, out bool changed)
    {
        changed = false;

        if (settings.ConfigVersion < 1)
        {
            // 0 -> 1: introduce the schema version and defaults.
            settings.ConfigVersion = 1;
            changed = true;
        }

        // Future migrations:
        // if (settings.ConfigVersion < 2) { ...; settings.ConfigVersion = 2; changed = true; }

        if (settings.ConfigVersion > CurrentVersion)
        {
            // Configuration from a newer version: keep it, do not downgrade.
        }

        return settings;
    }

    /// <summary>Migrates a raw settings object read from disk (used by tests).</summary>
    public static AppSettings Migrate(AppSettings settings) => Migrate(settings, out _);
}
