using System.Runtime.InteropServices;
using static AudioFlow.ProcessLoopback.ProcessLoopbackInterop;

namespace AudioFlow.ProcessLoopback;

/// <summary>
/// Activates a Process Loopback IAudioClient for a target process and returns the
/// COM interface so it can be used for real capture.
/// </summary>
internal static class ProcessLoopbackActivator
{
    public static WasapiInterop.IAudioClient? Activate(
        uint processId,
        ProcessLoopbackMode mode,
        TimeSpan timeout,
        out string? error)
    {
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            error = "Not running on Windows.";
            return null;
        }

        if (Environment.OSVersion.Version.Build < 20348)
        {
            error = $"Requires Windows 10 build 20348 or later (current build {Environment.OSVersion.Version.Build}).";
            return null;
        }

        if (processId == 0)
        {
            error = "Invalid process id.";
            return null;
        }

        var parameters = new AudioClientActivationParams
        {
            ActivationType = (int)AudioClientActivationType.ProcessLoopback,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = processId,
                ProcessLoopbackMode = (int)mode
            }
        };

        var handler = new ActivationCompletionHandler();
        var iid = WasapiInterop.IidAudioClient;
        var blob = IntPtr.Zero;

        try
        {
            var size = Marshal.SizeOf<AudioClientActivationParams>();
            blob = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(parameters, blob, fDeleteOld: false);

            var propVariant = new PropVariant
            {
                vt = VtBlob,
                blobSize = (uint)size,
                blobData = blob
            };

            ActivateAudioInterfaceAsync(ProcessLoopbackDeviceInterface, ref iid, ref propVariant, handler, out _);

            if (!handler.Wait(timeout))
            {
                error = "Activation timed out.";
                return null;
            }

            if (handler.ActivateResult < 0)
            {
                error = $"Activation failed (0x{handler.ActivateResult:X8}).";
                return null;
            }

            if (handler.ActivatedInterface == IntPtr.Zero)
            {
                error = "Activation returned no interface.";
                return null;
            }

            var instance = Marshal.GetObjectForIUnknown(handler.ActivatedInterface);
            return (WasapiInterop.IAudioClient)instance;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
        finally
        {
            if (handler.ActivatedInterface != IntPtr.Zero)
            {
                Marshal.Release(handler.ActivatedInterface);
            }

            if (blob != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(blob);
            }
        }
    }
}
