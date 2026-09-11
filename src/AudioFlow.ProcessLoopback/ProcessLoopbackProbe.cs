using System.Runtime.InteropServices;
using static AudioFlow.ProcessLoopback.ProcessLoopbackInterop;

namespace AudioFlow.ProcessLoopback;

/// <summary>Result of probing the Process Loopback API for a target process.</summary>
public sealed record ProcessLoopbackProbeResult(
    bool Supported,
    bool Activated,
    string Message);

/// <summary>
/// EXPERIMENTAL. Probes whether Windows can activate a Process Loopback capture
/// stream for a given process (the official API introduced in Windows 10 build
/// 20348 / Windows 11).
///
/// This module is intentionally isolated from the MVP. It only verifies that the
/// capture interface can be activated; it does not yet re-render audio. See
/// docs/AUDIO-ROUTING.md and docs/LIMITATIONS.md for the analysis.
/// </summary>
public static class ProcessLoopbackProbe
{
    public static bool IsSupported =>
        OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 20348;

    public static ProcessLoopbackProbeResult Probe(uint processId, TimeSpan? timeout = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ProcessLoopbackProbeResult(false, false, "Not running on Windows.");
        }

        if (Environment.OSVersion.Version.Build < 20348)
        {
            return new ProcessLoopbackProbeResult(
                false, false,
                $"Requires Windows 10 build 20348 or later (current build {Environment.OSVersion.Version.Build}).");
        }

        if (processId == 0)
        {
            return new ProcessLoopbackProbeResult(true, false, "Invalid process id.");
        }

        var parameters = new AudioClientActivationParams
        {
            ActivationType = (int)AudioClientActivationType.ProcessLoopback,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = processId,
                ProcessLoopbackMode = (int)ProcessLoopbackMode.IncludeTargetProcessTree
            }
        };

        var handler = new ActivationCompletionHandler();
        var iid = IidAudioClient;
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

            ActivateAudioInterfaceAsync(
                ProcessLoopbackDeviceInterface,
                ref iid,
                ref propVariant,
                handler,
                out var operation);

            if (!handler.Wait(timeout ?? TimeSpan.FromSeconds(5)))
            {
                return new ProcessLoopbackProbeResult(true, false, "Activation timed out.");
            }

            if (handler.ActivateResult < 0)
            {
                return new ProcessLoopbackProbeResult(
                    true, false, $"Activation failed (0x{handler.ActivateResult:X8}).");
            }

            if (handler.ActivatedInterface == IntPtr.Zero)
            {
                return new ProcessLoopbackProbeResult(true, false, "Activation returned no interface.");
            }

            return new ProcessLoopbackProbeResult(
                true, true, "Process loopback IAudioClient activated successfully (interface acquired).");
        }
        catch (Exception ex)
        {
            return new ProcessLoopbackProbeResult(true, false, $"{ex.GetType().Name}: {ex.Message}");
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

/// <summary>Managed implementation of the WinRT activation completion callback.</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class ActivationCompletionHandler : IActivateAudioInterfaceCompletionHandler
{
    private readonly ManualResetEventSlim _completed = new(false);

    public int ActivateResult { get; private set; } = unchecked((int)0x80004005);
    public IntPtr ActivatedInterface { get; private set; } = IntPtr.Zero;

    public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
    {
        try
        {
            var hr = activateOperation.GetActivateResult(out var result, out var pointer);
            ActivateResult = hr < 0 ? hr : result;
            ActivatedInterface = pointer;
        }
        catch
        {
            ActivateResult = unchecked((int)0x80004005);
        }
        finally
        {
            _completed.Set();
        }
    }

    public bool Wait(TimeSpan timeout) => _completed.Wait(timeout);
}
