using AudioFlow.Applications;
using Xunit;

namespace AudioFlow.Tests;

public class ApplicationIdentifierTests
{
    private readonly ApplicationIdentifier _identifier = new();

    private static ProcessDetails Details(uint pid, string? name, string? path) =>
        new(pid, name, path, Description: null, IsAccessible: true);

    [Fact]
    public void SameExecutable_DifferentProcessIds_HaveSameKey()
    {
        var first = _identifier.Identify(Details(1234, "Spotify.exe", @"C:\Apps\Spotify\Spotify.exe"));
        var second = _identifier.Identify(Details(9876, "Spotify.exe", @"C:\Apps\Spotify\Spotify.exe"));

        Assert.Equal(first.Key, second.Key);
        Assert.Equal("exe:spotify.exe", first.Key);
    }

    [Fact]
    public void DifferentExecutables_HaveDifferentKeys()
    {
        var spotify = _identifier.Identify(Details(1, "Spotify.exe", @"C:\Apps\Spotify\Spotify.exe"));
        var chrome = _identifier.Identify(Details(2, "chrome.exe", @"C:\Program Files\Google\chrome.exe"));

        Assert.NotEqual(spotify.Key, chrome.Key);
    }

    [Fact]
    public void PathHash_IsComputedAndStableForSamePath()
    {
        var a = _identifier.Identify(Details(1, "game.exe", @"C:\Games\A\game.exe"));
        var b = _identifier.Identify(Details(2, "game.exe", @"C:\Games\A\game.exe"));
        var c = _identifier.Identify(Details(3, "game.exe", @"C:\Games\B\game.exe"));

        Assert.NotNull(a.PathHash);
        Assert.Equal(a.PathHash, b.PathHash);
        Assert.NotEqual(a.PathHash, c.PathHash);
    }

    [Fact]
    public void UnknownApplication_FallsBackToProcessIdKey()
    {
        var identity = _identifier.Identify(new ProcessDetails(4321, null, null, null, false));

        Assert.Equal("pid:4321", identity.Key);
    }

    [Fact]
    public void CaseInsensitiveExecutableName_SameKey()
    {
        var lower = _identifier.Identify(Details(1, "spotify.exe", null));
        var upper = _identifier.Identify(Details(2, "Spotify.EXE", null));

        Assert.Equal(lower.Key, upper.Key);
    }

    [Fact]
    public void Win32Application_HasNoAumidOnNonPackagedProcess()
    {
        var identity = _identifier.Identify(Details(1, "notepad.exe", @"C:\Windows\notepad.exe"));

        // Notepad is not a packaged app; AUMID must be null and kind Win32.
        Assert.Null(identity.Aumid);
        Assert.Equal(AudioFlow.Models.ApplicationKind.Win32, identity.Kind);
    }
}
