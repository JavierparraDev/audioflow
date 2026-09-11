using AudioFlow.Applications;
using AudioFlow.Core.Logging;
using AudioFlow.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AudioFlow.Core;

/// <summary>
/// Enumerates the audio sessions of every active render endpoint.
///
/// Underlying APIs (official and documented):
///   - IMMDevice::Activate(IID_IAudioSessionManager2)
///   - IAudioSessionManager2::GetSessionEnumerator
///   - IAudioSessionControl / IAudioSessionControl2
///   - ISimpleAudioVolume, IAudioMeterInformation
///
/// Note: Windows has no API to move a live session to another endpoint; this
/// type only reads session information.
/// </summary>
public sealed class AudioSessionManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly ProcessManager _processManager;
    private readonly ApplicationIdentifier _identifier;
    private bool _disposed;

    public AudioSessionManager(
        ProcessManager? processManager = null,
        ApplicationIdentifier? identifier = null)
    {
        _enumerator = new MMDeviceEnumerator();
        _processManager = processManager ?? new ProcessManager();
        _identifier = identifier ?? new ApplicationIdentifier(_processManager);
    }

    /// <summary>
    /// Returns the sessions of all active render devices.
    /// </summary>
    /// <param name="includeInactive">
    /// When false, only sessions whose state is Active are returned.
    /// </param>
    public IReadOnlyList<AudioSessionInfo> GetSessions(bool includeInactive = false)
    {
        var result = new List<AudioSessionInfo>();

        var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (var i = 0; i < collection.Count; i++)
        {
            MMDevice device;
            try
            {
                device = collection[i];
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not open render device #{i}: {ex.Message}");
                continue;
            }

            using (device)
            {
                ReadDeviceSessions(device, _processManager, _identifier, result, includeInactive);
            }
        }

        return result;
    }

    internal static void ReadDeviceSessions(
        MMDevice device,
        ProcessManager processManager,
        ApplicationIdentifier identifier,
        List<AudioSessionInfo> into,
        bool includeInactive)
    {
        string deviceId;
        string deviceName;
        try
        {
            deviceId = device.ID;
            deviceName = device.FriendlyName;
        }
        catch (Exception ex)
        {
            Log.Debug($"Could not read device properties: {ex.Message}");
            return;
        }

        NAudio.CoreAudioApi.AudioSessionManager sessionManager;
        try
        {
            sessionManager = device.AudioSessionManager;
        }
        catch (Exception ex)
        {
            Log.Debug($"Could not get session manager for '{deviceName}': {ex.Message}");
            return;
        }

        SessionCollection sessions;
        try
        {
            sessions = sessionManager.Sessions;
        }
        catch (Exception ex)
        {
            Log.Debug($"Could not enumerate sessions for '{deviceName}': {ex.Message}");
            return;
        }

        if (sessions is null)
        {
            return;
        }

        var count = sessions.Count;
        for (var i = 0; i < count; i++)
        {
            AudioSessionControl? control = null;
            try
            {
                control = sessions[i];
                var info = Map(control, deviceId, deviceName, processManager, identifier);

                if (!includeInactive && info.State != AudioSessionStateKind.Active)
                {
                    continue;
                }

                into.Add(info);
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not read session #{i} on '{deviceName}': {ex.Message}");
            }
            finally
            {
                control?.Dispose();
            }
        }
    }

    private static AudioSessionInfo Map(
        AudioSessionControl control,
        string deviceId,
        string deviceName,
        ProcessManager processManager,
        ApplicationIdentifier identifier)
    {
        var processId = TryUInt(() => control.GetProcessID);
        var details = processManager.Resolve(processId);
        var identity = identifier.Identify(details);

        var volume = 0f;
        var muted = false;
        try
        {
            var simple = control.SimpleAudioVolume;
            if (simple is not null)
            {
                volume = simple.Volume;
                muted = simple.Mute;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Volume unavailable for pid {processId}: {ex.Message}");
        }

        var peak = 0f;
        try
        {
            var meter = control.AudioMeterInformation;
            if (meter is not null)
            {
                peak = meter.MasterPeakValue;
            }
        }
        catch
        {
            // Metering is best-effort.
        }

        return new AudioSessionInfo
        {
            SessionIdentifier = TryString(() => control.GetSessionIdentifier) ?? string.Empty,
            SessionInstanceIdentifier = TryString(() => control.GetSessionInstanceIdentifier),
            ProcessId = processId,
            ProcessName = details.ProcessName,
            ProcessPath = details.ExecutablePath,
            ApplicationKey = identity.Key,
            Aumid = identity.Aumid,
            ApplicationName = identity.DisplayName,
            DisplayName = TryString(() => control.DisplayName),
            DeviceId = deviceId,
            DeviceName = deviceName,
            State = MapState(control),
            Volume = volume,
            IsMuted = muted,
            PeakValue = peak,
            IsSystemSoundsSession = TryBool(() => control.IsSystemSoundsSession),
            LastSeen = DateTimeOffset.Now
        };
    }

    private static AudioSessionStateKind MapState(AudioSessionControl control)
    {
        try
        {
            return control.State switch
            {
                AudioSessionState.AudioSessionStateActive => AudioSessionStateKind.Active,
                AudioSessionState.AudioSessionStateInactive => AudioSessionStateKind.Inactive,
                AudioSessionState.AudioSessionStateExpired => AudioSessionStateKind.Expired,
                _ => AudioSessionStateKind.Unknown
            };
        }
        catch
        {
            return AudioSessionStateKind.Unknown;
        }
    }

    private static uint TryUInt(Func<uint> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryString(Func<string> getter)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryBool(Func<bool> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.Dispose();
    }
}
