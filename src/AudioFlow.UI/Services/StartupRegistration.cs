using Microsoft.Win32;

namespace AudioFlow.UI.Services;

/// <summary>
/// Optional "Start AudioFlow with Windows" support via the current-user Run key.
/// No administrator privileges are required. Off by default.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AudioFlow";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled, string? executablePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                var path = executablePath ?? Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                key.SetValue(ValueName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
