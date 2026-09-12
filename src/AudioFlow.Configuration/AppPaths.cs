namespace AudioFlow.Configuration;

/// <summary>
/// Resolves where AudioFlow stores user data.
///
/// Installed: %APPDATA%\AudioFlow (survives upgrades and reinstalls).
/// Portable : &lt;app&gt;\data (a "portable.txt" marker next to the executable).
///
/// Binaries and user data are always separated.
/// </summary>
public static class AppPaths
{
    public const string AppName = "AudioFlow";

    private const string PortableMarkerFile = "portable.txt";
    private const string PortableMarkerAlt = "portable.flag";

    public static string BaseDirectory => AppContext.BaseDirectory;

    /// <summary>True when a portable marker file sits next to the executable.</summary>
    public static bool IsPortable =>
        File.Exists(Path.Combine(BaseDirectory, PortableMarkerFile)) ||
        File.Exists(Path.Combine(BaseDirectory, PortableMarkerAlt));

    public static string UserDataDirectory
    {
        get
        {
            if (IsPortable)
            {
                var portableDir = Path.Combine(BaseDirectory, "data");
                Directory.CreateDirectory(portableDir);
                return portableDir;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = BaseDirectory;
            }

            var dir = Path.Combine(appData, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string RulesFile => Path.Combine(UserDataDirectory, "rules.json");

    public static string SettingsFile => Path.Combine(UserDataDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(UserDataDirectory, "logs");

    public static string BackupDirectory => Path.Combine(UserDataDirectory, "backup");

    /// <summary>Creates a timestamped backup of the whole user-data directory.</summary>
    public static string CreateBackup(DateTimeOffset? timestamp = null)
    {
        var source = UserDataDirectory;
        var stamp = (timestamp ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss");
        var destination = Path.Combine(BackupDirectory, stamp);

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith("backup", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return destination;
    }
}
