using AudioFlow.Core;
using AudioFlow.Models;
using AudioFlow.Rules;

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
        Console.WriteLine("  audioflow apply                      Apply the rules to active sessions (Phase 5)");
        Console.WriteLine("  audioflow verify <device>            Measure real audio level per endpoint (Phase E)");
        Console.WriteLine("  audioflow route-pid <pid> <device>   Route one process (diagnostics)");
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

    public static int Apply()
    {
        using var devices = new AudioDeviceManager();
        var engine = new RuleEngine();
        var names = DeviceNames(devices);
        EnsureDefaultDevice(engine, devices);

        using var sessions = new AudioSessionManager();
        var routing = new AudioRoutingManager();
        var list = sessions.GetSessions();

        Banner("APPLY ROUTING");
        Console.WriteLine("Sets the persisted output device for each application.");
        Console.WriteLine("Windows applies it when the app (re)starts its audio stream.");
        Console.WriteLine();

        if (list.Count == 0)
        {
            Console.WriteLine("No active audio sessions.");
            Footer();
            return 0;
        }

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

            var targetName = DescribeDevice(resolution.OutputDeviceId, names);
            var result = routing.Apply(session.ProcessId, key, resolution.OutputDeviceId);

            if (result.Success)
            {
                applied++;
                var verified = result.Verified ? " [verified]" : " [unverified]";
                Console.WriteLine($"  [ok]   {key} (pid {session.ProcessId}) -> {targetName}{verified}");
            }
            else
            {
                failed++;
                Console.WriteLine($"  [fail] {key} (pid {session.ProcessId}) -> {targetName}: {result.Error}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Applied: {applied}   Failed: {failed}");
        Footer();
        return failed == 0 ? 0 : 1;
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

        var routing = new AudioRoutingManager();
        var result = routing.Apply(pid, $"pid:{pid}", id);

        Console.WriteLine($"pid {pid} -> {name}");
        Console.WriteLine($"success={result.Success} verified={result.Verified} status={result.Status}");
        if (result.Error is not null)
        {
            Console.WriteLine($"error: {result.Error}");
        }

        return result.Success ? 0 : 1;
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
