using AudioFlow.Core;

namespace AudioFlow.Routing;

/// <summary>
/// Holds the available routing backends and selects the preferred one:
/// Virtual Endpoint when available (live, no duplication), otherwise Policy
/// Endpoint (no duplication, applies on stream restart).
/// </summary>
public sealed class RoutingBackendRegistry : IDisposable
{
    private readonly AudioDeviceManager? _devices;
    private readonly VirtualAudioDeviceManager? _virtualDevices;

    /// <summary>Test/DI constructor with explicit backends.</summary>
    public RoutingBackendRegistry(IReadOnlyList<IRoutingBackend> backends) => Backends = backends;

    public RoutingBackendRegistry(AudioDeviceManager devices, VirtualAudioDeviceManager virtualDevices)
    {
        _devices = devices;
        _virtualDevices = virtualDevices;
        Backends = new IRoutingBackend[]
        {
            new VirtualEndpointRoutingBackend(virtualDevices),
            new PolicyEndpointRoutingBackend(devices)
        };
    }

    public IReadOnlyList<IRoutingBackend> Backends { get; }

    public static RoutingBackendRegistry CreateDefault() =>
        new(new AudioDeviceManager(), new VirtualAudioDeviceManager());

    public IRoutingBackend? SelectPreferred() =>
        Backends.FirstOrDefault(b => b.Info.Kind == RoutingBackendKind.VirtualEndpoint && b.Info.IsAvailable)
        ?? Backends.FirstOrDefault(b => b.Info.Kind == RoutingBackendKind.PolicyEndpoint && b.Info.IsAvailable);

    public void Dispose()
    {
        _devices?.Dispose();
        _virtualDevices?.Dispose();
    }
}
