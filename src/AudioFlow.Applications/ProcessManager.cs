using System.Diagnostics;

namespace AudioFlow.Applications;

/// <summary>
/// Information about a running process, resolved from a process id.
/// Any field can be null when Windows does not allow access (protected process).
/// </summary>
public sealed record ProcessDetails(
    uint ProcessId,
    string? ProcessName,
    string? ExecutablePath,
    string? Description,
    bool IsAccessible)
{
    public static ProcessDetails Unknown(uint pid) => new(pid, null, null, null, false);
}

/// <summary>
/// Resolves process ids to names, executable paths and descriptions.
/// Uses System.Diagnostics.Process (documented .NET API over the Win32 process API).
/// </summary>
public sealed class ProcessManager
{
    public ProcessDetails Resolve(uint processId)
    {
        if (processId == 0)
        {
            return ProcessDetails.Unknown(0);
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);

            string? path = null;
            string? description = null;

            try
            {
                // MainModule can throw for elevated/protected processes.
                var mainModule = process.MainModule;
                path = mainModule?.FileName;
                description = mainModule?.FileVersionInfo.FileDescription;
            }
            catch
            {
                // Access denied: keep whatever we already have.
            }

            var name = !string.IsNullOrWhiteSpace(path)
                ? Path.GetFileName(path)
                : process.ProcessName + ".exe";

            return new ProcessDetails(processId, name, path, description, true);
        }
        catch
        {
            return ProcessDetails.Unknown(processId);
        }
    }
}
