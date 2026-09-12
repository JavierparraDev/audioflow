using AudioFlow.Routing;
using Xunit;
using Xunit.Abstractions;

namespace AudioFlow.Windows.Tests;

public class RoutingBackendIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public RoutingBackendIntegrationTests(ITestOutputHelper output) => _output = output;

    [WindowsFact]
    public void VirtualAudioDeviceManager_EnumeratesAndClassifies()
    {
        using var manager = new VirtualAudioDeviceManager();

        var endpoints = manager.GetEndpoints();

        _output.WriteLine($"Endpoints: {endpoints.Count}");
        foreach (var endpoint in endpoints)
        {
            _output.WriteLine($"- {endpoint.FriendlyName} virtual={endpoint.IsVirtual} state={endpoint.State} default={endpoint.IsDefault}");
        }

        Assert.NotNull(endpoints);
    }

    [WindowsFact]
    public void Registry_SelectsAPreferredBackend()
    {
        using var registry = RoutingBackendRegistry.CreateDefault();

        foreach (var backend in registry.Backends)
        {
            _output.WriteLine($"{backend.Info.Name}: {backend.Info.Status} - {backend.Info.Reason}");
        }

        var preferred = registry.SelectPreferred();

        Assert.NotNull(preferred);
        Assert.True(preferred!.Info.IsAvailable);
    }

    [WindowsFact]
    public void PolicyBackend_ReturnsPhysicalTargets()
    {
        using var devices = new AudioFlow.Core.AudioDeviceManager();
        var backend = new PolicyEndpointRoutingBackend(devices);

        var targets = backend.GetTargets();

        _output.WriteLine($"Targets: {targets.Count}");
        Assert.True(backend.Info.IsAvailable);
    }
}
