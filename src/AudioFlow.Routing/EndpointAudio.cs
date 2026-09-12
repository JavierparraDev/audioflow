using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioFlow.Routing;

public sealed class EndpointAudioEventArgs : EventArgs
{
    public EndpointAudioEventArgs(byte[] buffer, int count)
    {
        Buffer = buffer;
        Count = count;
    }

    public byte[] Buffer { get; }
    public int Count { get; }
}

/// <summary>
/// Captures the loopback (what is being rendered) of an output endpoint using
/// WASAPI loopback. Used to capture a virtual endpoint that the application
/// renders to.
/// </summary>
public sealed class EndpointLoopbackSource : IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private WasapiLoopbackCapture? _capture;

    public WaveFormat? Format { get; private set; }
    public PipelineMetrics Metrics { get; } = new();
    public string? LastError { get; private set; }
    public bool IsRunning => _capture is not null;

    public event EventHandler<EndpointAudioEventArgs>? DataAvailable;

    public bool Start(string deviceId)
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            var device = _enumerator.GetDevice(deviceId);
            _capture = new WasapiLoopbackCapture(device);
            Format = _capture.WaveFormat;
            _capture.DataAvailable += OnData;
            _capture.StartRecording();
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            Stop();
            return false;
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || Format is null)
        {
            return;
        }

        var peak = ComputePeak(e.Buffer, e.BytesRecorded, Format);
        Metrics.LastSourcePeak = peak;
        if (peak > Metrics.MaxSourcePeak)
        {
            Metrics.MaxSourcePeak = peak;
        }

        Metrics.FramesCaptured += e.BytesRecorded / Format.BlockAlign;
        DataAvailable?.Invoke(this, new EndpointAudioEventArgs(e.Buffer, e.BytesRecorded));
    }

    private static float ComputePeak(byte[] buffer, int count, WaveFormat format)
    {
        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
        {
            return 0f;
        }

        var peak = 0f;
        for (var i = 0; i + 3 < count; i += 4)
        {
            var value = Math.Abs(BitConverter.ToSingle(buffer, i));
            if (value > peak)
            {
                peak = value;
            }
        }

        return peak;
    }

    public void Stop()
    {
        if (_capture is null)
        {
            return;
        }

        try
        {
            _capture.DataAvailable -= OnData;
            _capture.StopRecording();
            _capture.Dispose();
        }
        catch
        {
            // best effort
        }

        _capture = null;
    }

    public void Dispose()
    {
        Stop();
        _enumerator?.Dispose();
        _enumerator = null;
    }
}

/// <summary>Renders a source format to a chosen endpoint in shared mode.</summary>
public sealed class WasapiEndpointRenderer : IDisposable
{
    private readonly string _deviceId;
    private readonly WaveFormat _sourceFormat;
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private BufferedWaveProvider? _buffer;
    private WasapiOut? _output;

    public WasapiEndpointRenderer(string deviceId, WaveFormat sourceFormat)
    {
        _deviceId = deviceId;
        _sourceFormat = sourceFormat;
    }

    public PipelineMetrics Metrics { get; } = new();
    public string? LastError { get; private set; }
    public bool IsRunning { get; private set; }
    public string? MixFormatDescription { get; private set; }

    public bool Start(int latencyMs = 100)
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _device = _enumerator.GetDevice(_deviceId);
            var mix = _device.AudioClient.MixFormat;
            MixFormatDescription = $"{mix.SampleRate}Hz/{mix.Channels}ch/{mix.BitsPerSample}bit/{mix.Encoding}";

            _buffer = new BufferedWaveProvider(_sourceFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(750),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

            IWaveProvider provider;
            if (FormatsMatch(_sourceFormat, mix))
            {
                // Source already matches the device mix format: feed it directly.
                provider = _buffer;
            }
            else
            {
                ISampleProvider chain = _buffer.ToSampleProvider();
                if (chain.WaveFormat.Channels == 1 && mix.Channels == 2)
                {
                    chain = new MonoToStereoSampleProvider(chain);
                }
                else if (chain.WaveFormat.Channels == 2 && mix.Channels == 1)
                {
                    chain = new StereoToMonoSampleProvider(chain);
                }

                if (chain.WaveFormat.SampleRate != mix.SampleRate)
                {
                    chain = new WdlResamplingSampleProvider(chain, mix.SampleRate);
                }

                provider = mix.Encoding == WaveFormatEncoding.IeeeFloat
                    ? chain.ToWaveProvider()
                    : chain.ToWaveProvider16();
            }

            Metrics.LatencyMs = latencyMs;
            _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, latencyMs);
            _output.Init(provider);
            _output.Play();

            IsRunning = true;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            Stop();
            return false;
        }
    }
    public void AddSamples(byte[] data, int count)
    {
        if (!IsRunning || _buffer is null || count <= 0)
        {
            return;
        }
        try
        {
            _buffer.AddSamples(data, 0, count);
            Metrics.FramesRendered += count / Math.Max(1, _sourceFormat.BlockAlign);
            if (_buffer.BufferedBytes == 0)
            {
                Metrics.Underruns++;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private static bool FormatsMatch(WaveFormat a, WaveFormat b) =>
        a.SampleRate == b.SampleRate &&
        a.Channels == b.Channels &&
        a.BitsPerSample == b.BitsPerSample &&
        a.Encoding == b.Encoding;

    public void Stop()
    {
        IsRunning = false;
        try
        {
            _output?.Stop();
            _output?.Dispose();
        }
        catch
        {
            // best effort
        }

        _output = null;
        _buffer = null;
        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
    }

    public void Dispose() => Stop();
}
