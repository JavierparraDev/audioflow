using System.Diagnostics;
using System.Text.Json;
using AudioFlow.Configuration;
using AudioFlow.Session;

namespace AudioFlow.SessionGuardian;

/// <summary>
/// Independent process that monitors an AudioFlow session. If the owner dies
/// while the session marker still exists (a crash), it restores Windows audio.
/// On a clean shutdown the owner removes the marker, so the guardian exits
/// without doing anything.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var options = GuardianOptions.Parse(args);
        if (options is null)
        {
            Console.WriteLine("Usage: AudioFlow.SessionGuardian --owner-pid <pid> [--poll <ms>] [--once]");
            return 2;
        }

        Log("Guardian started.");

        var marker = new SessionMarker(options.MarkerPath);
        var ownerPid = options.OwnerProcessId;
        if (ownerPid == 0)
        {
            ownerPid = (int)(marker.Read()?.OwnerProcessId ?? 0);
        }

        if (options.Once)
        {
            return RunRecovery(marker);
        }

        while (true)
        {
            if (!marker.Exists)
            {
                Log("Session marker removed (clean shutdown). Guardian exiting.");
                return 0;
            }

            if (!IsAlive(ownerPid))
            {
                Log($"Owner process {ownerPid} is no longer running. Restoring...");
                return RunRecovery(marker);
            }

            Thread.Sleep(options.PollMilliseconds);
        }
    }

    private static int RunRecovery(SessionMarker marker)
    {
        try
        {
            using var backend = new WindowsAudioRoutingBackend();
            var manager = new AudioFlowSessionManager(backend, marker);
            var recovery = manager.RecoverIfNeeded();

            var report = new GuardianReport
            {
                Timestamp = DateTimeOffset.UtcNow,
                HadStaleSession = recovery.HadStaleSession,
                Success = recovery.Restore.Success,
                Summary = recovery.Restore.Summary,
                Applications = recovery.Restore.Results
                    .Select(r => new GuardianApplicationResult
                    {
                        ApplicationIdentifier = r.ApplicationIdentifier,
                        Status = r.Status.ToString(),
                        DeviceId = r.DeviceId,
                        Reason = r.Reason
                    })
                    .ToList()
            };

            WriteReport(report);
            Log($"Recovery complete: {report.Summary}");
            return report.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Log($"Guardian recovery failed: {ex}");
            return 1;
        }
    }

    private static bool IsAlive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteReport(GuardianReport report)
    {
        try
        {
            var path = Path.Combine(AppPaths.UserDataDirectory, "session", "guardian-report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
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
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            File.AppendAllText(
                Path.Combine(AppPaths.LogsDirectory, "guardian.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // logging must never crash the guardian
        }
    }
}

internal sealed class GuardianOptions
{
    public int OwnerProcessId { get; private set; }
    public int PollMilliseconds { get; private set; } = 750;
    public bool Once { get; private set; }
    public string? MarkerPath { get; private set; }

    public static GuardianOptions? Parse(string[] args)
    {
        var options = new GuardianOptions();
        var hasOwner = false;

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].ToLowerInvariant();
            string? Value() => i + 1 < args.Length ? args[++i] : null;

            switch (key)
            {
                case "--owner-pid":
                    if (int.TryParse(Value(), out var pid)) { options.OwnerProcessId = pid; hasOwner = true; }
                    break;
                case "--poll":
                    if (int.TryParse(Value(), out var poll)) options.PollMilliseconds = Math.Max(100, poll);
                    break;
                case "--once":
                    options.Once = true;
                    break;
                case "--marker":
                    options.MarkerPath = Value();
                    break;
            }
        }

        return hasOwner || options.Once ? options : null;
    }
}

internal sealed class GuardianReport
{
    public DateTimeOffset Timestamp { get; set; }
    public bool HadStaleSession { get; set; }
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<GuardianApplicationResult> Applications { get; set; } = new();
}

internal sealed class GuardianApplicationResult
{
    public string ApplicationIdentifier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? Reason { get; set; }
}
