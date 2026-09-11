using AudioFlow.Core;
using AudioFlow.Core.Logging;
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
            Console.WriteLine($"  Name:");
            Console.WriteLine($"  {device.FriendlyName}");
            Console.WriteLine();
            Console.WriteLine($"  Device ID:");
            Console.WriteLine($"  {device.Id}");
            Console.WriteLine();
            Console.WriteLine($"  Status:");
            Console.WriteLine($"  {device.State}");
            if (!string.IsNullOrWhiteSpace(device.DeviceFriendlyName))
            {
                Console.WriteLine();
                Console.WriteLine($"  Adapter:");
                Console.WriteLine($"  {device.DeviceFriendlyName}");
            }

            Console.WriteLine();
            Console.WriteLine(new string('-', 41));
            Console.WriteLine();
        }

        Console.WriteLine("Default Device:");
        Console.WriteLine();
        Console.WriteLine(defaultDevice is null ? "None" : $"  {defaultDevice.FriendlyName}");
        Footer();
        return 0;
    }

    private static void Banner(string section)
    {
        Console.WriteLine("=================================");
        Console.WriteLine(Title);
        Console.WriteLine(section);
        Console.WriteLine("=================================");
        Console.WriteLine();
    }

    private static void Footer()
    {
        Console.WriteLine("=================================");
    }
}
