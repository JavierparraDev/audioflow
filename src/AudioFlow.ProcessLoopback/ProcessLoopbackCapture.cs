using System.Runtime.InteropServices;
using static AudioFlow.ProcessLoopback.ProcessLoopbackInterop;
using static AudioFlow.ProcessLoopback.WasapiInterop;

namespace AudioFlow.ProcessLoopback;

/// <summary>Which processes a capture includes.</summary>
public enum ProcessLoopbackScope
{
    IncludeTargetProcessTree = 0,
    ExcludeTargetProcessTree = 1
}

/// <summary>Audio format produced by the process loopback capture.</summary>
public sealed record ProcessLoopbackFormat(int SampleRate, int Channels, int BitsPerSample)
{
    public static ProcessLoopbackFormat Default { get; } = new(44100, 2, 16);

    public int BlockAlign => Channels * BitsPerSample / 8;

    public int AverageBytesPerSecond => SampleRate * BlockAlign;
}

/// <summary>A captured audio packet.</summary>
public sealed class ProcessLoopbackAudioEventArgs : EventArgs
{
    public ProcessLoopbackAudioEventArgs(byte[] buffer, int count, bool silent)
    {
        Buffer = buffer;
        Count = count;
        Silent = silent;
    }

    public byte[] Buffer { get; }
    public int Count { get; }
    public bool Silent { get; }
}

/// <summary>Real-time capture statistics.</summary>
public sealed class CaptureStatistics
{
    private readonly object _sync = new();

    public long TotalFrames { get; private set; }
    public long TotalBytes { get; private set; }
    public long PacketCount { get; private set; }
    public long SilentPackets { get; private set; }
    public float LastPeak { get; private set; }
    public float MaxPeak { get; private set; }
    public float LastRms { get; private set; }
    public int LastPacketFrames { get; private set; }

    public void Update(byte[] buffer, int count, bool silent)
    {
        float peak = 0f;
        double sumSquares = 0;
        var samples = count / 2;
        var sampleCount = 0;

        for (var i = 0; i + 1 < count; i += 2)
        {
            var value = (short)(buffer[i] | (buffer[i + 1] << 8));
            var normalized = value / 32768f;
            var abs = Math.Abs(normalized);
            if (abs > peak)
            {
                peak = abs;
            }

            sumSquares += (double)normalized * normalized;
            sampleCount++;
        }

        var rms = sampleCount > 0 ? (float)Math.Sqrt(sumSquares / sampleCount) : 0f;
        var frames = samples / 2;

        lock (_sync)
        {
            TotalFrames += frames;
            TotalBytes += count;
            PacketCount++;
            if (silent)
            {
                SilentPackets++;
            }

            LastPacketFrames = frames;
            LastPeak = peak;
            LastRms = rms;
            if (peak > MaxPeak)
            {
                MaxPeak = peak;
            }
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            TotalFrames = 0;
            TotalBytes = 0;
            PacketCount = 0;
            SilentPackets = 0;
            LastPeak = 0;
            MaxPeak = 0;
            LastRms = 0;
            LastPacketFrames = 0;
        }
    }
}

/// <summary>
/// Real per-process audio capture using the official Windows Process Loopback API
/// (AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS).
///
/// Capture format: 16-bit PCM, 2 channels, 44100 Hz (same as the Microsoft
/// ApplicationLoopback sample), with AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM so the
/// audio engine performs any conversion.
///
/// The capture runs on its own MTA thread and never touches the UI thread.
/// </summary>
public sealed class ProcessLoopbackCapture : IDisposable
{
    private readonly object _sync = new();
    private IAudioClient? _client;
    private IAudioCaptureClient? _captureClient;
    private AutoResetEvent? _sampleEvent;
    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    public ProcessLoopbackFormat Format { get; } = ProcessLoopbackFormat.Default;

    public CaptureStatistics Statistics { get; } = new();

    public string? LastError { get; private set; }

    public bool IsRunning => _running;

    public uint ProcessId { get; private set; }

    /// <summary>Raised on the capture thread for every packet.</summary>
    public event EventHandler<ProcessLoopbackAudioEventArgs>? DataAvailable;

    public bool Start(uint processId, ProcessLoopbackScope scope = ProcessLoopbackScope.IncludeTargetProcessTree)
    {
        if (_disposed)
        {
            LastError = "Capture was disposed.";
            return false;
        }

        if (_running)
        {
            return true;
        }

        ProcessId = processId;

        var mode = scope == ProcessLoopbackScope.ExcludeTargetProcessTree
            ? ProcessLoopbackMode.ExcludeTargetProcessTree
            : ProcessLoopbackMode.IncludeTargetProcessTree;

        var client = ProcessLoopbackActivator.Activate(processId, mode, TimeSpan.FromSeconds(5), out var error);
        if (client is null)
        {
            LastError = error;
            return false;
        }

        try
        {
            var format = WaveFormat.Pcm16Stereo44100();
            var flags = StreamFlagsLoopback | StreamFlagsEventCallback | StreamFlagsAutoConvertPcm
                        | StreamFlagsSrcDefaultQuality;

            var hr = client.Initialize(ShareModeShared, flags, 0, 0, ref format, IntPtr.Zero);
            if (hr < 0)
            {
                LastError = $"IAudioClient.Initialize failed (0x{hr:X8}).";
                ReleaseClient(client);
                return false;
            }

            hr = client.GetBufferSize(out var bufferFrames);
            if (hr < 0)
            {
                LastError = $"GetBufferSize failed (0x{hr:X8}).";
                ReleaseClient(client);
                return false;
            }

            var captureIid = IidAudioCaptureClient;
            hr = client.GetService(ref captureIid, out var capturePtr);
            if (hr < 0 || capturePtr == IntPtr.Zero)
            {
                LastError = $"GetService(IAudioCaptureClient) failed (0x{hr:X8}).";
                ReleaseClient(client);
                return false;
            }

            var captureClient = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(capturePtr);
            Marshal.Release(capturePtr);

            var sampleEvent = new AutoResetEvent(false);
            hr = client.SetEventHandle(sampleEvent.SafeWaitHandle.DangerousGetHandle());
            if (hr < 0)
            {
                LastError = $"SetEventHandle failed (0x{hr:X8}).";
                Marshal.ReleaseComObject(captureClient);
                ReleaseClient(client);
                return false;
            }

            _client = client;
            _captureClient = captureClient;
            _sampleEvent = sampleEvent;

            _running = true;
            _thread = new Thread(CaptureThread)
            {
                IsBackground = true,
                Name = "AudioFlow.ProcessLoopback"
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();

            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            ReleaseClient(client);
            return false;
        }
    }

    private void CaptureThread()
    {
        try
        {
            var hr = _client!.Start();
            if (hr < 0)
            {
                LastError = $"IAudioClient.Start failed (0x{hr:X8}).";
                return;
            }

            while (_running)
            {
                if (_sampleEvent!.WaitOne(200))
                {
                    DrainPackets();
                }
            }

            _client.Stop();
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private void DrainPackets()
    {
        while (_running)
        {
            var hr = _captureClient!.GetNextPacketSize(out var frames);
            if (hr < 0 || frames == 0)
            {
                return;
            }

            hr = _captureClient.GetBuffer(out var data, out var numFrames, out var flags, out _, out _);
            if (hr < 0)
            {
                return;
            }

            var bytes = (int)(numFrames * Format.BlockAlign);
            var silent = (flags & BufferFlagsSilent) != 0;
            var buffer = new byte[bytes];

            if (!silent && data != IntPtr.Zero && bytes > 0)
            {
                Marshal.Copy(data, buffer, 0, bytes);
            }

            _captureClient.ReleaseBuffer(numFrames);
            Statistics.Update(buffer, bytes, silent);
            DataAvailable?.Invoke(this, new ProcessLoopbackAudioEventArgs(buffer, bytes, silent));
        }
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _sampleEvent?.Set();
        _thread?.Join(TimeSpan.FromSeconds(3));
        _thread = null;

        lock (_sync)
        {
            if (_captureClient is not null)
            {
                Marshal.ReleaseComObject(_captureClient);
                _captureClient = null;
            }

            if (_client is not null)
            {
                Marshal.ReleaseComObject(_client);
                _client = null;
            }

            _sampleEvent?.Dispose();
            _sampleEvent = null;
        }
    }

    private static void ReleaseClient(IAudioClient client)
    {
        try
        {
            Marshal.ReleaseComObject(client);
        }
        catch
        {
            // best effort
        }
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
