using AudioFlow.ProcessLoopback;

namespace AudioFlow.LiveRouting;

/// <summary>Explicit pipeline states (never a single bool).</summary>
public enum AudioPipelineState
{
    Created,
    Starting,
    Capturing,
    Rendering,
    Running,
    Moving,
    Stopping,
    Stopped,
    Failed,
    DeviceDisconnected
}

/// <summary>
/// A live audio pipeline: capture one process (Process Loopback) and render it to
/// a selected output device.
///
/// NOTE (verified on Windows 11): the process loopback capture is post-session-mute,
/// so muting the original session also silences the capture. This pipeline does not
/// suppress the original output; see docs/ARCHITECTURE-DECISION-LIVE-ROUTING.md.
/// </summary>
public sealed class AudioPipeline : IDisposable
{
    private readonly object _sync = new();
    private readonly ProcessLoopbackCapture _capture = new();
    private WasapiProcessRenderer? _renderer;
    private bool _disposed;

    public AudioPipeline(string applicationKey, uint processId, string targetDeviceId)
    {
        ApplicationKey = applicationKey;
        ProcessId = processId;
        TargetDeviceId = targetDeviceId;
    }

    public string ApplicationKey { get; }

    public uint ProcessId { get; private set; }

    public string TargetDeviceId { get; private set; }

    public AudioPipelineState State { get; private set; } = AudioPipelineState.Created;

    public string? LastError { get; private set; }

    public CaptureStatistics Capture => _capture.Statistics;

    public RenderStatistics Render => _renderer?.Statistics ?? new RenderStatistics();

    public string? MixFormat => _renderer?.MixFormatDescription;

    public event EventHandler<AudioPipelineState>? StateChanged;

    private void SetState(AudioPipelineState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public bool Start()
    {
        if (_disposed)
        {
            LastError = "Pipeline was disposed.";
            SetState(AudioPipelineState.Failed);
            return false;
        }

        if (State == AudioPipelineState.Running)
        {
            return true;
        }

        SetState(AudioPipelineState.Starting);

        _renderer = new WasapiProcessRenderer(TargetDeviceId, _capture.Format);
        if (!_renderer.Start())
        {
            LastError = _renderer.LastError;
            SetState(AudioPipelineState.Failed);
            return false;
        }

        SetState(AudioPipelineState.Rendering);

        _capture.DataAvailable += OnDataAvailable;
        if (!_capture.Start(ProcessId))
        {
            LastError = _capture.LastError;
            _capture.DataAvailable -= OnDataAvailable;
            _renderer.Stop();
            SetState(AudioPipelineState.Failed);
            return false;
        }

        SetState(AudioPipelineState.Running);
        LastError = null;
        return true;
    }

    private void OnDataAvailable(object? sender, ProcessLoopbackAudioEventArgs e) =>
        _renderer?.AddSamples(e.Buffer, e.Count);

    public bool MoveTo(string deviceId)
    {
        if (_disposed || State != AudioPipelineState.Running)
        {
            return false;
        }

        SetState(AudioPipelineState.Moving);

        var next = new WasapiProcessRenderer(deviceId, _capture.Format);
        if (!next.Start())
        {
            LastError = next.LastError;
            SetState(AudioPipelineState.Failed);
            return false;
        }

        var previous = _renderer;
        _renderer = next;
        TargetDeviceId = deviceId;
        previous?.Dispose();

        SetState(AudioPipelineState.Running);
        return true;
    }

    public void Stop()
    {
        if (_disposed && State == AudioPipelineState.Stopped)
        {
            return;
        }

        SetState(AudioPipelineState.Stopping);
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Stop();
        _renderer?.Stop();
        SetState(AudioPipelineState.Stopped);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _renderer?.Dispose();
        _renderer = null;
    }
}
