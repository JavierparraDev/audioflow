using AudioFlow.Core.Logging;
using AudioFlow.Models;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AudioFlow.Core;

public sealed class AudioDeviceChangedEventArgs : EventArgs
{
    public AudioDeviceChangedEventArgs(string reason) => Reason = reason;
    public string Reason { get; }
}

/// <summary>
/// Enumerates Windows render endpoints through the MMDevice API and raises an
/// event when devices are added, removed, change state or when the default
/// device changes.
///
/// Underlying APIs (all official and documented):
///   - IMMDeviceEnumerator
///   - IMMNotificationClient
/// </summary>
public sealed class AudioDeviceManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notificationClient;
    private bool _disposed;

    public event EventHandler<AudioDeviceChangedEventArgs>? DevicesChanged;

    public AudioDeviceManager()
    {
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new DeviceNotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    /// <summary>
    /// Returns all output (render) endpoints. When <paramref name="includeInactive"/>
    /// is true, disabled/unplugged/not-present devices are included.
    /// </summary>
    public IReadOnlyList<AudioDevice> GetOutputDevices(bool includeInactive = true)
    {
        var stateMask = includeInactive ? DeviceState.All : DeviceState.Active;
        var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, stateMask);
        var defaults = GetDefaultDeviceIds();

        var result = new List<AudioDevice>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
        {
            using var device = collection[i];
            result.Add(Map(device, defaults.Contains(device.ID)));
        }

        return result;
    }

    public AudioDevice? GetDefaultOutputDevice(Role role = Role.Console)
    {
        if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, role))
        {
            return null;
        }

        using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
        return Map(device, isDefault: true);
    }

    public AudioDevice? FindById(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            var defaults = GetDefaultDeviceIds();
            return Map(device, defaults.Contains(device.ID));
        }
        catch (Exception ex)
        {
            Log.Debug($"Device not found: {deviceId} ({ex.GetType().Name})");
            return null;
        }
    }

    public bool DeviceExists(string deviceId) => FindById(deviceId) is { State: AudioDeviceState.Active };

    private HashSet<string> GetDefaultDeviceIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in new[] { Role.Console, Role.Multimedia, Role.Communications })
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, role))
            {
                continue;
            }

            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
            ids.Add(device.ID);
        }

        return ids;
    }

    private static AudioDevice Map(MMDevice device, bool isDefault) => new()
    {
        Id = device.ID,
        FriendlyName = device.FriendlyName,
        DeviceFriendlyName = Safe(() => device.DeviceFriendlyName),
        InstanceId = Safe(() => device.InstanceId),
        IconPath = Safe(() => device.IconPath),
        State = MapState(device.State),
        DataFlow = AudioFlowDataFlow.Render,
        IsDefault = isDefault
    };

    private static string? Safe(Func<string> getter)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) || value == "Unknown" ? null : value;
        }
        catch
        {
            return null;
        }
    }

    internal static AudioDeviceState MapState(DeviceState state) => state switch
    {
        DeviceState.Active => AudioDeviceState.Active,
        DeviceState.Disabled => AudioDeviceState.Disabled,
        DeviceState.NotPresent => AudioDeviceState.NotPresent,
        DeviceState.Unplugged => AudioDeviceState.Unplugged,
        _ => AudioDeviceState.Unknown
    };

    internal void RaiseDevicesChanged(string reason) =>
        DevicesChanged?.Invoke(this, new AudioDeviceChangedEventArgs(reason));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        }
        catch
        {
            // Ignore: the endpoint may already be gone.
        }

        _enumerator.Dispose();
    }
}
