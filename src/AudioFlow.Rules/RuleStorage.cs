using AudioFlow.Configuration;
using AudioFlow.Models;

namespace AudioFlow.Rules;

/// <summary>
/// In-memory rule storage. AudioFlow is session-only: rules are never written to
/// disk, so nothing survives after the application closes. The legacy file path
/// is kept only so any rules.json left by earlier versions can be deleted.
/// </summary>
public sealed class RuleStorage
{
    public RuleStorage(string? filePath = null) => FilePath = filePath ?? DefaultPath();

    public string FilePath { get; }

    /// <summary>Error from the last load/save/delete operation, if any.</summary>
    public string? LastError { get; private set; }

    public static string DefaultPath()
    {
        // Legacy location. Rules are no longer persisted here; it is only used to
        // clean up files created by previous versions.
        return AppPaths.RulesFile;
    }

    /// <summary>Always returns a fresh, empty rule set: nothing is persisted.</summary>
    public AudioRuleSet Load()
    {
        LastError = null;
        return new AudioRuleSet();
    }

    /// <summary>
    /// No-op. Rules live in memory for the current session only; this never
    /// creates or modifies a file.
    /// </summary>
    public bool Save(AudioRuleSet ruleSet)
    {
        LastError = null;
        return true;
    }

    /// <summary>
    /// Deletes a legacy rules file and its temporary/backup siblings, if present.
    /// Returns the number of files removed.
    /// </summary>
    public int DeleteLegacyFiles()
    {
        LastError = null;
        var removed = 0;

        foreach (var path in new[] { FilePath, FilePath + ".tmp", FilePath + ".bak" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Could not delete {path}: {ex.Message}";
            }
        }

        return removed;
    }
}
