using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioFlow.Configuration;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON with an atomic write, so a
/// crash during save cannot corrupt the existing configuration.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SettingsStore(string? filePath = null) => FilePath = filePath ?? AppPaths.SettingsFile;

    public string FilePath { get; }

    public string? LastError { get; private set; }

    public AppSettings Load()
    {
        LastError = null;

        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            var migrated = ConfigMigrator.Migrate(settings, out var changed);
            if (changed)
            {
                Save(migrated);
            }

            return migrated;
        }
        catch (Exception ex)
        {
            LastError = $"Could not load settings: {ex.Message}";
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        LastError = null;

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, Options);
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, FilePath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Could not save settings: {ex.Message}";
            return false;
        }
    }
}
