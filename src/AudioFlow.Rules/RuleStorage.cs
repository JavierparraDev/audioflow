using System.Text.Json;
using System.Text.Json.Serialization;
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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "AudioFlow", "rules.json");
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
            File.WriteAllText(FilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Could not save rules: {ex.Message}";
            return false;
        }
    }
}
