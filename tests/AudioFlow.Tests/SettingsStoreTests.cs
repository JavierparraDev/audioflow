using AudioFlow.Configuration;
using Xunit;

namespace AudioFlow.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "audioflow-tests", Guid.NewGuid().ToString("N"));
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(_file).Load();

        Assert.Equal(ConfigMigrator.CurrentVersion, settings.ConfigVersion);
        Assert.Equal("en", settings.Language);
        Assert.True(settings.CheckForUpdates);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_file, "   ");

        var settings = new SettingsStore(_file).Load();

        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var store = new SettingsStore(_file);
        var saved = new AppSettings
        {
            Language = "de",
            CheckForUpdates = false,
            CloseCompletely = true,
            UpdateChannel = "beta",
            SkippedVersion = "0.3.0"
        };

        Assert.True(store.Save(saved));

        var loaded = new SettingsStore(_file).Load();

        Assert.Equal("de", loaded.Language);
        Assert.False(loaded.CheckForUpdates);
        Assert.True(loaded.CloseCompletely);
        Assert.Equal("beta", loaded.UpdateChannel);
        Assert.Equal("0.3.0", loaded.SkippedVersion);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var nested = Path.Combine(_dir, "nested", "settings.json");

        Assert.True(new SettingsStore(nested).Save(new AppSettings()));
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFile()
    {
        var store = new SettingsStore(_file);

        Assert.True(store.Save(new AppSettings()));

        Assert.False(File.Exists(_file + ".tmp"));
    }

    [Fact]
    public void Load_InvalidJson_ReportsErrorAndReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_file, "{ this is not json");

        var store = new SettingsStore(_file);
        var settings = store.Load();

        Assert.NotNull(store.LastError);
        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public void Load_MigratesLegacyConfigVersion_AndPersistsIt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_file, """{"configVersion":0,"language":"es"}""");

        var settings = new SettingsStore(_file).Load();

        Assert.Equal(ConfigMigrator.CurrentVersion, settings.ConfigVersion);
        Assert.Equal("es", settings.Language);
        Assert.Contains("\"configVersion\": 1", File.ReadAllText(_file));
    }
}

public class ConfigMigratorTests
{
    [Fact]
    public void Migrate_LegacyVersion_SetsCurrentVersionAndReportsChange()
    {
        var settings = new AppSettings { ConfigVersion = 0 };

        var result = ConfigMigrator.Migrate(settings, out var changed);

        Assert.True(changed);
        Assert.Equal(ConfigMigrator.CurrentVersion, result.ConfigVersion);
    }

    [Fact]
    public void Migrate_CurrentVersion_ReportsNoChange()
    {
        var settings = new AppSettings { ConfigVersion = ConfigMigrator.CurrentVersion };

        ConfigMigrator.Migrate(settings, out var changed);

        Assert.False(changed);
    }

    [Fact]
    public void Migrate_NewerVersion_IsKeptUntouched()
    {
        var settings = new AppSettings { ConfigVersion = ConfigMigrator.CurrentVersion + 5 };

        var result = ConfigMigrator.Migrate(settings, out var changed);

        Assert.False(changed);
        Assert.Equal(ConfigMigrator.CurrentVersion + 5, result.ConfigVersion);
    }
}
