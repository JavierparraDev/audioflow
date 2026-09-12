using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace AudioFlow.Core.WindowsAudio;

/// <summary>
/// Snapshots and restores the Windows per-application audio routing stored under
/// HKCU\...\LowRegistry\Audio\PolicyConfig\PropertyStore.
///
/// AudioFlow is session-only: it captures the state before applying any rule and
/// puts it back when it closes, so no change survives. Restoration matches
/// subkeys by executable name and only touches applications AudioFlow managed.
/// </summary>
public static class AudioPolicyRegistryGuard
{
    private const string PropertyStorePath =
        @"Software\Microsoft\Internet Explorer\LowRegistry\Audio\PolicyConfig\PropertyStore";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Captures the whole per-application audio policy store as JSON, or null when
    /// unavailable (non-Windows host or read failure).
    /// </summary>
    public static string? Capture()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var snapshot = new PolicyRegistrySnapshot();

            using var root = Registry.CurrentUser.OpenSubKey(PropertyStorePath, writable: false);
            if (root is not null)
            {
                foreach (var subKeyName in root.GetSubKeyNames())
                {
                    using var subKey = root.OpenSubKey(subKeyName, writable: false);
                    if (subKey is null)
                    {
                        continue;
                    }

                    var key = new PolicyRegistryKey { Name = subKeyName };
                    foreach (var valueName in subKey.GetValueNames())
                    {
                        key.Values.Add(ReadValue(subKey, valueName));
                    }

                    snapshot.Keys.Add(key);
                }
            }

            return JsonSerializer.Serialize(snapshot, Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Restores the captured state for the given executables: subkeys that did not
    /// exist before are deleted, and modified ones are put back. Returns true when
    /// the state is clean (also true when there is nothing to restore).
    /// </summary>
    public static bool Revert(string? snapshotJson, IReadOnlyList<string>? executableNames)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(snapshotJson))
        {
            return true;
        }

        var names = Normalize(executableNames);
        if (names.Count == 0)
        {
            return true;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<PolicyRegistrySnapshot>(snapshotJson, Options);
            if (snapshot is null)
            {
                return true;
            }

            var original = snapshot.Keys.ToDictionary(k => k.Name, StringComparer.OrdinalIgnoreCase);

            using var root = Registry.CurrentUser.OpenSubKey(PropertyStorePath, writable: true);
            if (root is null)
            {
                // Nothing exists now: there is nothing AudioFlow could have left.
                return true;
            }

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var subKey = root.OpenSubKey(subKeyName, writable: false);
                if (subKey is null || !MatchesExecutable(subKey, names))
                {
                    continue;
                }

                if (original.TryGetValue(subKeyName, out var originalKey))
                {
                    RestoreKey(root, subKeyName, originalKey);
                }
                else
                {
                    root.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes the policy subkeys for the given executables. Used by the one-time
    /// cleanup of changes left by earlier AudioFlow versions. Returns keys removed.
    /// </summary>
    public static int RemoveEntriesForExecutables(IEnumerable<string> executableNames)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var names = Normalize(executableNames);
        if (names.Count == 0)
        {
            return 0;
        }

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(PropertyStorePath, writable: true);
            if (root is null)
            {
                return 0;
            }

            var removed = 0;
            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var subKey = root.OpenSubKey(subKeyName, writable: false);
                if (subKey is null || !MatchesExecutable(subKey, names))
                {
                    continue;
                }

                try
                {
                    root.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
                    removed++;
                }
                catch
                {
                    // best effort
                }
            }

            return removed;
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> Normalize(IEnumerable<string>? executableNames) =>
        (executableNames ?? Array.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool MatchesExecutable(RegistryKey subKey, IReadOnlyList<string> executableNames)
    {
        var value = subKey.GetValue(null) as string;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var name in executableNames)
        {
            // The default value ends with "...\App.exe%b{GUID}".
            if (value.Contains("\\" + name + "%b", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("\\" + name, StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void RestoreKey(RegistryKey root, string subKeyName, PolicyRegistryKey originalKey)
    {
        using var subKey = root.CreateSubKey(subKeyName, writable: true);
        if (subKey is null)
        {
            return;
        }

        var originalNames = originalKey.Values
            .Select(v => v.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var valueName in subKey.GetValueNames())
        {
            if (!originalNames.Contains(valueName))
            {
                try
                {
                    subKey.DeleteValue(valueName, throwOnMissingValue: false);
                }
                catch
                {
                    // best effort
                }
            }
        }

        foreach (var value in originalKey.Values)
        {
            try
            {
                WriteValue(subKey, value);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static PolicyRegistryValue ReadValue(RegistryKey key, string name)
    {
        var kind = key.GetValueKind(name);
        var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        var result = new PolicyRegistryValue { Name = name, Kind = kind.ToString() };
        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                result.StringValue = value as string;
                break;
            case RegistryValueKind.Binary:
                result.BinaryBase64 = value is byte[] bytes ? Convert.ToBase64String(bytes) : null;
                break;
            case RegistryValueKind.DWord:
            case RegistryValueKind.QWord:
                result.NumberValue = value is null ? null : Convert.ToInt64(value);
                break;
            case RegistryValueKind.MultiString:
                result.MultiStringValue = value as string[];
                break;
        }

        return result;
    }

    private static void WriteValue(RegistryKey key, PolicyRegistryValue value)
    {
        if (!Enum.TryParse<RegistryValueKind>(value.Kind, out var kind))
        {
            return;
        }

        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                key.SetValue(value.Name, value.StringValue ?? string.Empty, kind);
                break;
            case RegistryValueKind.Binary:
                key.SetValue(
                    value.Name,
                    value.BinaryBase64 is null ? Array.Empty<byte>() : Convert.FromBase64String(value.BinaryBase64),
                    kind);
                break;
            case RegistryValueKind.DWord:
                key.SetValue(value.Name, (int)(value.NumberValue ?? 0), kind);
                break;
            case RegistryValueKind.QWord:
                key.SetValue(value.Name, value.NumberValue ?? 0, kind);
                break;
            case RegistryValueKind.MultiString:
                key.SetValue(value.Name, value.MultiStringValue ?? Array.Empty<string>(), kind);
                break;
        }
    }

    private sealed class PolicyRegistrySnapshot
    {
        public List<PolicyRegistryKey> Keys { get; set; } = new();
    }

    private sealed class PolicyRegistryKey
    {
        public string Name { get; set; } = string.Empty;
        public List<PolicyRegistryValue> Values { get; set; } = new();
    }

    private sealed class PolicyRegistryValue
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = nameof(RegistryValueKind.String);
        public string? StringValue { get; set; }
        public string? BinaryBase64 { get; set; }
        public long? NumberValue { get; set; }
        public string[]? MultiStringValue { get; set; }
    }
}
