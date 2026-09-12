using System.Net.Http;
using AudioFlow.Configuration;
using AudioFlow.Updates;
using Xunit;

namespace AudioFlow.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.2.0", 0, 2, 0)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3+build.5", 1, 2, 3)]
    [InlineData("1.2.3-beta.1", 1, 2, 3)]
    public void Parse_ValidVersions(string text, int major, int minor, int patch)
    {
        var version = AppVersion.Parse(text);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    public void TryParse_InvalidVersions_ReturnFalse(string text)
    {
        Assert.False(AppVersion.TryParse(text, out _));
    }

    [Fact]
    public void Compare_OrdersNumerically()
    {
        Assert.True(AppVersion.Parse("0.3.0").CompareTo(AppVersion.Parse("0.2.9")) > 0);
        Assert.True(AppVersion.Parse("1.0.0").CompareTo(AppVersion.Parse("0.9.9")) > 0);
        Assert.Equal(0, AppVersion.Parse("0.2.0").CompareTo(AppVersion.Parse("0.2.0")));
    }

    [Fact]
    public void Prerelease_SortsBeforeRelease()
    {
        var pre = AppVersion.Parse("0.3.0-beta.1");
        var stable = AppVersion.Parse("0.3.0");

        Assert.True(pre.IsPreRelease);
        Assert.True(pre.CompareTo(stable) < 0);
        Assert.True(stable.CompareTo(pre) > 0);
    }

    [Fact]
    public void Parse_KeepsPrereleaseTag()
    {
        Assert.Equal("0.3.0-beta.1", AppVersion.Parse("v0.3.0-beta.1").ToString());
    }
}

public class UpdateServiceTests
{
    private sealed class FakeReleaseSource : IReleaseSource
    {
        public ReleaseInfo? Release;
        public Exception? Throw;
        public int Calls;

        public Task<ReleaseInfo?> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
        {
            Calls++;
            if (Throw is not null)
            {
                return Task.FromException<ReleaseInfo?>(Throw);
            }

            return Task.FromResult(Release);
        }
    }

    private static ReleaseInfo Release(string version, bool pre = false) =>
        new(AppVersion.Parse(version), "v" + version, "AudioFlow " + version, "notes",
            "https://example.invalid/release", DateTimeOffset.UtcNow, pre,
            new List<UpdateAsset> { new($"AudioFlow-Setup-v{version}.exe", "https://example.invalid/setup.exe", 10) });

    [Fact]
    public async Task SameVersion_IsUpToDate()
    {
        var source = new FakeReleaseSource { Release = Release("0.2.0") };
        using var service = new UpdateService(source, AppVersion.Parse("0.2.0"));

        var result = await service.CheckAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task NewerVersion_IsUpdateAvailable()
    {
        var source = new FakeReleaseSource { Release = Release("0.3.0") };
        using var service = new UpdateService(source, AppVersion.Parse("0.2.0"));

        var result = await service.CheckAsync(force: true);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.True(result.UpdateAvailable);
        Assert.Equal(AppVersion.Parse("0.3.0"), result.Latest);
    }

    [Fact]
    public async Task NewerPrerelease_IsNotAnUpdateOverStable()
    {
        var source = new FakeReleaseSource { Release = Release("0.3.0-beta.1", pre: true) };
        using var service = new UpdateService(source, AppVersion.Parse("0.3.0"));

        var result = await service.CheckAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task NoRelease_ReturnsNoReleaseFound()
    {
        var source = new FakeReleaseSource { Release = null };
        using var service = new UpdateService(source, AppVersion.Parse("0.2.0"));

        var result = await service.CheckAsync(force: true);

        Assert.Equal(UpdateStatus.NoReleaseFound, result.Status);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsFailed_AndDoesNotThrow()
    {
        var source = new FakeReleaseSource { Throw = new HttpRequestException("offline") };
        using var service = new UpdateService(source, AppVersion.Parse("0.2.0"));

        var result = await service.CheckAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Equal("Unable to check for updates.", result.Message);
    }

    [Fact]
    public async Task RecentCheck_IsNotRepeated_WhenNotForced()
    {
        var source = new FakeReleaseSource { Release = Release("0.2.0") };
        using var service = new UpdateService(source, AppVersion.Parse("0.2.0"), UpdateChannel.Stable, TimeSpan.FromHours(12));

        await service.CheckAsync(force: true);
        await service.CheckAsync(DateTimeOffset.UtcNow, force: false);

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public void ParseChannel_RecognizesChannels()
    {
        Assert.Equal(UpdateChannel.Beta, UpdateService.ParseChannel("beta"));
        Assert.Equal(UpdateChannel.Nightly, UpdateService.ParseChannel("nightly"));
        Assert.Equal(UpdateChannel.Stable, UpdateService.ParseChannel(null));
        Assert.Equal(UpdateChannel.Stable, UpdateService.ParseChannel("whatever"));
    }

    [Fact]
    public void Release_FindsInstallerAndPortableAssets()
    {
        var release = Release("0.3.0");

        Assert.NotNull(release.Installer);
        Assert.True(release.Installer!.IsInstaller);
    }
}

public class ChecksumTests : IDisposable
{
    private readonly string _file;

    public ChecksumTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "af-checksum-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllText(_file, "AudioFlow test payload");
    }

    public void Dispose()
    {
        try { File.Delete(_file); } catch { }
    }

    [Fact]
    public void Verify_AcceptsCorrectHash()
    {
        var hash = Checksum.ComputeSha256(_file);
        Assert.True(Checksum.Verify(_file, hash));
        Assert.True(Checksum.Verify(_file, hash.ToLowerInvariant()));
    }

    [Fact]
    public void Verify_RejectsWrongHash_AndMissingFile()
    {
        Assert.False(Checksum.Verify(_file, "DEADBEEF"));
        Assert.False(Checksum.Verify(_file + ".missing", "DEADBEEF"));
        Assert.False(Checksum.Verify(_file, ""));
    }

    [Fact]
    public void FindInSumsFile_ParsesBothFormats()
    {
        const string content = "ABC123  AudioFlow-Setup-v0.3.0.exe\nDEF456 *AudioFlow-v0.3.0-win-x64.zip\n";

        Assert.Equal("ABC123", Checksum.FindInSumsFile(content, "AudioFlow-Setup-v0.3.0.exe"));
        Assert.Equal("DEF456", Checksum.FindInSumsFile(content, "AudioFlow-v0.3.0-win-x64.zip"));
        Assert.Null(Checksum.FindInSumsFile(content, "other.exe"));
    }
}

public class ConfigurationTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public ConfigurationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "af-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Migrator_SetsSchemaVersion()
    {
        var settings = new AppSettings { ConfigVersion = 0 };

        var migrated = ConfigMigrator.Migrate(settings, out var changed);

        Assert.True(changed);
        Assert.Equal(ConfigMigrator.CurrentVersion, migrated.ConfigVersion);
    }

    [Fact]
    public void Migrator_DoesNotChangeCurrentVersion()
    {
        var settings = new AppSettings { ConfigVersion = ConfigMigrator.CurrentVersion };

        ConfigMigrator.Migrate(settings, out var changed);

        Assert.False(changed);
    }

    [Fact]
    public void SettingsStore_RoundTrips()
    {
        var store = new SettingsStore(_file);
        var settings = new AppSettings { Language = "es", CheckForUpdates = false, StartWithWindows = true };

        Assert.True(store.Save(settings));

        var loaded = new SettingsStore(_file).Load();
        Assert.Equal("es", loaded.Language);
        Assert.False(loaded.CheckForUpdates);
        Assert.True(loaded.StartWithWindows);
    }

    [Fact]
    public void SettingsStore_InvalidJson_ReturnsDefaults()
    {
        File.WriteAllText(_file, "{ not valid json ]");

        var store = new SettingsStore(_file);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.NotNull(store.LastError);
    }

    [Fact]
    public void UpdateManifest_ParsesValidJson()
    {
        const string json = """
        { "version": "0.3.0", "channel": "stable", "installer": "setup.exe", "portable": "portable.zip", "sha256": "ABC" }
        """;

        var manifest = UpdateManifest.Parse(json);

        Assert.NotNull(manifest);
        Assert.Equal("0.3.0", manifest!.Version);
        Assert.Equal("stable", manifest.Channel);
        Assert.Equal("ABC", manifest.Sha256);
    }

    [Fact]
    public void UpdateManifest_InvalidJson_ReturnsNull()
    {
        Assert.Null(UpdateManifest.Parse("{ not json"));
    }
}
