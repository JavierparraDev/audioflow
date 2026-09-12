using AudioFlow.Models;
using AudioFlow.Routing;
using Xunit;

namespace AudioFlow.Tests;

public class VirtualDeviceDetectionTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)", true)]
    [InlineData("VoiceMeeter Input", true)]
    [InlineData("AudioFlow Virtual Output", true)]
    [InlineData("Steam Streaming Speakers", true)]
    [InlineData("Speaker (Realtek(R) Audio)", false)]
    [InlineData("Auriculares (HyperX Cloud III)", false)]
    [InlineData("", false)]
    public void LooksVirtual_ClassifiesByHeuristic(string name, bool expected)
    {
        Assert.Equal(expected, VirtualAudioDeviceManager.LooksVirtual(name));
    }
}

public class RoutingBackendSelectionTests
{
    private sealed class FakeBackend : IRoutingBackend
    {
        public FakeBackend(RoutingBackendKind kind, bool available) =>
            Info = new RoutingBackendInfo(kind.ToString(), kind, available, null);

        public RoutingBackendInfo Info { get; }

        public IReadOnlyList<AudioEndpointInfo> GetTargets() => Array.Empty<AudioEndpointInfo>();

        public IRoutingPipeline CreatePipeline(ApplicationIdentity identity, uint processId, string targetDeviceId) =>
            new FakePipeline();
    }

    private sealed class FakePipeline : IRoutingPipeline
    {
        public string ApplicationIdentifier => "fake";
        public uint ProcessId => 0;
        public string TargetDeviceId => "target";
        public PipelineState State { get; private set; } = PipelineState.Created;
        public PipelineMetrics Metrics { get; } = new();
        public event EventHandler<PipelineState>? StateChanged;
        public bool Start() { State = PipelineState.Running; StateChanged?.Invoke(this, State); return true; }
        public void Stop() { State = PipelineState.Stopped; }
        public bool MoveTo(string targetDeviceId) => true;
        public void Dispose() { }
    }

    [Fact]
    public void SelectPreferred_PrefersVirtualEndpointWhenAvailable()
    {
        using var registry = new RoutingBackendRegistry(new IRoutingBackend[]
        {
            new FakeBackend(RoutingBackendKind.PolicyEndpoint, true),
            new FakeBackend(RoutingBackendKind.VirtualEndpoint, true)
        });

        Assert.Equal(RoutingBackendKind.VirtualEndpoint, registry.SelectPreferred()!.Info.Kind);
    }

    [Fact]
    public void SelectPreferred_FallsBackToPolicyEndpoint()
    {
        using var registry = new RoutingBackendRegistry(new IRoutingBackend[]
        {
            new FakeBackend(RoutingBackendKind.PolicyEndpoint, true),
            new FakeBackend(RoutingBackendKind.VirtualEndpoint, false)
        });

        Assert.Equal(RoutingBackendKind.PolicyEndpoint, registry.SelectPreferred()!.Info.Kind);
    }

    [Fact]
    public void SelectPreferred_ReturnsNullWhenNoneAvailable()
    {
        using var registry = new RoutingBackendRegistry(new IRoutingBackend[]
        {
            new FakeBackend(RoutingBackendKind.PolicyEndpoint, false),
            new FakeBackend(RoutingBackendKind.VirtualEndpoint, false)
        });

        Assert.Null(registry.SelectPreferred());
    }
}

public class VirtualEndpointBackendTests
{
    [Fact]
    public void Info_IsBlocked_WhenNoVirtualEndpointExists()
    {
        using var virtualDevices = new VirtualAudioDeviceManager();
        var backend = new VirtualEndpointRoutingBackend(virtualDevices);

        // On Linux there is no audio subsystem, so no virtual endpoint.
        Assert.False(backend.Info.IsAvailable);
        Assert.Equal("BLOCKED", backend.Info.Status);
        Assert.False(string.IsNullOrWhiteSpace(backend.Info.Reason));
    }

    [Fact]
    public void Pipeline_IsBlocked_WhenNoVirtualEndpointExists()
    {
        using var virtualDevices = new VirtualAudioDeviceManager();
        var backend = new VirtualEndpointRoutingBackend(virtualDevices);
        using var pipeline = backend.CreatePipeline(
            new ApplicationIdentity { Key = "exe:test.exe" }, 1234, "target");

        Assert.False(pipeline.Start());
        Assert.Equal(PipelineState.Blocked, pipeline.State);
    }
}
