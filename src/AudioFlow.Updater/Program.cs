using System.Diagnostics;
using System.IO.Compression;
using AudioFlow.Configuration;
using AudioFlow.Updates;

namespace AudioFlow.Updater;

/// <summary>
/// Minimal, independent updater. It is started by AudioFlow, waits for the main
/// process to exit, backs up user data, verifies the package and applies it.
///
/// Safety rules:
///   - never replaces files while AudioFlow is running;
///   - never runs an installer without a verified SHA-256;
///   - never deletes user data (%APPDATA%\AudioFlow);
///   - fails safely and logs to the user data folder.
/// </summary>
internal static class Program
{
    private static string LogFile => Path.Combine(AppPaths.LogsDirectory, "updater.log");

    private static int Main(string[] args)
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        Log("Updater started.");

        var options = UpdaterOptions.Parse(args);
        if (options is null)
        {
            Log("Invalid arguments. Usage: --installer <path> --sha256 <hex> --restart <exe> | --zip <path> --target <dir>");
            return 2;
        }

        try
        {
            if (options.WaitProcessId is { } pid && pid > 0)
            {
                WaitForExit(pid, TimeSpan.FromSeconds(60));
            }

            var backup = AppPaths.CreateBackup();
            Log($"Configuration backed up to {backup}");

            if (!string.IsNullOrWhiteSpace(options.InstallerPath))
            {
                return ApplyInstaller(options);
            }

            if (!string.IsNullOrWhiteSpace(options.ZipPath))
            {
                return ApplyPortable(options);
            }

            Log("Nothing to apply.");
            return 2;
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex}");
            return 1;
        }
    }

    private static int ApplyInstaller(UpdaterOptions options)
    {
        var installer = options.InstallerPath!;
        if (!File.Exists(installer))
        {
            Log($"Installer not found: {installer}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.Sha256))
        {
            Log("Refusing to run an installer without a SHA-256 checksum.");
            return 3;
        }

        if (!Checksum.Verify(installer, options.Sha256))
        {
            Log("Installer checksum verification FAILED. Aborting.");
            return 3;
        }

        Log("Checksum verified. Running installer silently...");
        var exitCode = RunProcess(
            installer,
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /NOCANCEL");

        Log($"Installer exit code: {exitCode}");
        if (exitCode != 0)
        {
            return exitCode;
        }

        Restart(options);
        return 0;
    }

    private static int ApplyPortable(UpdaterOptions options)
    {
        var zip = options.ZipPath!;
        var target = options.TargetDirectory;
        if (string.IsNullOrWhiteSpace(target))
        {
            Log("Portable update requires --target.");
            return 2;
        }

        if (!File.Exists(zip))
        {
            Log($"Zip not found: {zip}");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(options.Sha256) && !Checksum.Verify(zip, options.Sha256))
        {
            Log("Zip checksum verification FAILED. Aborting.");
            return 3;
        }

        var staging = Path.Combine(Path.GetTempPath(), "audioflow-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            ZipFile.ExtractToDirectory(zip, staging, overwriteFiles: true);
            CopyTree(staging, target);
            Log($"Portable files copied to {target}");
        }
        finally
        {
            TryDeleteDirectory(staging);
        }

        Restart(options);
        return 0;
    }

    private static void CopyTree(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);

            // Never overwrite user data inside a portable install.
            if (relative.StartsWith("data", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("portable.txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void Restart(UpdaterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RestartPath) || !File.Exists(options.RestartPath))
        {
            Log("No restart path; the user can launch AudioFlow manually.");
            return;
        }

        Log($"Restarting {options.RestartPath}");
        RunProcess(options.RestartPath, string.Empty);
    }

    private static int RunProcess(string fileName, string arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        };

        using var process = Process.Start(info);
        if (process is null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static void WaitForExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            Log($"Waiting for process {pid} to exit...");
            process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Process already gone.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // logging must never crash the updater
        }
    }
}

internal sealed class UpdaterOptions
{
    public int? WaitProcessId { get; private set; }
    public string? InstallerPath { get; private set; }
    public string? ZipPath { get; private set; }
    public string? TargetDirectory { get; private set; }
    public string? Sha256 { get; private set; }
    public string? RestartPath { get; private set; }

    public static UpdaterOptions? Parse(string[] args)
    {
        var options = new UpdaterOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].ToLowerInvariant();
            string? Value() => i + 1 < args.Length ? args[++i] : null;

            switch (key)
            {
                case "--wait-pid":
                    if (int.TryParse(Value(), out var pid)) options.WaitProcessId = pid;
                    break;
                case "--installer":
                    options.InstallerPath = Value();
                    break;
                case "--zip":
                    options.ZipPath = Value();
                    break;
                case "--target":
                    options.TargetDirectory = Value();
                    break;
                case "--sha256":
                    options.Sha256 = Value();
                    break;
                case "--restart":
                    options.RestartPath = Value();
                    break;
            }
        }

        var hasWork = !string.IsNullOrWhiteSpace(options.InstallerPath) || !string.IsNullOrWhiteSpace(options.ZipPath);
        return hasWork ? options : null;
    }
}
