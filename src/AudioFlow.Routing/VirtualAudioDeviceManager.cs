using AudioFlow.Core;

namespace AudioFlow.Routing;

/// <summary>
/// Discovers render endpoints and classifies them as physical or virtual using
/// heuristics (never hardcoded device names). Also exposes the Windows default.
/// </summary>
public sealed class VirtualAudioDeviceManager : IDisposable
{
    // Tokens commonly used by virtual audio devices. Discovery is dynamic; we
    // never require an exact name.
    private static readonly string[] VirtualTokens =
    {
        "virtual", "cable", "vb-audio", "voicemeeter", "vban",
        "loopback", "null audio", "dummy", "audioflow",
        "steam streaming", "nvidia broadcast", "blackhole", "soundflower"
    };

    private AudioDeviceManager? _devices;

    public IReadOnlyList<AudioEndpointInfo> GetEndpoints()
    {
        var result = new List<AudioEndpointInfo>();

        try
        {
            _devices ??= new AudioDeviceManager();
            foreach (var device in _devices.GetOutputDevices(includeInactive: true))
            {
                result.Add(new AudioEndpointInfo(
                    device.Id,
                    device.FriendlyName,
                    device.DeviceFriendlyName ?? "Unknown",
                    device.State,
                    LooksVirtual(device.FriendlyName),
                    device.IsDefault));
            }
        }
        catch
        {
            // No audio subsystem (e.g. non-Windows): report no endpoints.
        }

        return result;
    }

    public IReadOnlyList<AudioEndpointInfo> GetPhysicalEndpoints() =>
        GetEndpoints().Where(e => !e.IsVirtual).ToList();

    public IReadOnlyList<AudioEndpointInfo> GetVirtualEndpoints() =>
        GetEndpoints().Where(e => e.IsVirtual).ToList();

    /// <summary>The first active virtual render endpoint, if any.</summary>
    public AudioEndpointInfo? GetVirtualRenderEndpoint() =>
        GetVirtualEndpoints().FirstOrDefault(e => e.IsAvailable);

    public bool HasVirtualEndpoint => GetVirtualRenderEndpoint() is not null;

    public static bool LooksVirtual(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            return false;
        }

        var name = friendlyName.ToLowerInvariant();
        return VirtualTokens.Any(token => name.Contains(token, StringComparison.Ordinal));
    }

    public void Dispose() => _devices?.Dispose();
}
