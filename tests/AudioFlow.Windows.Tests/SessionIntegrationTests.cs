using AudioFlow.Session;
using Xunit;
using Xunit.Abstractions;

namespace AudioFlow.Windows.Tests;

public class SessionIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public SessionIntegrationTests(ITestOutputHelper output) => _output = output;

    private static SessionMarker TempMarker() =>
        new(Path.Combine(Path.GetTempPath(), "af-win-session-" + Guid.NewGuid().ToString("N") + ".json"));

    [WindowsFact]
    public void HandleDeviceLost_UnknownDevice_DoesNotThrow()
    {
        using var backend = new WindowsAudioRoutingBackend();
        var manager = new AudioFlowSessionManager(backend, TempMarker());

        var report = manager.HandleDeviceLost("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}");

        _output.WriteLine($"affected={report.AffectedCount}");
        Assert.Equal(0, report.AffectedCount);
    }

    [WindowsFact]
    public void RestoreApplication_NoSession_IsSkipped()
    {
        using var backend = new WindowsAudioRoutingBackend();
        var manager = new AudioFlowSessionManager(backend, TempMarker());

        var result = manager.RestoreApplication("exe:notmodified.exe");

        _output.WriteLine($"status={result.Status}");
        Assert.Equal(RestoreStatus.Skipped, result.Status);
    }

    [WindowsFact]
    public void StartSession_WritesMarker_AndEndSession_DeletesIt()
    {
        var marker = TempMarker();
        using var backend = new WindowsAudioRoutingBackend();
        var manager = new AudioFlowSessionManager(backend, marker);

        Assert.True(manager.StartSession());
        Assert.True(marker.Exists);

        manager.EndSession();

        Assert.False(marker.Exists);
        Assert.Equal(SessionState.Inactive, manager.State);
    }
}
