using System.Runtime.InteropServices;

namespace AudioFlow.Core.WindowsAudio;

/// <summary>
/// Access to the Windows internal interface used by Settings for
/// "App volume and device preferences" (per-application output device).
///
/// Activation: RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig").
/// Interfaces (variant per Windows build):
///   - IAudioPolicyConfigFactoryVariantFor21H2    {ab3d4648-e242-459f-b02f-541c70306324}
///   - IAudioPolicyConfigFactoryVariantForDownlevel {2a59116d-6c4f-45e0-a74f-707e3fef9258}
///
/// These interfaces are NOT documented or officially supported. Their layout was
/// obtained from the reverse-engineered definitions used by EarTrumpet
/// (File-New-Project/EarTrumpet). AudioFlow calls the methods through their
/// vtable slots (Set = 25, Get = 26) to avoid depending on IInspectable
/// marshalling, and degrades gracefully if activation fails.
///
/// IMPORTANT: setting the persisted endpoint only takes effect when the target
/// application (re)initializes its audio stream. It does not move a live stream.
/// </summary>
internal static class AudioPolicyConfig
{
    private const string ActivatableClassId = "Windows.Media.Internal.AudioPolicyConfig";

    // The persisted-endpoint API expects the device interface path, not the raw
    // MMDevice id. EarTrumpet wraps it like this for render endpoints:
    //   \\?\SWD#MMDEVAPI#{0.0.0...}.{guid}#{e6327cad-dcec-4949-ae8a-991e976a79d2}
    private const string MmdevapiToken = @"\\?\SWD#MMDEVAPI#";
    private const string DeviceInterfaceAudioRender = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    private static readonly Guid IidFor21H2 = new("ab3d4648-e242-459f-b02f-541c70306324");
    private static readonly Guid IidForDownlevel = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");
    private static readonly Guid IidFor1709 = new("32aa8e18-6496-4e24-9f94-b800e7eccc45");

    // IUnknown (3) + IInspectable (3) + 19 placeholder methods = 25.
    private const int VtableIndexSetPersistedEndpoint = 25;
    private const int VtableIndexGetPersistedEndpoint = 26;

    private const int EDataFlowRender = 0;
    private const int ERoleConsole = 0;
    private const int ERoleMultimedia = 1;

    private static readonly object FactoryLock = new();
    private static IntPtr _cachedFactory = IntPtr.Zero;
    private static bool _factoryUnavailable;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetPersistedDefaultAudioEndpointDelegate(
        IntPtr self, uint processId, int flow, int role, IntPtr deviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPersistedDefaultAudioEndpointDelegate(
        IntPtr self, uint processId, int flow, int role, out IntPtr deviceId);

    /// <summary>
    /// Sets the persisted output endpoint for a process. Returns true on success.
    /// </summary>
    public static bool TrySetPersistedEndpoint(uint processId, string deviceId, out string? error)
    {
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            error = "Not running on Windows.";
            return false;
        }

        if (processId == 0 || string.IsNullOrWhiteSpace(deviceId))
        {
            error = "Invalid process id or device id.";
            return false;
        }

        var factory = GetFactory(out error);
        if (factory == IntPtr.Zero)
        {
            return false;
        }

        var deviceHString = IntPtr.Zero;
        try
        {
            deviceHString = CreateHString(ToPersistedDeviceId(deviceId));
            if (deviceHString == IntPtr.Zero)
            {
                error = "Could not create HSTRING for the device id.";
                return false;
            }

            var setEndpoint = GetVtableDelegate<SetPersistedDefaultAudioEndpointDelegate>(
                factory, VtableIndexSetPersistedEndpoint);

            // Windows Settings persists the preference for both the Console and
            // Multimedia roles. Do the same so the app follows the rule in either.
            var consoleHr = setEndpoint(factory, processId, EDataFlowRender, ERoleConsole, deviceHString);
            var multimediaHr = setEndpoint(factory, processId, EDataFlowRender, ERoleMultimedia, deviceHString);

            if (consoleHr < 0 && multimediaHr < 0)
            {
                error = $"SetPersistedDefaultAudioEndpoint failed (0x{multimediaHr:X8}).";
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
            if (deviceHString != IntPtr.Zero)
            {
                WindowsDeleteString(deviceHString);
            }
        }
    }

    /// <summary>
    /// Reads back the persisted output endpoint for a process. Used to verify
    /// that a write actually took effect.
    /// </summary>
    public static string? TryGetPersistedEndpoint(uint processId, out string? error)
    {
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            error = "Not running on Windows.";
            return null;
        }

        var factory = GetFactory(out error);
        if (factory == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var getEndpoint = GetVtableDelegate<GetPersistedDefaultAudioEndpointDelegate>(
                factory, VtableIndexGetPersistedEndpoint);

            var hr = getEndpoint(factory, processId, EDataFlowRender, ERoleMultimedia, out var hstring);
            if (hr < 0)
            {
                error = $"GetPersistedDefaultAudioEndpoint failed (0x{hr:X8}).";
                return null;
            }

            if (hstring == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return FromPersistedDeviceId(ReadHString(hstring));
            }
            finally
            {
                WindowsDeleteString(hstring);
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Returns the cached activation factory. The factory is free-threaded and
    /// lives for the process lifetime; this avoids re-activating WinRT on every
    /// routing call (R10). Access is serialized because routing can be invoked
    /// from the UI thread and from the session monitor.
    /// </summary>
    private static IntPtr GetFactory(out string? error)
    {
        error = null;

        lock (FactoryLock)
        {
            if (_cachedFactory != IntPtr.Zero)
            {
                return _cachedFactory;
            }

            if (_factoryUnavailable)
            {
                error = "AudioPolicyConfig factory is not available on this system.";
                return IntPtr.Zero;
            }

            var factory = CreateFactory(out error);
            if (factory == IntPtr.Zero)
            {
                _factoryUnavailable = true;
                return IntPtr.Zero;
            }

            _cachedFactory = factory;
            return _cachedFactory;
        }
    }

    private static IntPtr CreateFactory(out string? error)
    {
        error = null;

        var classHString = CreateHString(ActivatableClassId);
        if (classHString == IntPtr.Zero)
        {
            error = "Could not create HSTRING for the activation class id.";
            return IntPtr.Zero;
        }

        try
        {
            foreach (var iid in new[] { IidFor21H2, IidForDownlevel, IidFor1709 })
            {
                var localIid = iid;
                var hr = RoGetActivationFactory(classHString, ref localIid, out var factory);
                if (hr == 0 && factory != IntPtr.Zero)
                {
                    return factory;
                }

                error = $"RoGetActivationFactory failed (0x{hr:X8}) for {iid}.";
            }

            return IntPtr.Zero;
        }
        finally
        {
            WindowsDeleteString(classHString);
        }
    }

    private static string ToPersistedDeviceId(string mmDeviceId) =>
        $"{MmdevapiToken}{mmDeviceId}{DeviceInterfaceAudioRender}";

    private static string FromPersistedDeviceId(string persistedDeviceId)
    {
        var value = persistedDeviceId;
        if (value.StartsWith(MmdevapiToken, StringComparison.Ordinal))
        {
            value = value[MmdevapiToken.Length..];
        }

        if (value.EndsWith(DeviceInterfaceAudioRender, StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^DeviceInterfaceAudioRender.Length];
        }

        return value;
    }

    private static T GetVtableDelegate<T>(IntPtr instance, int index) where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var functionPointer = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(functionPointer);
    }

    private static IntPtr CreateHString(string value) =>
        WindowsCreateString(value, value.Length, out var hstring) == 0 ? hstring : IntPtr.Zero;

    private static string ReadHString(IntPtr hstring)
    {
        var buffer = WindowsGetStringRawBuffer(hstring, out var length);
        return buffer == IntPtr.Zero || length == 0
            ? string.Empty
            : Marshal.PtrToStringUni(buffer, (int)length) ?? string.Empty;
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId, [In] ref Guid iid, out IntPtr activationFactory);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
}
