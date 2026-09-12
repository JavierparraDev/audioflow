using System.Text.Json;
using System.Text.Json.Serialization;
using AudioFlow.Configuration;
using AudioFlow.Models;

namespace AudioFlow.Rules;

/// <summary>
/// Persists <see cref="AudioRuleSet"/> as JSON under
/// %APPDATA%\AudioFlow\rules.json (on Windows) so rules survive restarts.
/// Corrupt or unreadable files never crash the app: a fresh rule set is used.
/// </summary>
public sealed class RuleStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public RuleStorage(string? filePath = null) => FilePath = filePath ?? DefaultPath();

    public string FilePath { get; }

    /// <summary>Error from the last load/save operation, if any.</summary>
    public string? LastError { get; private set; }

    public static string DefaultPath()
    {
        // User rules live in %APPDATA%\AudioFlow (or the portable data folder),
        // never inside the installation directory.
        return AppPaths.RulesFile;
    }

    public AudioRuleSet Load()
    {
        LastError = null;

        try
        {
            if (!File.Exists(FilePath))
            {
                return new AudioRuleSet();
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AudioRuleSet();
            }

            return JsonSerializer.Deserialize<AudioRuleSet>(json, Options) ?? new AudioRuleSet();
        }
        catch (Exception ex)
        {
            LastError = $"Could not load rules: {ex.Message}";
            return new AudioRuleSet();
        }
    }

    public bool Save(AudioRuleSet ruleSet)
    {
        LastError = null;

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(ruleSet, Options);

            // Atomic-ish save (R8): write to a temp file, then replace the real
            // file. A crash mid-write cannot corrupt the existing rules.
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(FilePath))
            {
                var backupPath = FilePath + ".bak";
                File.Replace(tempPath, FilePath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Could not save rules: {ex.Message}";
            return false;
        }
    }
}
