using System.Text.Json;

namespace AudioFlow.Configuration;

/// <summary>
/// Removes every record AudioFlow may leave behind on disk so that, once the
/// application is closed, nothing about it remains. User settings (language,
/// window behaviour) are intentionally kept.
/// </summary>
public static class UserDataCleanup
{
    /// <summary>Deletes rules.json and its temporary/backup siblings.</summary>
    public static int DeleteRuleFiles()
    {
        var removed = 0;
        foreach (var path in new[] { AppPaths.RulesFile, AppPaths.RulesFile + ".tmp", AppPaths.RulesFile + ".bak" })
        {
            removed += TryDeleteFile(path);
        }

        return removed;
    }

    /// <summary>Deletes the session marker folder (session.json, .bak, .tmp).</summary>
    public static int DeleteSessionFiles() => DeleteDirectory(Path.Combine(AppPaths.UserDataDirectory, "session"));

    /// <summary>Deletes the logs folder. Call <c>Log.StopFile()</c> first.</summary>
    public static int DeleteLogs() => DeleteDirectory(AppPaths.LogsDirectory);

    /// <summary>
    /// Reads the executable names AudioFlow managed according to any leftover
    /// state files (rules.json / session.json). Used once to remove the audio
    /// registry changes those rules implied. Must be called BEFORE deleting the
    /// files.
    /// </summary>
    public static IReadOnlyList<string> CollectLegacyExecutables()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ReadRulesExecutables(AppPaths.RulesFile, names);

        var sessionDirectory = Path.Combine(AppPaths.UserDataDirectory, "session");
        if (Directory.Exists(sessionDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(sessionDirectory, "session.json*"))
            {
                ReadSessionExecutables(file, names);
            }
        }

        return names.ToList();
    }

    private static void ReadRulesExecutables(string path, HashSet<string> names)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (!rule.TryGetProperty("applicationIdentifier", out var identifier))
                {
                    continue;
                }

                var value = identifier.GetString();
                if (value is not null && value.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = value[4..];
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }
        }
        catch
        {
            // Legacy files are best effort.
        }
    }

    private static void ReadSessionExecutables(string path, HashSet<string> names)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("applications", out var applications) ||
                applications.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var application in applications.EnumerateArray())
            {
                if (application.TryGetProperty("executableName", out var executableName) &&
                    executableName.GetString() is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }
        }
        catch
        {
            // Legacy files are best effort.
        }
    }

    private static int DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                removed += TryDeleteFile(file);
            }

            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // best effort
        }

        return removed;
    }

    private static int TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return 1;
            }
        }
        catch
        {
            // best effort
        }

        return 0;
    }
}
