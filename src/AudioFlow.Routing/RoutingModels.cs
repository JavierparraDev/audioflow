using AudioFlow.Models;

namespace AudioFlow.Routing;

public enum RoutingBackendKind
{
    None,
    PolicyEndpoint,
    VirtualEndpoint
}

public enum PipelineState
{
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Blocked
}

/// <summary>An output endpoint (physical or virtual) usable as a routing target.</summary>
public sealed record AudioEndpointInfo(
    string DeviceId,
    string FriendlyName,
    string DeviceType,
    AudioDeviceState State,
    bool IsVirtual,
    bool IsDefault)
{
    public bool IsAvailable => State == AudioDeviceState.Active;
}

/// <summary>Availability information for a routing backend.</summary>
public sealed record RoutingBackendInfo(
    string Name,
    RoutingBackendKind Kind,
    bool IsAvailable,
    string? Reason)
{
    public string Status => IsAvailable ? "AVAILABLE" : "BLOCKED";
}

/// <summary>Live metrics for a routing pipeline.</summary>
public sealed class PipelineMetrics
{
    public long FramesCaptured { get; internal set; }
    public long FramesRendered { get; internal set; }
    public long Underruns { get; internal set; }
    public float LastSourcePeak { get; internal set; }
    public float MaxSourcePeak { get; internal set; }
    public int LatencyMs { get; internal set; }
}

public interface IRoutingPipeline : IDisposable
{
    string ApplicationIdentifier { get; }
    uint ProcessId { get; }
    string TargetDeviceId { get; }
    PipelineState State { get; }
    PipelineMetrics Metrics { get; }
    event EventHandler<PipelineState>? StateChanged;
    bool Start();
    void Stop();
    bool MoveTo(string targetDeviceId);
}

/// <summary>
/// A swappable routing backend. The UI depends on this interface, never on
/// Process Loopback or a specific virtual audio implementation.
/// </summary>
public interface IRoutingBackend
{
    RoutingBackendInfo Info { get; }

    /// <summary>Physical targets this backend can render to.</summary>
    IReadOnlyList<AudioEndpointInfo> GetTargets();

    IRoutingPipeline CreatePipeline(ApplicationIdentity identity, uint processId, string targetDeviceId);
}
