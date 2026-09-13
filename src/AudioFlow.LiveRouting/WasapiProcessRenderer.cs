using AudioFlow.ProcessLoopback;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioFlow.LiveRouting;

/// <summary>Renderer statistics.</summary>
public sealed class RenderStatistics
{
    public long FramesWritten { get; internal set; }
    public long BytesWritten { get; internal set; }
    public long Underruns { get; internal set; }
    public int BufferedBytes { get; internal set; }
    public int LatencyMs { get; internal set; }
}

/// <summary>
/// Renders PCM audio to a specific output endpoint using NAudio's WasapiOut in
/// shared mode. The capture format is resampled/converted to the device mix
/// format when needed.
/// </summary>
public sealed class WasapiProcessRenderer : IDisposable
{
    private readonly string _deviceId;
    private readonly ProcessLoopbackFormat _sourceFormat;
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private BufferedWaveProvider? _buffer;
    private WasapiOut? _output;
    private bool _disposed;

    public WasapiProcessRenderer(string deviceId, ProcessLoopbackFormat sourceFormat)
    {
        _deviceId = deviceId;
        _sourceFormat = sourceFormat;
    }

    public RenderStatistics Statistics { get; } = new();

    public string? LastError { get; private set; }

    public bool IsRunning { get; private set; }

    public string? MixFormatDescription { get; private set; }

    public bool Start(int latencyMs = 100)
    {
        if (_disposed)
        {
            LastError = "Renderer was disposed.";
            return false;
        }

        if (IsRunning)
        {
            return true;
        }

        try
        {
            _enumerator = new MMDeviceEnumerator();
            _device = _enumerator.GetDevice(_deviceId);
            if (_device is null)
            {
                LastError = $"Device not found: {_deviceId}";
                return false;
            }

            var mix = _device.AudioClient.MixFormat;
            MixFormatDescription = $"{mix.SampleRate}Hz/{mix.Channels}ch/{mix.BitsPerSample}bit/{mix.Encoding}";

            var sourceWaveFormat = new WaveFormat(_sourceFormat.SampleRate, _sourceFormat.BitsPerSample, _sourceFormat.Channels);
            _buffer = new BufferedWaveProvider(sourceWaveFormat, TimeSpan.FromMilliseconds(750))
            {
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

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

            IWaveProvider provider = mix.Encoding == WaveFormatEncoding.IeeeFloat
                ? chain.ToWaveProvider()
                : chain.ToWaveProvider16();

            Statistics.LatencyMs = latencyMs;
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
            Cleanup();
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
            Statistics.BytesWritten += count;
            Statistics.FramesWritten += count / _sourceFormat.BlockAlign;
            Statistics.BufferedBytes = _buffer.BufferedBytes;

            if (_buffer.BufferedBytes == 0)
            {
                Statistics.Underruns++;
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        Cleanup();
    }

    private void Cleanup()
    {
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
