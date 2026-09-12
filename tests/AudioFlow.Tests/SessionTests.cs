using AudioFlow.Models;
using AudioFlow.Session;
using Xunit;

namespace AudioFlow.Tests;

public class AudioFlowSessionTests : IDisposable
{
    private readonly string _dir;
    private readonly string _markerPath;

    public AudioFlowSessionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "af-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _markerPath = Path.Combine(_dir, "session.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class FakeBackend : IAudioRoutingBackend
    {
        public Dictionary<uint, string> Endpoints { get; } = new();
        public List<SessionProcessInfo> Processes { get; } = new();
        public string? DefaultDevice { get; set; } = "DEV_DEFAULT";
        public HashSet<string> ExistingDevices { get; } = new();
        public List<(uint Pid, bool Mute)> MuteCalls { get; } = new();
        public Dictionary<uint, string> SetCalls { get; } = new();
        public string? PolicyState { get; set; }
        public List<(string? Snapshot, List<string> Names)> RevertCalls { get; } = new();

        public string? GetPersistedEndpoint(uint processId, out string? error)
        {
            error = null;
            return Endpoints.TryGetValue(processId, out var device) ? device : null;
        }

        public bool SetPersistedEndpoint(uint processId, string deviceId, out string? error)
        {
            error = null;
            Endpoints[processId] = deviceId;
            SetCalls[processId] = deviceId;
            return true;
        }

        public string? GetDefaultRenderDeviceId() => DefaultDevice;

        public bool DeviceExists(string deviceId) =>
            ExistingDevices.Count == 0 ? !string.IsNullOrWhiteSpace(deviceId) : ExistingDevices.Contains(deviceId);

        public IReadOnlyList<SessionProcessInfo> GetActiveProcesses() => Processes;

        public int SetProcessMute(uint processId, bool mute)
        {
            MuteCalls.Add((processId, mute));
            return 1;
        }

        public string? CapturePolicyState() => PolicyState;

        public bool RevertPolicyState(string? snapshot, IReadOnlyList<string> executableNames)
        {
            RevertCalls.Add((snapshot, executableNames.ToList()));
            return true;
        }
    }

    private static ApplicationIdentity Identity(string key, string? name = null, string? pathHash = null) =>
        new() { Key = key, ProcessName = name ?? key, PathHash = pathHash };

    private AudioFlowSessionManager NewManager(FakeBackend backend) =>
        new(backend, new SessionMarker(_markerPath));

    [Fact]
    public void StartSession_WritesMarker()
    {
        var backend = new FakeBackend();
        var manager = NewManager(backend);

        Assert.True(manager.StartSession());
        Assert.Equal(SessionState.Active, manager.State);
        Assert.True(File.Exists(_markerPath));
        Assert.Equal("DEV_DEFAULT", manager.Snapshot!.DefaultRenderDeviceId);
    }

    [Fact]
    public void ApplyRoute_CapturesOriginal_AndWritesMarkerBeforeSet()
    {
        var backend = new FakeBackend();
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe", "Spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();

        Assert.True(manager.ApplyRoute(Identity("exe:spotify.exe", "Spotify.exe"), 100, "DEV_SPEAKERS", out _));

        var entry = manager.Snapshot!.Applications.Single();
        Assert.Equal("DEV_ORIGINAL", entry.OriginalDeviceId);
        Assert.True(entry.OriginalCaptured);
        Assert.True(entry.HadOverride);
        Assert.Equal("DEV_SPEAKERS", backend.SetCalls[100]);
    }

    [Fact]
    public void EndSession_RestoresOriginal_Exact()
    {
        var backend = new FakeBackend();
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        var report = manager.EndSession();

        Assert.True(report.Success);
        Assert.Equal(RestoreStatus.Exact, report.Results.Single().Status);
        Assert.Equal("DEV_ORIGINAL", backend.Endpoints[100]);
        Assert.False(File.Exists(_markerPath));
        Assert.Equal(SessionState.Inactive, manager.State);
    }

    [Fact]
    public void EndSession_NoOriginal_RestoresFallbackToDefault()
    {
        var backend = new FakeBackend { DefaultDevice = "DEV_DEFAULT" };
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:chrome.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:chrome.exe"), 100, "DEV_HEADPHONES", out _);

        var report = manager.EndSession();

        Assert.True(report.Success);
        Assert.Equal(RestoreStatus.Fallback, report.Results.Single().Status);
        Assert.Equal("DEV_DEFAULT", backend.Endpoints[100]);
    }

    [Fact]
    public void EndSession_IsIdempotent()
    {
        var backend = new FakeBackend();
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        var first = manager.EndSession();
        var second = manager.EndSession();
        var third = manager.EndSession();

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(third.Success);
    }

    [Fact]
    public void Restore_MatchesByStableIdentity_AcrossPidChange()
    {
        var backend = new FakeBackend();
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        // Spotify restarted with a new PID before restore.
        backend.Processes.Clear();
        backend.Processes.Add(new SessionProcessInfo(200, Identity("exe:spotify.exe")));

        var report = manager.EndSession();

        Assert.True(report.Success);
        Assert.Equal("DEV_ORIGINAL", backend.Endpoints[200]);
    }

    [Fact]
    public void Restore_DoesNotTouchUnrelatedApplications()
    {
        var backend = new FakeBackend();
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));
        backend.Processes.Add(new SessionProcessInfo(999, Identity("exe:unrelated.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        manager.EndSession();

        Assert.DoesNotContain(999u, backend.SetCalls.Keys);
    }

    [Fact]
    public void Restore_UnmutesMutedProcesses()
    {
        var backend = new FakeBackend();
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);
        manager.RecordMute(100);

        manager.EndSession();

        Assert.Contains(backend.MuteCalls, c => c.Pid == 100 && c.Mute == false);
    }

    [Fact]
    public void Restore_ApplicationNotRunning_ReportsFailedAndKeepsMarker()
    {
        var backend = new FakeBackend();
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        backend.Processes.Clear(); // app closed

        var report = manager.EndSession();

        Assert.False(report.Success);
        Assert.Equal(RestoreStatus.Failed, report.Results.Single().Status);
        Assert.True(File.Exists(_markerPath));
        Assert.Equal(SessionState.Stale, manager.State);
    }

    [Fact]
    public void RecoverIfNeeded_RestoresStaleSession()
    {
        var backend = new FakeBackend();
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        // Simulate a crashed session: write a marker manually.
        var marker = new SessionMarker(_markerPath);
        var snapshot = new AudioRoutingSnapshot
        {
            DefaultRenderDeviceId = "DEV_DEFAULT",
            Applications =
            {
                new AudioApplicationSnapshot
                {
                    ApplicationIdentifier = "exe:spotify.exe",
                    ExecutableName = "spotify.exe",
                    OriginalDeviceId = "DEV_ORIGINAL",
                    OriginalCaptured = true,
                    ProcessIds = { 100 }
                }
            }
        };
        marker.Write(snapshot);

        var manager = NewManager(backend);
        var recovery = manager.RecoverIfNeeded();

        Assert.True(recovery.HadStaleSession);
        Assert.True(recovery.Restore.Success);
        Assert.Equal("DEV_ORIGINAL", backend.Endpoints[100]);
        Assert.False(File.Exists(_markerPath));
        Assert.Equal(SessionState.Recovered, manager.State);
    }

    [Fact]
    public void RecoverIfNeeded_NoMarker_IsNoOp()
    {
        var backend = new FakeBackend();
        var manager = NewManager(backend);

        var recovery = manager.RecoverIfNeeded();

        Assert.False(recovery.HadStaleSession);
        Assert.Equal(SessionState.Inactive, manager.State);
    }

    [Fact]
    public void SessionState_ReportsStaleWhenMarkerExists()
    {
        var marker = new SessionMarker(_markerPath);
        marker.Write(new AudioRoutingSnapshot { DefaultRenderDeviceId = "X" });

        var manager = NewManager(new FakeBackend());

        Assert.True(manager.HasStaleSession);
    }

    [Fact]
    public void HandleDeviceLost_RestoresAffectedOnly_Exact()
    {
        var backend = new FakeBackend { DefaultDevice = "DEV_DEFAULT" };
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Endpoints[200] = "DEV_OTHER";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));
        backend.Processes.Add(new SessionProcessInfo(200, Identity("exe:discord.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);
        manager.ApplyRoute(Identity("exe:discord.exe"), 200, "DEV_HEADPHONES", out _);

        var report = manager.HandleDeviceLost("DEV_SPEAKERS");

        Assert.Equal(1, report.AffectedCount);
        Assert.Equal(RestoreStatus.Exact, report.Results.Single().Status);
        Assert.Equal("DEV_ORIGINAL", backend.Endpoints[100]);
        Assert.Equal("DEV_HEADPHONES", backend.Endpoints[200]); // untouched
    }

    [Fact]
    public void HandleDeviceLost_OriginalGone_FallsBackToDefault()
    {
        var backend = new FakeBackend { DefaultDevice = "DEV_DEFAULT" };
        backend.Endpoints[100] = "DEV_ORIGINAL";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        // The original device disappears too.
        backend.ExistingDevices.Add("DEV_DEFAULT");

        var report = manager.HandleDeviceLost("DEV_SPEAKERS");

        Assert.Equal(RestoreStatus.Fallback, report.Results.Single().Status);
        Assert.Equal("DEV_DEFAULT", backend.Endpoints[100]);
    }

    [Fact]
    public void HandleDeviceLost_NoAffected_ReturnsEmptyReport()
    {
        var backend = new FakeBackend();
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);

        var report = manager.HandleDeviceLost("DEV_OTHER");

        Assert.Equal(0, report.AffectedCount);
    }

    [Fact]
    public void RestoreApplication_OnlyTouchesNamedApp()
    {
        var backend = new FakeBackend();
        backend.Endpoints[100] = "DEV_ORIGINAL_A";
        backend.Endpoints[200] = "DEV_ORIGINAL_B";
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe")));
        backend.Processes.Add(new SessionProcessInfo(200, Identity("exe:discord.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe"), 100, "DEV_SPEAKERS", out _);
        manager.ApplyRoute(Identity("exe:discord.exe"), 200, "DEV_HEADPHONES", out _);

        var result = manager.RestoreApplication("exe:spotify.exe");

        Assert.Equal(RestoreStatus.Exact, result.Status);
        Assert.Equal("DEV_ORIGINAL_A", backend.Endpoints[100]);
        Assert.Equal("DEV_HEADPHONES", backend.Endpoints[200]); // discord still routed
    }

    [Fact]
    public void RestoreApplication_UnknownApp_IsSkipped()
    {
        var backend = new FakeBackend();
        var manager = NewManager(backend);
        manager.StartSession();

        var result = manager.RestoreApplication("exe:notmodified.exe");

        Assert.Equal(RestoreStatus.Skipped, result.Status);
    }

    [Fact]
    public void StartSession_CapturesPolicyState()
    {
        var backend = new FakeBackend { PolicyState = "{\"keys\":[]}" };
        var manager = NewManager(backend);

        manager.StartSession();

        Assert.Equal("{\"keys\":[]}", manager.Snapshot!.PolicyState);
    }

    [Fact]
    public void EndSession_WithPolicyState_CleansUpEvenWhenAppNotRunning()
    {
        var backend = new FakeBackend { PolicyState = "{\"keys\":[]}" };
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe", "Spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe", "Spotify.exe"), 100, "DEV_SPEAKERS", out _);

        backend.Processes.Clear(); // app closed before exit

        var report = manager.EndSession();

        Assert.True(report.Success);
        Assert.False(File.Exists(_markerPath));
        Assert.Equal(SessionState.Inactive, manager.State);
        Assert.Contains(backend.RevertCalls, c => c.Names.Contains("Spotify.exe"));
    }

    [Fact]
    public void HandleDeviceLost_RevertsPolicyStateForAffectedApps()
    {
        var backend = new FakeBackend { DefaultDevice = "DEV_DEFAULT", PolicyState = "{\"keys\":[]}" };
        backend.Processes.Add(new SessionProcessInfo(100, Identity("exe:spotify.exe", "Spotify.exe")));

        var manager = NewManager(backend);
        manager.StartSession();
        manager.ApplyRoute(Identity("exe:spotify.exe", "Spotify.exe"), 100, "DEV_SPEAKERS", out _);

        manager.HandleDeviceLost("DEV_SPEAKERS");

        Assert.Contains(backend.RevertCalls, c => c.Names.Contains("Spotify.exe"));
    }
}
