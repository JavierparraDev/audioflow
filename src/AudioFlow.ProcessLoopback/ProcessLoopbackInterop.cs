using System.Runtime.InteropServices;

namespace AudioFlow.ProcessLoopback;

/// <summary>
/// Interop definitions for the official Windows Process Loopback API
/// (Windows 10 build 20348+ / Windows 11).
///
/// Documented types:
///   - ActivateAudioInterfaceAsync (mmdevapi.dll)
///   - IActivateAudioInterfaceCompletionHandler
///   - IActivateAudioInterfaceAsyncOperation
///   - AUDIOCLIENT_ACTIVATION_PARAMS / AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
///   - PROCESS_LOOPBACK_MODE
///
/// Reference: https://learn.microsoft.com/windows/win32/api/audioclientactivationparams/
/// </summary>
internal static class ProcessLoopbackInterop
{
    public const string ProcessLoopbackDeviceInterface = @"VAD\Process_Loopback";

    public static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

    public enum AudioClientActivationType
    {
        Default = 0,
        ProcessLoopback = 1
    }

    public enum ProcessLoopbackMode
    {
        IncludeTargetProcessTree = 0,
        ExcludeTargetProcessTree = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioClientActivationParams
    {
        public int ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropVariant
    {
        public ushort vt;
        public ushort reserved1;
        public ushort reserved2;
        public ushort reserved3;
        public uint blobSize;
        public IntPtr blobData;
    }

    public const ushort VtBlob = 65;

    [ComImport]
    [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport]
    [Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivateAudioInterfaceAsyncOperation
    {
        [PreserveSig]
        int GetActivateResult(out int activateResult, out IntPtr activatedInterface);
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    public static extern void ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        ref PropVariant activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);
}
