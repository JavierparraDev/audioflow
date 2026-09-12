using AudioFlow.Models;

namespace AudioFlow.Routing;

/// <summary>
/// Live, duplication-free backend. The application must render to a virtual
/// endpoint; AudioFlow captures that endpoint's loopback and renders it to the
/// chosen physical target, so the original physical device never plays it.
///
/// Requires a virtual audio endpoint to be installed. When none is present the
/// backend reports BLOCKED and cannot create a running pipeline.
/// </summary>
public sealed class VirtualEndpointRoutingBackend : IRoutingBackend
{
    private readonly VirtualAudioDeviceManager _virtualDevices;

    public VirtualEndpointRoutingBackend(VirtualAudioDeviceManager virtualDevices) =>
        _virtualDevices = virtualDevices;

    public RoutingBackendInfo Info
    {
        get
        {
            var endpoint = _virtualDevices.GetVirtualRenderEndpoint();
            return endpoint is null
                ? new RoutingBackendInfo(
                    "Virtual Endpoint",
                    RoutingBackendKind.VirtualEndpoint,
                    false,
                    "No virtual audio endpoint detected. Install a virtual audio cable (or AudioFlow's own endpoint) to enable live, duplication-free routing.")
                : new RoutingBackendInfo(
                    "Virtual Endpoint",
                    RoutingBackendKind.VirtualEndpoint,
                    true,
                    $"Virtual endpoint: {endpoint.FriendlyName}");
        }
    }

    public IReadOnlyList<AudioEndpointInfo> GetTargets() =>
        _virtualDevices.GetPhysicalEndpoints().Where(e => e.IsAvailable).ToList();

    public IRoutingPipeline CreatePipeline(ApplicationIdentity identity, uint processId, string targetDeviceId) =>
        new VirtualEndpointPipeline(_virtualDevices, identity.Key, processId, targetDeviceId);
}

internal sealed class VirtualEndpointPipeline : IRoutingPipeline
{
    private readonly VirtualAudioDeviceManager _virtualDevices;
    private readonly EndpointLoopbackSource _source = new();
    private WasapiEndpointRenderer? _renderer;

    public VirtualEndpointPipeline(
        VirtualAudioDeviceManager virtualDevices,
        string applicationIdentifier,
        uint processId,
        string targetDeviceId)
    {
        _virtualDevices = virtualDevices;
        ApplicationIdentifier = applicationIdentifier;
        ProcessId = processId;
        TargetDeviceId = targetDeviceId;
    }

    public string ApplicationIdentifier { get; }
    public uint ProcessId { get; }
    public string TargetDeviceId { get; private set; }
    public PipelineState State { get; private set; } = PipelineState.Created;
    public PipelineMetrics Metrics { get; } = new();

    public event EventHandler<PipelineState>? StateChanged;

    public bool Start()
    {
        var endpoint = _virtualDevices.GetVirtualRenderEndpoint();
        if (endpoint is null)
        {
            SetState(PipelineState.Blocked);
            return false;
        }

        SetState(PipelineState.Starting);

        if (!_source.Start(endpoint.DeviceId) || _source.Format is null)
        {
            SetState(PipelineState.Failed);
            return false;
        }

        _renderer = new WasapiEndpointRenderer(TargetDeviceId, _source.Format);
        if (!_renderer.Start())
        {
            _source.Stop();
            SetState(PipelineState.Failed);
            return false;
        }

        _source.DataAvailable += OnData;
        SetState(PipelineState.Running);
        return true;
    }

    private void OnData(object? sender, EndpointAudioEventArgs e) => _renderer?.AddSamples(e.Buffer, e.Count);

    public void Stop()
    {
        SetState(PipelineState.Stopping);
        _source.DataAvailable -= OnData;
        _source.Stop();
        _renderer?.Stop();
        SetState(PipelineState.Stopped);
    }

    public bool MoveTo(string targetDeviceId)
    {
        TargetDeviceId = targetDeviceId;
        _renderer?.Stop();
        _renderer = _source.Format is null ? null : new WasapiEndpointRenderer(TargetDeviceId, _source.Format);
        if (_renderer is null || !_renderer.Start())
        {
            SetState(PipelineState.Failed);
            return false;
        }

        SetState(PipelineState.Running);
        return true;
    }

    private void SetState(PipelineState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _source.DataAvailable -= OnData;
        _source.Dispose();
        _renderer?.Dispose();
        _renderer = null;
    }
}
