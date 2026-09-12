using AudioFlow.Applications;
using AudioFlow.Core;
using AudioFlow.Core.WindowsAudio;

namespace AudioFlow.Session;

/// <summary>Windows implementation of <see cref="IAudioRoutingBackend"/>.</summary>
public sealed class WindowsAudioRoutingBackend : IAudioRoutingBackend, IDisposable
{
    private readonly AudioRoutingManager _routing = new();
    private readonly AudioSessionManager _sessions = new();
    private readonly AudioDeviceManager _devices = new();
    private readonly ApplicationIdentifier _identifier = new();

    public string? GetPersistedEndpoint(uint processId, out string? error) =>
        _routing.GetPersistedEndpoint(processId, out error);

    public bool SetPersistedEndpoint(uint processId, string deviceId, out string? error)
    {
        var result = _routing.Apply(processId, null, deviceId);
        error = result.Error;
        return result.Success;
    }

    public string? GetDefaultRenderDeviceId() => _devices.GetDefaultOutputDevice()?.Id;

    public bool DeviceExists(string deviceId)
    {
        try
        {
            return _devices.DeviceExists(deviceId);
        }
        catch
        {
            return false;
        }
    }

    public int SetProcessMute(uint processId, bool mute) => _sessions.SetProcessMute(processId, mute);

    public string? CapturePolicyState() => AudioPolicyRegistryGuard.Capture();

    public bool RevertPolicyState(string? snapshot, IReadOnlyList<string> executableNames) =>
        AudioPolicyRegistryGuard.Revert(snapshot, executableNames);

    public IReadOnlyList<SessionProcessInfo> GetActiveProcesses()
    {
        var result = new List<SessionProcessInfo>();
        var seen = new HashSet<uint>();

        foreach (var session in _sessions.GetSessions())
        {
            if (session.ProcessId == 0 || !seen.Add(session.ProcessId))
            {
                continue;
            }

            try
            {
                var identity = _identifier.Identify(session.ProcessId);
                result.Add(new SessionProcessInfo(session.ProcessId, identity));
            }
            catch
            {
                // Skip processes we cannot identify.
            }
        }

        return result;
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _devices.Dispose();
    }
}
