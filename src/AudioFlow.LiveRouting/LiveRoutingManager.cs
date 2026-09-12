namespace AudioFlow.LiveRouting;

/// <summary>
/// Creates, tracks and moves live audio pipelines, one per application.
/// Independent pipelines never share buffers.
/// </summary>
public sealed class LiveRoutingManager : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AudioPipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public event EventHandler<AudioPipeline>? PipelineChanged;

    public IReadOnlyCollection<AudioPipeline> Pipelines
    {
        get
        {
            lock (_sync)
            {
                return _pipelines.Values.ToList();
            }
        }
    }

    public AudioPipeline EnsurePipeline(string applicationKey, uint processId, string targetDeviceId)
    {
        lock (_sync)
        {
            if (_pipelines.TryGetValue(applicationKey, out var existing))
            {
                if (!string.Equals(existing.TargetDeviceId, targetDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    existing.MoveTo(targetDeviceId);
                }

                return existing;
            }

            var pipeline = new AudioPipeline(applicationKey, processId, targetDeviceId);
            pipeline.StateChanged += (_, _) => PipelineChanged?.Invoke(this, pipeline);
            pipeline.Start();
            _pipelines[applicationKey] = pipeline;
            return pipeline;
        }
    }

    public bool RemovePipeline(string applicationKey)
    {
        AudioPipeline? pipeline;
        lock (_sync)
        {
            if (!_pipelines.Remove(applicationKey, out pipeline))
            {
                return false;
            }
        }

        pipeline.Dispose();
        return true;
    }

    public void StopAll()
    {
        List<AudioPipeline> pipelines;
        lock (_sync)
        {
            pipelines = _pipelines.Values.ToList();
            _pipelines.Clear();
        }

        foreach (var pipeline in pipelines)
        {
            pipeline.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAll();
    }
}
