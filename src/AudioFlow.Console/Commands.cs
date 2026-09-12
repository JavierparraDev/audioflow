using AudioFlow.Configuration;
using AudioFlow.Core;
using AudioFlow.Models;
using AudioFlow.Rules;
using AudioFlow.Updates;
using AudioFlow.Applications;
using AudioFlow.Session;
using System.Diagnostics;

namespace AudioFlow.ConsoleApp;

internal static class Commands
{
    private const string Title = "AUDIOFLOW";

    public static int Help()
    {
        Console.WriteLine("AudioFlow - control where each application plays audio");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  audioflow devices                    List output devices (Phase 1)");
        Console.WriteLine("  audioflow sessions                   List active audio sessions (Phase 2A)");
        Console.WriteLine("  audioflow monitor                    Watch sessions in real time (Phase 2A)");
        Console.WriteLine("  audioflow rules                      Show the saved rules (Phase 4)");
        Console.WriteLine("  audioflow plan                       Resolve active sessions to devices (Phase 4)");
        Console.WriteLine("  audioflow apply [--duration N]       Apply rules for a temporary session (restores on exit)");
        Console.WriteLine("  audioflow session                    Show the AudioFlow session state (ACTIVE/INACTIVE/STALE)");
        Console.WriteLine("  audioflow restore                    Restore Windows audio changed by AudioFlow");
        Console.WriteLine("  audioflow verify <device>            Measure real audio level per endpoint (Phase E)");
        Console.WriteLine("  audioflow route-pid <pid> <device>   Route one process (diagnostics)");
        Console.WriteLine("  audioflow loopback-probe <pid>       Probe Windows Process Loopback (experimental)");
        Console.WriteLine("  audioflow loopback-capture <pid> <s> Capture a process's audio and show metrics");
        Console.WriteLine("  audioflow mute-pid <pid> <on|off>    Mute/unmute a process's audio sessions");
        Console.WriteLine("  audioflow live-route <pid> <dev> <s> Capture a process and render it to a device");
        Console.WriteLine("  audioflow version                    Show the application version");
        Console.WriteLine("  audioflow update [--check]           Check GitHub Releases for updates");
        Console.WriteLine("  audioflow set-default <device>       Set the default output device");
        Console.WriteLine("  audioflow set-rule <app> <device>    Route an application to a device");
        Console.WriteLine("  audioflow remove-rule <app>          Delete an application rule");
        Console.WriteLine("  audioflow lock <device> <fallback> [app...]  Enable Audio Lock (fallback device required)");
        Console.WriteLine("  audioflow unlock                     Disable Audio Lock");
        Console.WriteLine("  audioflow help                       Show this help");
        Console.WriteLine();
        Console.WriteLine("  <device> is a number from 'audioflow devices', a device id, or a name substring.");
        Console.WriteLine("  <app> is the Application ID shown by 'audioflow sessions' (e.g. exe:spotify.exe).");
        Console.WriteLine();
        return 0;
    }

    public static int Devices()
    {
        using var devices = new AudioDeviceManager();
        var outputs = devices.GetOutputDevices();
        var defaultDevice = devices.GetDefaultOutputDevice();

        Banner("OUTPUT DEVICES");

        if (outputs.Count == 0)
        {
            Console.WriteLine("No output devices were found.");
            Footer();
            return 0;
        }

        for (var i = 0; i < outputs.Count; i++)
        {
            var device = outputs[i];
            Console.WriteLine($"{i + 1}.");
            Console.WriteLine();
            Console.WriteLine("  Name:");
            Console.WriteLine($"  {device.FriendlyName}");
            Console.WriteLine();
            Console.WriteLine("  Device ID:");
            Console.WriteLine($"  {device.Id}");
            Console.WriteLine();
            Console.WriteLine("  Status:");
            Console.WriteLine($"  {device.State}");
            if (!string.IsNullOrWhiteSpace(device.DeviceFriendlyName))
            {
                Console.WriteLine();
                Console.WriteLine("  Adapter:");
                Console.WriteLine($"  {device.DeviceFriendlyName}");
            }

            Console.WriteLine();
            Console.WriteLine(new string('-', 41));
            Console.WriteLine();
        }

        Console.WriteLine("Default Device:");
        Console.WriteLine();
        Console.WriteLine(defaultDevice is null ? "  None" : $"  {defaultDevice.FriendlyName}");
        Footer();
        return 0;
    }

    public static int Sessions()
    {
        using var sessions = new AudioSessionManager();
        var list = sessions.GetSessions();

        Banner("ACTIVE AUDIO SESSIONS");

        if (list.Count == 0)
        {
            Console.WriteLine("No active audio sessions.");
            Console.WriteLine("Start playing audio in any application and run the command again.");
            Footer();
            return 0;
        }

        foreach (var session in list)
        {
            PrintSession(session);
        }

        Footer();
        return 0;
    }

    public static int Monitor()
    {
        using var devices = new AudioDeviceManager();
        using var monitor = new AudioSessionMonitor(devices);

        Banner("LIVE AUDIO SESSIONS");
        Console.WriteLine("Monitoring audio sessions. Press Ctrl+C to stop.");
        Console.WriteLine();

        monitor.SessionStarted += (_, session) =>
            Console.WriteLine($"+ [{DateTimeOffset.Now:HH:mm:ss}] {Describe(session)}");
        monitor.SessionEnded += (_, session) =>
            Console.WriteLine($"- [{DateTimeOffset.Now:HH:mm:ss}] {Describe(session)}");

        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };

        monitor.Start();
        stop.Wait();

        Console.WriteLine();
        Console.WriteLine("Stopped.");
        Footer();
        return 0;
    }

    public static int Rules()
    {
        using var devices = new AudioDeviceManager();
        var engine = new RuleEngine();
        var names = DeviceNames(devices);

        EnsureDefaultDevice(engine, devices);

        Banner("RULES");

        Console.WriteLine("Default output:");
        Console.WriteLine($"  {DescribeDevice(engine.Rules.DefaultOutputDeviceId, names)}");
        Console.WriteLine();

        Console.WriteLine("Application rules:");
        if (engine.Rules.Rules.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var rule in engine.Rules.Rules)
            {
                var state = rule.Enabled ? "enabled" : "disabled";
                Console.WriteLine($"  {rule.ApplicationName} [{rule.ApplicationIdentifier}] -> " +
                                  $"{DescribeDevice(rule.OutputDeviceId, names)} ({state})");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Audio Lock:");
        if (engine.Rules.AudioLockEnabled)
        {
            Console.WriteLine($"  ON - locked device: {DescribeDevice(engine.Rules.AudioLockDeviceId, names)}");
            Console.WriteLine($"  Fallback: {DescribeDevice(engine.Rules.AudioLockFallbackDeviceId, names)}");
            Console.WriteLine($"  Allowed: {string.Join(", ", engine.Rules.AudioLockAllowedApplications)}");
        }
        else
        {
            Console.WriteLine("  OFF");
        }

        Console.WriteLine();
        Console.WriteLine($"File: {engine.RulesFilePath}");
        Footer();
        return 0;
    }

    public static int Plan()
    {
        using var devices = new AudioDeviceManager();
        var engine = new RuleEngine();
        var names = DeviceNames(devices);

        EnsureDefaultDevice(engine, devices);

        using var sessions = new AudioSessionManager();
        var list = sessions.GetSessions();

        Banner("ROUTING PLAN");

        Console.WriteLine($"Default output: {DescribeDevice(engine.Rules.DefaultOutputDeviceId, names)}");
        Console.WriteLine();

        if (list.Count == 0)
        {
            Console.WriteLine("No active audio sessions.");
            Footer();
            return 0;
        }

        foreach (var session in list)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = engine.Resolve(key, session.ApplicationPathHash);
            var marker = resolution.HasExplicitRule ? "RULE" : "DEFAULT";
            if (resolution.BlockedByAudioLock)
            {
                marker = "LOCK";
            }

            Console.WriteLine($"  [{marker}] {session.ApplicationName ?? session.ProcessName ?? "?"} ({key})");
            Console.WriteLine($"          current : {session.DeviceName ?? "Unknown"}");
            Console.WriteLine($"          target  : {DescribeDevice(resolution.OutputDeviceId, names)}  ({resolution.Reason})");
            Console.WriteLine();
        }

        Footer();
        return 0;
    }

    public static int Apply(string[] args)
    {
        var duration = 0;
        var durationIndex = Array.FindIndex(args, a => a.Equals("--duration", StringComparison.OrdinalIgnoreCase));
        if (durationIndex >= 0 && durationIndex + 1 < args.Length)
        {
            int.TryParse(args[durationIndex + 1], out duration);
        }

        var noWait = args.Any(a => a.Equals("--no-wait", StringComparison.OrdinalIgnoreCase));

        using var devices = new AudioDeviceManager();
        var engine = new RuleEngine();
        var names = DeviceNames(devices);
        EnsureDefaultDevice(engine, devices);

        using var backend = new WindowsAudioRoutingBackend();
        var sessionManager = new AudioFlowSessionManager(backend);

        Banner("APPLY ROUTING (TEMPORARY SESSION)");
        Console.WriteLine("Rules are applied for this session only.");
        Console.WriteLine("Windows audio is restored when this command exits.");
        Console.WriteLine();

        if (!sessionManager.StartSession())
        {
            Console.WriteLine("Could not start a session.");
            Footer();
            return 2;
        }

        using var sessions = new AudioSessionManager();
        var list = sessions.GetSessions();
        var applied = 0;
        var failed = 0;

        foreach (var session in list)
        {
            var key = session.ApplicationKey ?? $"pid:{session.ProcessId}";
            var resolution = engine.Resolve(key, session.ApplicationPathHash);

            if (string.IsNullOrWhiteSpace(resolution.OutputDeviceId))
            {
                Console.WriteLine($"  [skip] {key}: no target device configured");
                continue;
            }

            var identity = new ApplicationIdentity
            {
                Key = key,
                Aumid = session.Aumid,
                ExecutablePath = session.ProcessPath,
                PathHash = session.ApplicationPathHash,
                ProcessName = session.ProcessName,
                DisplayName = session.ApplicationName
            };

            var targetName = DescribeDevice(resolution.OutputDeviceId, names);
            if (sessionManager.ApplyRoute(identity, session.ProcessId, resolution.OutputDeviceId, out var error))
            {
                applied++;
                Console.WriteLine($"  [ok]   {key} (pid {session.ProcessId}) -> {targetName}");
            }
            else
            {
                failed++;
                Console.WriteLine($"  [fail] {key} (pid {session.ProcessId}) -> {targetName}: {error}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Applied: {applied}   Failed: {failed}");

        if (noWait)
        {
            Console.WriteLine("Restoring Windows audio (--no-wait)...");
            PrintRestoreReport(sessionManager.EndSession());
            Footer();
            return failed == 0 ? 0 : 1;
        }

        Console.WriteLine();
        Console.WriteLine("SESSION ACTIVE. Press Ctrl+C to stop and restore Windows audio.");
        var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };

        if (duration > 0)
        {
            stop.Wait(TimeSpan.FromSeconds(duration));
        }
        else
        {
            stop.Wait();
        }

        Console.WriteLine();
        Console.WriteLine("Restoring Windows audio...");
        var report = sessionManager.EndSession();
        PrintRestoreReport(report);
        Footer();
        return report.Success ? 0 : 1;
    }

    public static int Session()
    {
        var marker = new SessionMarker();
        var snapshot = marker.Read();

        if (snapshot is null)
        {
            Console.WriteLine("INACTIVE");
            Console.WriteLine("Windows audio is not being modified by AudioFlow.");
            return 0;
        }

        var active = snapshot.OwnerProcessId != 0 && IsProcessAlive(snapshot.OwnerProcessId);
        Console.WriteLine(active ? "ACTIVE" : "STALE");
        Console.WriteLine($"  sessionId : {snapshot.SessionId}");
        Console.WriteLine($"  createdAt : {snapshot.CreatedAt:u}");
        Console.WriteLine($"  ownerPid  : {snapshot.OwnerProcessId} ({(active ? "running" : "not running")})");
        Console.WriteLine($"  apps      : {snapshot.Applications.Count}");
        return 0;
    }

    public static int Restore()
    {
        using var backend = new WindowsAudioRoutingBackend();
        var manager = new AudioFlowSessionManager(backend);
        var recovery = manager.RecoverIfNeeded();

        if (!recovery.HadStaleSession)
        {
            Console.WriteLine("No AudioFlow session to restore. Windows audio is untouched.");
            Console.WriteLine("RESTORE SUCCESS");
            return 0;
        }

        PrintRestoreReport(recovery.Restore);
        return recovery.Restore.Success ? 0 : 1;
    }

    private static void PrintRestoreReport(AudioFlow.Session.RestoreReport report)
    {
        foreach (var result in report.Results)
        {
            var detail = string.IsNullOrWhiteSpace(result.Reason) ? string.Empty : $" ({result.Reason})";
            Console.WriteLine($"  [{result.Status}] {result.ApplicationIdentifier} -> {result.DeviceId ?? "-"}{detail}");
        }

        Console.WriteLine(report.Success ? "RESTORE SUCCESS" : "RESTORE FAILED");
    }

    private static bool IsProcessAlive(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public static int Verify(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: audioflow verify <device>");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var (id, name) = ResolveDeviceArg(args[0], devices.GetOutputDevices());
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[0]}");
            return 1;
        }

        Banner("ROUTING VERIFICATION");
        Console.WriteLine($"Expected endpoint: {name}");
        Console.WriteLine("Measuring the real audio level of every endpoint for 3 seconds...");
        Console.WriteLine("Play audio in the target application while this runs.");
        Console.WriteLine();

        using var verifier = new AudioOutputVerifier();
        var result = verifier.Verify(id, TimeSpan.FromSeconds(3));

        foreach (var peak in result.Peaks)
        {
            var mark = string.Equals(peak.DeviceId, id, StringComparison.OrdinalIgnoreCase)
                ? "   <== expected"
                : string.Empty;
            Console.WriteLine($"  {peak.Peak:0.0000}  {peak.DeviceName}{mark}");
        }

        Console.WriteLine();
        Console.WriteLine(result.Verified
            ? "RESULT: VERIFIED - signal present on the expected endpoint only."
            : result.SignalOnExpected
                ? "RESULT: PARTIAL - signal on the expected endpoint, but also on another device."
                : "RESULT: NOT VERIFIED - no signal measured on the expected endpoint.");

        Footer();
        return 0;
    }

    public static int RoutePid(string[] args)
    {
        if (args.Length < 2 || !uint.TryParse(args[0], out var pid))
        {
            Console.WriteLine("Usage: audioflow route-pid <pid> <device>");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var (id, name) = ResolveDeviceArg(args[1], devices.GetOutputDevices());
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[1]}");
            return 1;
        }

        using var backend = new WindowsAudioRoutingBackend();
        var manager = new AudioFlowSessionManager(backend);
        var identity = new ApplicationIdentifier().Identify(pid);

        Console.WriteLine($"pid {pid} -> {name} (temporary session)");
        if (!manager.ApplyRoute(identity, pid, id, out var error))
        {
            Console.WriteLine($"route failed: {error}");
            manager.EndSession();
            return 1;
        }

        Console.WriteLine("Route applied. Restoring immediately (diagnostic only)...");
        var report = manager.EndSession();
        PrintRestoreReport(report);
        return report.Success ? 0 : 1;
    }

    public static int LoopbackProbe(string[] args)
    {
        if (args.Length < 1 || !uint.TryParse(args[0], out var pid))
        {
            Console.WriteLine("Usage: audioflow loopback-probe <pid>");
            return 1;
        }

        Banner("PROCESS LOOPBACK (EXPERIMENTAL)");
        Console.WriteLine($"Supported on this OS: {AudioFlow.ProcessLoopback.ProcessLoopbackProbe.IsSupported}");
        Console.WriteLine($"Target process: {pid}");
        Console.WriteLine();

        var result = AudioFlow.ProcessLoopback.ProcessLoopbackProbe.Probe(pid);

        Console.WriteLine($"supported: {result.Supported}");
        Console.WriteLine($"activated: {result.Activated}");
        Console.WriteLine($"message  : {result.Message}");
        Console.WriteLine();
        Console.WriteLine("Note: this only proves the capture interface can be activated;");
        Console.WriteLine("re-rendering captured audio is not implemented yet (experimental).");
        Footer();
        return result.Activated ? 0 : 2;
    }

    public static int Version()
    {
        Console.WriteLine($"AudioFlow {AudioFlowVersion.Current}");
        return 0;
    }

    public static async Task<int> Update(string[] args)
    {
        var checkOnly = args.Any(a => a.Equals("--check", StringComparison.OrdinalIgnoreCase));

        using var source = new GitHubReleaseSource();
        using var service = new UpdateService(
            source,
            AppVersion.Parse(AudioFlowVersion.Current),
            UpdateChannel.Stable);

        Console.WriteLine($"Current version: {service.Current}");

        var result = await service.CheckAsync(force: true);

        if (result.Latest is not null)
        {
            Console.WriteLine($"Latest version: {result.Latest}");
        }

        Console.WriteLine($"Status: {DescribeUpdateStatus(result.Status)}");
        Console.WriteLine(result.Message);

        if (checkOnly)
        {
            return 0;
        }

        if (result.UpdateAvailable && result.Release is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Open the release page to download the update:");
            Console.WriteLine($"  {result.Release.HtmlUrl}");
            Console.WriteLine();
            Console.WriteLine("Or use the AudioFlow UI (Settings > Updates) to update automatically.");
        }

        return 0;
    }

    private static string DescribeUpdateStatus(UpdateStatus status) => status switch
    {
        UpdateStatus.UpToDate => "Up to date",
        UpdateStatus.UpdateAvailable => "Update available",
        UpdateStatus.NoReleaseFound => "No release published yet",
        UpdateStatus.Failed => "Unable to check for updates",
        _ => "Unknown"
    };

    public static int LoopbackCapture(string[] args)
    {
        if (args.Length < 2 || !uint.TryParse(args[0], out var pid) || !int.TryParse(args[1], out var seconds))
        {
            Console.WriteLine("Usage: audioflow loopback-capture <pid> <seconds>");
            return 1;
        }

        Banner("PROCESS LOOPBACK CAPTURE");

        using var capture = new AudioFlow.ProcessLoopback.ProcessLoopbackCapture();
        if (!capture.Start(pid))
        {
            Console.WriteLine($"Capture start failed: {capture.LastError}");
            Footer();
            return 2;
        }

        Console.WriteLine($"Capturing process {pid} for {seconds}s (16-bit PCM / 44100 / stereo)...");
        Console.WriteLine("Play audio in the target application now.");
        Console.WriteLine();

        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            var stats = capture.Statistics;
            Console.WriteLine(
                $"  peak={stats.LastPeak:0.0000}  rms={stats.LastRms:0.0000}  " +
                $"frames={stats.TotalFrames}  packets={stats.PacketCount}  silent={stats.SilentPackets}");
        }

        capture.Stop();

        var final = capture.Statistics;
        Console.WriteLine();
        Console.WriteLine($"Frames captured : {final.TotalFrames}");
        Console.WriteLine($"Packets         : {final.PacketCount}");
        Console.WriteLine($"Silent packets  : {final.SilentPackets}");
        Console.WriteLine($"Max peak        : {final.MaxPeak:0.0000}");
        Console.WriteLine();
        Console.WriteLine(final.MaxPeak > 0.001f
            ? "RESULT: AUDIO CAPTURED (peak above silence threshold)."
            : "RESULT: SILENCE (no audio captured).");
        Footer();
        return 0;
    }

    public static int MutePid(string[] args)
    {
        if (args.Length < 2 || !uint.TryParse(args[0], out var pid))
        {
            Console.WriteLine("Usage: audioflow mute-pid <pid> <on|off>");
            return 1;
        }

        var mute = args[1].Equals("on", StringComparison.OrdinalIgnoreCase)
                   || args[1].Equals("1", StringComparison.OrdinalIgnoreCase)
                   || args[1].Equals("true", StringComparison.OrdinalIgnoreCase);

        using var sessions = new AudioSessionManager();
        var changed = sessions.SetProcessMute(pid, mute);
        Console.WriteLine($"pid {pid} mute={mute}: {changed} session(s) changed.");
        return 0;
    }

    public static int LiveRoute(string[] args)
    {
        if (args.Length < 3 || !uint.TryParse(args[0], out var pid) || !int.TryParse(args[2], out var seconds))
        {
            Console.WriteLine("Usage: audioflow live-route <pid> <device> <seconds> [--mute]");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var (id, name) = ResolveDeviceArg(args[1], devices.GetOutputDevices());
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[1]}");
            return 1;
        }

        var muteOriginal = args.Any(a => a.Equals("--mute", StringComparison.OrdinalIgnoreCase));

        Banner("LIVE ROUTE");
        Console.WriteLine($"Process {pid} -> {name} for {seconds}s");
        Console.WriteLine();

        using var pipeline = new AudioFlow.LiveRouting.AudioPipeline($"pid:{pid}", pid, id);
        pipeline.StateChanged += (_, state) => Console.WriteLine($"  [state] {state}");

        if (!pipeline.Start())
        {
            Console.WriteLine($"Pipeline start failed: {pipeline.LastError}");
            Footer();
            return 2;
        }

        if (muteOriginal)
        {
            using var muter = new AudioSessionManager();
            muter.SetProcessMute(pid, true);
            Console.WriteLine("  original session muted (duplication suppression attempt)");
        }

        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            var c = pipeline.Capture;
            var r = pipeline.Render;
            Console.WriteLine(
                $"  capture peak={c.LastPeak:0.0000} rms={c.LastRms:0.0000} frames={c.TotalFrames} | " +
                $"render frames={r.FramesWritten} buffered={r.BufferedBytes}B underruns={r.Underruns}");
        }

        Console.WriteLine();
        Console.WriteLine($"Capture frames: {pipeline.Capture.TotalFrames}  Render frames: {pipeline.Render.FramesWritten}");
        Console.WriteLine($"Mix format    : {pipeline.MixFormat}");

        // Verify WHILE the pipeline is still running.
        Console.WriteLine();
        Console.WriteLine($"Verifying real audio level on '{name}' (3s, pipeline still running)...");
        using var verifier = new AudioOutputVerifier();
        var result = verifier.Verify(id, TimeSpan.FromSeconds(3));
        foreach (var peak in result.Peaks)
        {
            var mark = string.Equals(peak.DeviceId, id, StringComparison.OrdinalIgnoreCase) ? "   <== target" : string.Empty;
            Console.WriteLine($"  {peak.Peak:0.0000}  {peak.DeviceName}{mark}");
        }

        pipeline.Stop();

        if (muteOriginal)
        {
            using var muter = new AudioSessionManager();
            muter.SetProcessMute(pid, false);
        }

        Console.WriteLine();
        Console.WriteLine(result.SignalOnExpected
            ? "RESULT: target endpoint received audio."
            : "RESULT: target endpoint received NO audio.");
        Footer();
        return 0;
    }

    public static int SetDefault(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: audioflow set-default <device>");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var (id, name) = ResolveDeviceArg(args[0], devices.GetOutputDevices());
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[0]}");
            return 1;
        }

        var engine = new RuleEngine();
        engine.SetDefaultDevice(id);
        Console.WriteLine($"Default output set to: {name}");
        return 0;
    }

    public static int SetRule(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: audioflow set-rule <app> <device>");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var (id, name) = ResolveDeviceArg(args[1], devices.GetOutputDevices());
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[1]}");
            return 1;
        }

        var engine = new RuleEngine();
        var app = args[0];
        var rule = engine.SetRule(app, app, id);
        Console.WriteLine($"Rule set: {rule.ApplicationIdentifier} -> {name}");
        return 0;
    }

    public static int RemoveRule(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: audioflow remove-rule <app>");
            return 1;
        }

        var engine = new RuleEngine();
        Console.WriteLine(engine.RemoveRule(args[0])
            ? $"Rule removed: {args[0]}"
            : $"No rule found for: {args[0]}");
        return 0;
    }

    public static int Lock(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: audioflow lock <device> <fallback> [app...]");
            return 1;
        }

        using var devices = new AudioDeviceManager();
        var outputs = devices.GetOutputDevices();
        var (id, name) = ResolveDeviceArg(args[0], outputs);
        if (id is null)
        {
            Console.WriteLine($"Device not found: {args[0]}");
            return 1;
        }

        var (fallbackId, fallbackName) = ResolveDeviceArg(args[1], outputs);
        if (fallbackId is null)
        {
            Console.WriteLine($"Fallback device not found: {args[1]}");
            return 1;
        }

        var engine = new RuleEngine();
        var allowed = args.Skip(2).ToArray();
        engine.EnableAudioLock(id, fallbackId, allowed);
        Console.WriteLine($"Audio Lock ON for: {name}");
        Console.WriteLine($"Fallback: {fallbackName}");
        Console.WriteLine(allowed.Length == 0
            ? "Allowed applications: (none)"
            : $"Allowed applications: {string.Join(", ", allowed)}");
        return 0;
    }

    public static int Unlock()
    {
        var engine = new RuleEngine();
        engine.DisableAudioLock();
        Console.WriteLine("Audio Lock OFF.");
        return 0;
    }

    private static void EnsureDefaultDevice(RuleEngine engine, AudioDeviceManager devices)
    {
        if (!string.IsNullOrWhiteSpace(engine.Rules.DefaultOutputDeviceId))
        {
            return;
        }

        var systemDefault = devices.GetDefaultOutputDevice();
        if (systemDefault is not null)
        {
            engine.SetDefaultDevice(systemDefault.Id);
        }
    }

    private static Dictionary<string, string> DeviceNames(AudioDeviceManager devices)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices.GetOutputDevices())
        {
            map[device.Id] = device.FriendlyName;
        }

        return map;
    }

    private static string DescribeDevice(string? deviceId, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return "(not set)";
        }

        return names.TryGetValue(deviceId, out var name) ? name : deviceId;
    }

    private static (string? Id, string? Name) ResolveDeviceArg(string arg, IReadOnlyList<AudioDevice> devices)
    {
        if (int.TryParse(arg, out var index) && index >= 1 && index <= devices.Count)
        {
            return (devices[index - 1].Id, devices[index - 1].FriendlyName);
        }

        var byId = devices.FirstOrDefault(d => string.Equals(d.Id, arg, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return (byId.Id, byId.FriendlyName);
        }

        var byName = devices.FirstOrDefault(d =>
            d.FriendlyName.Contains(arg, StringComparison.OrdinalIgnoreCase));
        return byName is null ? (null, null) : (byName.Id, byName.FriendlyName);
    }

    private static void PrintSession(AudioSessionInfo session)
    {
        Console.WriteLine($"{IconFor(session.ProcessName)} {DisplayNameOf(session)}");
        Console.WriteLine();
        Console.WriteLine("  Process:");
        Console.WriteLine($"  {session.ProcessName ?? "Unknown"}");
        Console.WriteLine();
        Console.WriteLine("  Process ID:");
        Console.WriteLine($"  {session.ProcessId}");
        Console.WriteLine();
        Console.WriteLine("  Application ID:");
        Console.WriteLine($"  {session.ApplicationKey ?? "Unknown"}");
        Console.WriteLine();
        Console.WriteLine("  Session State:");
        Console.WriteLine($"  {session.State}");
        Console.WriteLine();
        Console.WriteLine("  Volume:");
        Console.WriteLine($"  {(int)Math.Round(session.Volume * 100)}%{(session.IsMuted ? " (muted)" : string.Empty)}");
        Console.WriteLine();
        Console.WriteLine("  Peak:");
        Console.WriteLine($"  {session.PeakValue:0.000}");
        Console.WriteLine();
        Console.WriteLine("  Device:");
        Console.WriteLine($"  {session.DeviceName ?? "Unknown / Not Available"}");
        Console.WriteLine();
        Console.WriteLine(new string('-', 41));
        Console.WriteLine();
    }

    private static string Describe(AudioSessionInfo session) =>
        $"{IconFor(session.ProcessName)} {session.ProcessName ?? "Unknown"} (pid {session.ProcessId}) " +
        $"-> {session.DeviceName ?? "Unknown"}  vol {(int)Math.Round(session.Volume * 100)}%";

    private static string DisplayNameOf(AudioSessionInfo session) =>
        !string.IsNullOrWhiteSpace(session.DisplayName) ? session.DisplayName! : session.ProcessName ?? "Unknown";

    private static string IconFor(string? processName)
    {
        var name = processName?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("spotify"))
        {
            return "\U0001F3B5";
        }
        if (name.Contains("discord"))
        {
            return "\U0001F4AC";
        }
        if (name.Contains("chrome") || name.Contains("msedge") || name.Contains("firefox") || name.Contains("brave"))
        {
            return "\U0001F310";
        }
        return "\U0001F50A";
    }

    private static void Banner(string section)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine(Title);
        Console.WriteLine(section);
        Console.WriteLine("=========================================");
        Console.WriteLine();
    }

    private static void Footer()
    {
        Console.WriteLine("=========================================");
    }
}
