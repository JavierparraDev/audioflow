using System.Diagnostics;
using AudioFlow.Core;
using AudioFlow.LiveRouting;
using AudioFlow.ProcessLoopback;
using Xunit;
using Xunit.Abstractions;

namespace AudioFlow.Windows.Tests;

public class ProcessLoopbackCaptureIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public ProcessLoopbackCaptureIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void Capture_CurrentProcess_ActivatesAndStops()
    {
        if (!ProcessLoopbackProbe.IsSupported)
        {
            _output.WriteLine("Process loopback not supported on this build.");
            return;
        }

        using var capture = new ProcessLoopbackCapture();
        using var current = Process.GetCurrentProcess();

        var started = capture.Start((uint)current.Id);

        _output.WriteLine($"Started={started} Error={capture.LastError}");
        Assert.True(started, capture.LastError);

        Thread.Sleep(500);
        capture.Stop();

        Assert.False(capture.IsRunning);
    }
}

public class AudioPipelineIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public AudioPipelineIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void Pipeline_StartsAndStops_OnRealDevice()
    {
        using var devices = new AudioDeviceManager();
        var active = devices.GetOutputDevices(includeInactive: false);
        if (active.Count == 0)
        {
            _output.WriteLine("No active render endpoint.");
            return;
        }

        using var current = Process.GetCurrentProcess();
        using var pipeline = new AudioPipeline("test:current", (uint)current.Id, active[0].Id);

        var started = pipeline.Start();
        _output.WriteLine($"Started={started} State={pipeline.State} Error={pipeline.LastError} Mix={pipeline.MixFormat}");

        if (started)
        {
            Assert.Equal(AudioPipelineState.Running, pipeline.State);
        }

        pipeline.Stop();
        Assert.Equal(AudioPipelineState.Stopped, pipeline.State);
    }
}
