using AudioFlow.Models;
using AudioFlow.Rules;
using Xunit;

namespace AudioFlow.Tests;

public class RuleStorageTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public RuleStorageTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "audioflow-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "rules.json");
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
    public void SaveThenLoad_RoundTripsRules()
    {
        var storage = new RuleStorage(_file);
        var set = new AudioRuleSet
        {
            DefaultOutputDeviceId = "DEV_HEADPHONES",
            Rules =
            {
                new AudioRule
                {
                    ApplicationIdentifier = "exe:spotify.exe",
                    ApplicationName = "Spotify",
                    OutputDeviceId = "DEV_SPEAKERS"
                }
            }
        };

        Assert.True(storage.Save(set));

        var loaded = new RuleStorage(_file).Load();

        Assert.Equal("DEV_HEADPHONES", loaded.DefaultOutputDeviceId);
        Assert.Single(loaded.Rules);
        Assert.Equal("exe:spotify.exe", loaded.Rules[0].ApplicationIdentifier);
        Assert.Equal("DEV_SPEAKERS", loaded.Rules[0].OutputDeviceId);
    }

    [Fact]
    public void MissingFile_ReturnsEmptySet()
    {
        var storage = new RuleStorage(Path.Combine(_dir, "does-not-exist.json"));

        var loaded = storage.Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Rules);
        Assert.Null(storage.LastError);
    }

    [Fact]
    public void EmptyFile_ReturnsEmptySet()
    {
        File.WriteAllText(_file, string.Empty);

        var loaded = new RuleStorage(_file).Load();

        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void InvalidJson_ReturnsEmptySetAndRecordsError()
    {
        File.WriteAllText(_file, "{ this is not json ]");

        var storage = new RuleStorage(_file);
        var loaded = storage.Load();

        Assert.Empty(loaded.Rules);
        Assert.NotNull(storage.LastError);
    }

    [Fact]
    public void CorruptJson_CanBeRecoveredBySaving()
    {
        File.WriteAllText(_file, "###corrupt###");

        var storage = new RuleStorage(_file);
        storage.Load();

        var recovered = new AudioRuleSet { DefaultOutputDeviceId = "DEV_HEADPHONES" };
        Assert.True(storage.Save(recovered));

        var loaded = new RuleStorage(_file).Load();
        Assert.Equal("DEV_HEADPHONES", loaded.DefaultOutputDeviceId);
    }

    [Fact]
    public void AtomicSave_LeavesNoTempFile()
    {
        var storage = new RuleStorage(_file);
        storage.Save(new AudioRuleSet { DefaultOutputDeviceId = "A" });
        storage.Save(new AudioRuleSet { DefaultOutputDeviceId = "B" });

        Assert.True(File.Exists(_file));
        Assert.False(File.Exists(_file + ".tmp"));
        Assert.Equal("B", new RuleStorage(_file).Load().DefaultOutputDeviceId);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var nested = Path.Combine(_dir, "nested", "deep", "rules.json");
        var storage = new RuleStorage(nested);

        Assert.True(storage.Save(new AudioRuleSet { DefaultOutputDeviceId = "X" }));
        Assert.True(File.Exists(nested));
    }
}
