using Microsoft.Win32;

namespace AudioFlow.Core.Windows;

/// <summary>
/// Removes the "start with Windows" entry AudioFlow may have created in earlier
/// versions. AudioFlow no longer registers itself to run at logon: it must only
/// exist while the user has it open.
/// </summary>
public static class LegacyStartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AudioFlow";

    /// <summary>Deletes the legacy Run entry. Returns true when one was removed.</summary>
    public static bool Remove()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is null)
            {
                return false;
            }

            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
