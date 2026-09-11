using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AudioFlow.Models;

namespace AudioFlow.Applications;

/// <summary>
/// Builds a stable identity for an application so that routing rules survive
/// process restarts (a process id changes every time an app is reopened).
///
/// Identification strategy:
///   1. Packaged (Store/UWP) apps: Application User Model ID via
///      GetApplicationUserModelId (kernel32, documented).
///   2. Win32 apps: executable file name, plus a path hash for disambiguation.
///
/// The process id is never used as the rule key.
/// </summary>
public sealed class ApplicationIdentifier
{
    private readonly ProcessManager _processManager;

    public ApplicationIdentifier(ProcessManager? processManager = null) =>
        _processManager = processManager ?? new ProcessManager();

    public ApplicationIdentity Identify(uint processId) =>
        Identify(_processManager.Resolve(processId));

    public ApplicationIdentity Identify(ProcessDetails details)
    {
        var aumid = TryGetAumid(details.ProcessId);

        var processName = details.ProcessName;
        var pathHash = HashPath(details.ExecutablePath);

        var key = !string.IsNullOrWhiteSpace(aumid)
            ? $"aumid:{aumid}"
            : !string.IsNullOrWhiteSpace(processName)
                ? $"exe:{processName.ToLowerInvariant()}"
                : $"pid:{details.ProcessId}";

        var displayName = !string.IsNullOrWhiteSpace(details.Description)
            ? details.Description
            : StripExtension(processName) ?? aumid ?? $"pid {details.ProcessId}";

        return new ApplicationIdentity
        {
            Key = key,
            DisplayName = displayName,
            ProcessName = processName,
            ExecutablePath = details.ExecutablePath,
            PathHash = pathHash,
            Aumid = aumid,
            Kind = !string.IsNullOrWhiteSpace(aumid)
                ? ApplicationKind.Packaged
                : details.IsAccessible
                    ? ApplicationKind.Win32
                    : ApplicationKind.Unknown
        };
    }

    private static string? StripExtension(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : Path.GetFileNameWithoutExtension(name);

    private static string? HashPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;
    private const int ErrorSuccess = 0;

    private static string? TryGetAumid(uint processId)
    {
        if (processId == 0 || !OperatingSystem.IsWindows())
        {
            return null;
        }

        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            uint length = 0;
            var result = GetApplicationUserModelId(handle, ref length, null);
            if (result != ErrorInsufficientBuffer || length == 0)
            {
                return null;
            }

            var buffer = new StringBuilder((int)length);
            result = GetApplicationUserModelId(handle, ref length, buffer);
            return result == ErrorSuccess ? buffer.ToString() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetApplicationUserModelId(
        IntPtr hProcess,
        ref uint applicationUserModelIdLength,
        StringBuilder? applicationUserModelId);
}
