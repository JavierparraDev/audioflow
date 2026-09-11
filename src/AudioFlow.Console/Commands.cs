using AudioFlow.Core;
using AudioFlow.Models;

namespace AudioFlow.ConsoleApp;

internal static class Commands
{
    private const string Title = "AUDIOFLOW";

    public static int Help()
    {
        Console.WriteLine("AudioFlow - control where each application plays audio");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  audioflow devices            List output devices (Phase 1)");
        Console.WriteLine("  audioflow sessions           List active audio sessions (Phase 2A)");
        Console.WriteLine("  audioflow monitor            Watch sessions in real time (Phase 2A)");
        Console.WriteLine("  audioflow help               Show this help");
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
        using var devices = new AudioDeviceManager();
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
            return "\U0001F3B5"; // musical note
        }
        if (name.Contains("discord"))
        {
            return "\U0001F4AC"; // speech balloon
        }
        if (name.Contains("chrome") || name.Contains("msedge") || name.Contains("firefox") || name.Contains("brave"))
        {
            return "\U0001F310"; // globe
        }
        return "\U0001F50A"; // speaker
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
