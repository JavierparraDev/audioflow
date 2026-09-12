using System.Runtime.InteropServices;

namespace AudioFlow.ProcessLoopback;

/// <summary>
/// Minimal WASAPI interop used by the live routing pipeline.
///
/// Only the members required to capture (IAudioCaptureClient) and render
/// (IAudioRenderClient via NAudio) are declared. All interfaces are official and
/// documented.
/// </summary>
internal static class WasapiInterop
{
    public static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    public static readonly Guid IidAudioCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    public const ushort WaveFormatPcm = 1;
    public const ushort WaveFormatIeeeFloat = 3;

    public const int ShareModeShared = 0;

    public const int StreamFlagsLoopback = 0x00020000;
    public const int StreamFlagsEventCallback = 0x00040000;
    public const int StreamFlagsSrcDefaultQuality = 0x08000000;
    public const int StreamFlagsAutoConvertPcm = unchecked((int)0x80000000);

    // IAudioCaptureClient::GetBuffer flags.
    public const uint BufferFlagsDataDiscontinuity = 0x1;
    public const uint BufferFlagsSilent = 0x2;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WaveFormat
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;

        public static WaveFormat Pcm16Stereo44100() => new()
        {
            wFormatTag = WaveFormatPcm,
            nChannels = 2,
            nSamplesPerSec = 44100,
            wBitsPerSample = 16,
            nBlockAlign = 2 * 16 / 8,
            nAvgBytesPerSec = 44100 * (2 * 16 / 8),
            cbSize = 0
        };
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        [PreserveSig]
        int Initialize(
            int shareMode,
            int streamFlags,
            long bufferDuration,
            long periodicity,
            ref WaveFormat format,
            IntPtr audioSessionGuid);

        [PreserveSig]
        int GetBufferSize(out uint bufferFrameCount);

        [PreserveSig]
        int GetStreamLatency(out long latency);

        [PreserveSig]
        int GetCurrentPadding(out uint padding);

        [PreserveSig]
        int IsFormatSupported(int shareMode, ref WaveFormat format, out IntPtr closestMatch);

        [PreserveSig]
        int GetMixFormat(out IntPtr deviceFormat);

        [PreserveSig]
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

        [PreserveSig]
        int Start();

        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int SetEventHandle(IntPtr eventHandle);

        [PreserveSig]
        int GetService(ref Guid iid, out IntPtr service);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(
            out IntPtr data,
            out uint numFrames,
            out uint flags,
            out ulong devicePosition,
            out ulong qpcPosition);

        [PreserveSig]
        int ReleaseBuffer(uint numFramesRead);

        [PreserveSig]
        int GetNextPacketSize(out uint numFramesInNextPacket);
    }
}
