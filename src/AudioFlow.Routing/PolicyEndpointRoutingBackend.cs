using AudioFlow.Core;
using AudioFlow.Models;

namespace AudioFlow.Routing;

/// <summary>
/// Backend that routes by setting the application's persisted output endpoint
/// (the mechanism used by Windows Settings). No capture, so there is no
/// duplication; the application renders directly to the target. Limitation: it
/// applies when the application recreates its audio stream (not live).
/// </summary>
public sealed class PolicyEndpointRoutingBackend : IRoutingBackend
{
    private readonly AudioDeviceManager _devices;
    private readonly AudioRoutingManager _routing = new();

    public PolicyEndpointRoutingBackend(AudioDeviceManager devices) => _devices = devices;

    public RoutingBackendInfo Info => new(
        "Policy Endpoint",
        RoutingBackendKind.PolicyEndpoint,
        OperatingSystem.IsWindows(),
        OperatingSystem.IsWindows()
            ? "Applies on stream restart (not live). No duplication."
            : "Windows only.");

    public IReadOnlyList<AudioEndpointInfo> GetTargets() =>
        _devices.GetOutputDevices(includeInactive: false)
            .Select(d => new AudioEndpointInfo(
                d.Id, d.FriendlyName, d.DeviceFriendlyName ?? "Unknown", d.State,
                VirtualAudioDeviceManager.LooksVirtual(d.FriendlyName), d.IsDefault))
            .ToList();

    public IRoutingPipeline CreatePipeline(ApplicationIdentity identity, uint processId, string targetDeviceId) =>
        new PolicyEndpointPipeline(_routing, identity.Key, processId, targetDeviceId);
}

internal sealed class PolicyEndpointPipeline : IRoutingPipeline
{
    private readonly AudioRoutingManager _routing;

    public PolicyEndpointPipeline(AudioRoutingManager routing, string applicationIdentifier, uint processId, string targetDeviceId)
    {
        _routing = routing;
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
        SetState(PipelineState.Starting);
        var result = _routing.Apply(ProcessId, ApplicationIdentifier, TargetDeviceId);
        if (!result.Success)
        {
            SetState(PipelineState.Failed);
            return false;
        }

        SetState(PipelineState.Running);
        return true;
    }

    public void Stop() => SetState(PipelineState.Stopped);

    public bool MoveTo(string targetDeviceId)
    {
        TargetDeviceId = targetDeviceId;
        return Start();
    }

    private void SetState(PipelineState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose() => Stop();
}
