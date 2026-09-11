using AudioFlow.Core.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AudioFlow.Core;

/// <summary>
/// Bridges Windows endpoint notifications (IMMNotificationClient) to
/// <see cref="AudioDeviceManager.DevicesChanged"/>.
/// </summary>
internal sealed class DeviceNotificationClient : IMMNotificationClient
{
    private readonly AudioDeviceManager _owner;

    public DeviceNotificationClient(AudioDeviceManager owner) => _owner = owner;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        Log.Info($"Device state changed: {deviceId} -> {newState}");
        _owner.RaiseDevicesChanged($"state:{deviceId}");
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        Log.Info($"Device added: {pwstrDeviceId}");
        _owner.RaiseDevicesChanged($"added:{pwstrDeviceId}");
    }

    public void OnDeviceRemoved(string deviceId)
    {
        Log.Info($"Device removed: {deviceId}");
        _owner.RaiseDevicesChanged($"removed:{deviceId}");
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render)
        {
            return;
        }

        Log.Info($"Default render device changed ({role}): {defaultDeviceId}");
        _owner.RaiseDevicesChanged($"default:{role}:{defaultDeviceId}");
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Property changes are noisy; only forward them as a generic refresh.
        _owner.RaiseDevicesChanged($"property:{pwstrDeviceId}");
    }
}
