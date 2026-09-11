using System.Diagnostics;
using AudioFlow.Applications;
using AudioFlow.Core;
using AudioFlow.Models;
using Xunit;
using Xunit.Abstractions;

namespace AudioFlow.Windows.Tests;

public class AudioDeviceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public AudioDeviceIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void EnumerateOutputDevices_ReturnsWellFormedDevices()
    {
        using var manager = new AudioDeviceManager();

        var devices = manager.GetOutputDevices(includeInactive: true);

        _output.WriteLine($"Detected {devices.Count} output endpoint(s).");
        foreach (var device in devices)
        {
            _output.WriteLine($"- {device.FriendlyName} | {device.State} | default={device.IsDefault}");
            _output.WriteLine($"    id: {device.Id}");

            Assert.False(string.IsNullOrWhiteSpace(device.Id));
            Assert.False(string.IsNullOrWhiteSpace(device.FriendlyName));
        }
    }

    [WindowsFact]
    public void GetDefaultOutputDevice_ReturnsActiveEndpoint_WhenAvailable()
    {
        using var manager = new AudioDeviceManager();

        var active = manager.GetOutputDevices(includeInactive: false);
        if (active.Count == 0)
        {
            _output.WriteLine("No active render endpoint on this machine.");
            return;
        }

        var defaultDevice = manager.GetDefaultOutputDevice();

        Assert.NotNull(defaultDevice);
        _output.WriteLine($"Default: {defaultDevice!.FriendlyName}");
    }
}

public class AudioSessionIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public AudioSessionIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void EnumerateSessions_ReturnsWellFormedSessions()
    {
        using var manager = new AudioSessionManager();

        var sessions = manager.GetSessions(includeInactive: false);

        _output.WriteLine($"Detected {sessions.Count} active session(s).");
        foreach (var session in sessions)
        {
            _output.WriteLine(
                $"- {session.ProcessName} (pid {session.ProcessId}) [{session.State}] " +
                $"vol={session.Volume:0.00} peak={session.PeakValue:0.000} " +
                $"device={session.DeviceName} app={session.ApplicationKey}");

            Assert.True(session.ProcessId > 0);
            Assert.False(string.IsNullOrWhiteSpace(session.DeviceId));
            Assert.NotEqual(AudioSessionStateKind.Unknown, session.State);
            Assert.InRange(session.Volume, 0f, 1f);
        }
    }

    [WindowsFact]
    public void SessionMonitor_StartStop_DoesNotThrow()
    {
        using var devices = new AudioDeviceManager();
        using var monitor = new AudioSessionMonitor(devices, interval: TimeSpan.FromMilliseconds(300));

        var started = new List<string>();
        monitor.SessionStarted += (_, session) =>
        {
            lock (started)
            {
                started.Add(session.ProcessName ?? "?");
            }
        };

        monitor.Start();
        Thread.Sleep(1500);
        monitor.Stop();

        _output.WriteLine($"Observed {started.Count} session start event(s).");
    }
}

public class ApplicationIdentityIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public ApplicationIdentityIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void CurrentProcess_IsIdentifiedWithStableKey()
    {
        var identifier = new ApplicationIdentifier();
        using var current = Process.GetCurrentProcess();

        var identity = identifier.Identify((uint)current.Id);

        _output.WriteLine($"Key={identity.Key} kind={identity.Kind} aumid={identity.Aumid}");
        Assert.False(string.IsNullOrWhiteSpace(identity.Key));
    }
}
