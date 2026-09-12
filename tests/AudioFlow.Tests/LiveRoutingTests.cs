using AudioFlow.LiveRouting;
using AudioFlow.ProcessLoopback;
using Xunit;

namespace AudioFlow.Tests;

public class CaptureStatisticsTests
{
    [Fact]
    public void Silence_YieldsZeroPeakAndRms()
    {
        var stats = new CaptureStatistics();
        var buffer = new byte[400];

        stats.Update(buffer, buffer.Length, silent: true);

        Assert.Equal(0f, stats.LastPeak);
        Assert.Equal(0f, stats.LastRms);
        Assert.Equal(1, stats.SilentPackets);
        Assert.Equal(100, stats.LastPacketFrames); // 400 bytes / 4 bytes per stereo frame
    }

    [Fact]
    public void KnownSamples_ComputePeakAndRms()
    {
        var stats = new CaptureStatistics();
        // Two stereo frames, all samples at half scale (16384).
        var buffer = new byte[] { 0x00, 0x40, 0x00, 0x40, 0x00, 0x40, 0x00, 0x40 };

        stats.Update(buffer, buffer.Length, silent: false);

        Assert.InRange(stats.LastPeak, 0.49f, 0.51f);
        Assert.InRange(stats.LastRms, 0.49f, 0.51f);
        Assert.Equal(2, stats.LastPacketFrames);
    }

    [Fact]
    public void MaxPeak_TracksMaximum()
    {
        var stats = new CaptureStatistics();
        stats.Update(new byte[] { 0x00, 0x10, 0x00, 0x10 }, 4, false); // 4096
        var first = stats.MaxPeak;
        stats.Update(new byte[] { 0x00, 0x70, 0x00, 0x70 }, 4, false); // 28672

        Assert.True(stats.MaxPeak > first);
    }

    [Fact]
    public void Reset_ClearsCounters()
    {
        var stats = new CaptureStatistics();
        stats.Update(new byte[] { 0x00, 0x40, 0x00, 0x40 }, 4, false);
        stats.Reset();

        Assert.Equal(0, stats.TotalFrames);
        Assert.Equal(0, stats.PacketCount);
        Assert.Equal(0f, stats.MaxPeak);
    }
}

public class ProcessLoopbackFormatTests
{
    [Fact]
    public void DefaultFormat_IsPcm16Stereo44100()
    {
        var format = ProcessLoopbackFormat.Default;

        Assert.Equal(44100, format.SampleRate);
        Assert.Equal(2, format.Channels);
        Assert.Equal(16, format.BitsPerSample);
        Assert.Equal(4, format.BlockAlign);
        Assert.Equal(176400, format.AverageBytesPerSecond);
    }
}

public class AudioPipelineTests
{
    [Fact]
    public void Start_WithInvalidDevice_FailsCleanly()
    {
        using var pipeline = new AudioPipeline("exe:test.exe", 1234, "{nonexistent-device}");

        var started = pipeline.Start();

        Assert.False(started);
        Assert.Equal(AudioPipelineState.Failed, pipeline.State);
        Assert.False(string.IsNullOrWhiteSpace(pipeline.LastError));
    }

    [Fact]
    public void Stop_OnCreatedPipeline_ReachesStopped()
    {
        using var pipeline = new AudioPipeline("exe:test.exe", 1234, "{nonexistent-device}");

        pipeline.Stop();

        Assert.Equal(AudioPipelineState.Stopped, pipeline.State);
    }

    [Fact]
    public void StateChanged_IsRaised()
    {
        using var pipeline = new AudioPipeline("exe:test.exe", 1234, "{nonexistent-device}");
        var states = new List<AudioPipelineState>();
        pipeline.StateChanged += (_, state) => states.Add(state);

        pipeline.Start();

        Assert.Contains(AudioPipelineState.Starting, states);
        Assert.Contains(AudioPipelineState.Failed, states);
    }
}

public class LiveRoutingManagerTests
{
    [Fact]
    public void RemovePipeline_UnknownKey_ReturnsFalse()
    {
        using var manager = new LiveRoutingManager();

        Assert.False(manager.RemovePipeline("exe:missing.exe"));
    }

    [Fact]
    public void Pipelines_IsEmptyInitially()
    {
        using var manager = new LiveRoutingManager();

        Assert.Empty(manager.Pipelines);
    }

    [Fact]
    public void StopAll_OnEmptyManager_DoesNotThrow()
    {
        using var manager = new LiveRoutingManager();
        manager.StopAll();
    }
}
