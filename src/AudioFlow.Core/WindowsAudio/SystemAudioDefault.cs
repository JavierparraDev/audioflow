using System.Runtime.InteropServices;

namespace AudioFlow.Core.WindowsAudio;

/// <summary>
/// Sets the Windows default output device using the internal
/// <c>IPolicyConfig</c> interface (the same one used by the Sound control panel).
///
/// This is what a "reset to factory" of the default output means: pick which
/// endpoint plays audio when an application has no explicit preference.
/// </summary>
public static class SystemAudioDefault
{
    // IUnknown (3) + the 10 IPolicyConfig methods that precede SetDefaultEndpoint.
    private const int VtableIndexSetDefaultEndpoint = 13;

    private const uint ClsCtxAll = 0x17;

    private const int ERoleConsole = 0;
    private const int ERoleMultimedia = 1;
    private const int ERoleCommunications = 2;

    private static readonly Guid ClsidPolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");
    private static readonly Guid IidPolicyConfig = new("f8679f50-850a-41cf-9c72-430f290290c8");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetDefaultEndpointDelegate(
        IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);

    /// <summary>
    /// Makes <paramref name="deviceId"/> the default render device for the
    /// Console, Multimedia and Communications roles. Returns true on success.
    /// </summary>
    public static bool SetDefault(string deviceId, out string? error)
    {
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            error = "Not running on Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            error = "Invalid device id.";
            return false;
        }

        var policyConfig = IntPtr.Zero;
        try
        {
            var clsid = ClsidPolicyConfigClient;
            var iid = IidPolicyConfig;
            var hr = CoCreateInstance(ref clsid, IntPtr.Zero, ClsCtxAll, ref iid, out policyConfig);
            if (hr < 0 || policyConfig == IntPtr.Zero)
            {
                error = $"CoCreateInstance(IPolicyConfig) failed (0x{hr:X8}).";
                return false;
            }

            var setDefault = GetVtableDelegate<SetDefaultEndpointDelegate>(
                policyConfig, VtableIndexSetDefaultEndpoint);

            var anyOk = false;
            foreach (var role in new[] { ERoleConsole, ERoleMultimedia, ERoleCommunications })
            {
                if (setDefault(policyConfig, deviceId, role) >= 0)
                {
                    anyOk = true;
                }
            }

            if (!anyOk)
            {
                error = "SetDefaultEndpoint failed for every role.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
        finally
        {
            if (policyConfig != IntPtr.Zero)
            {
                Marshal.Release(policyConfig);
            }
        }
    }

    private static T GetVtableDelegate<T>(IntPtr instance, int index) where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var functionPointer = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(functionPointer);
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);
}
