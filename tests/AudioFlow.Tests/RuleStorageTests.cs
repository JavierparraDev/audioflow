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
    public void Save_DoesNotCreateFile()
    {
        var storage = new RuleStorage(_file);

        Assert.True(storage.Save(new AudioRuleSet { DefaultOutputDeviceId = "DEV_HEADPHONES" }));

        Assert.False(File.Exists(_file));
        Assert.False(File.Exists(_file + ".tmp"));
        Assert.False(File.Exists(_file + ".bak"));
    }

    [Fact]
    public void Load_AlwaysReturnsEmptySet_EvenWhenLegacyFileExists()
    {
        File.WriteAllText(_file, """
            {
              "defaultOutputDeviceId": "DEV_HEADPHONES",
              "rules": [
                { "applicationIdentifier": "exe:spotify.exe", "outputDeviceId": "DEV_SPEAKERS" }
              ]
            }
            """);

        var loaded = new RuleStorage(_file).Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Rules);
        Assert.Null(loaded.DefaultOutputDeviceId);
        Assert.Null(new RuleStorage(_file).LastError);
    }

    [Fact]
    public void DeleteLegacyFiles_RemovesRulesAndSiblings()
    {
        File.WriteAllText(_file, "{}");
        File.WriteAllText(_file + ".tmp", "{}");
        File.WriteAllText(_file + ".bak", "{}");

        var removed = new RuleStorage(_file).DeleteLegacyFiles();

        Assert.Equal(3, removed);
        Assert.False(File.Exists(_file));
        Assert.False(File.Exists(_file + ".tmp"));
        Assert.False(File.Exists(_file + ".bak"));
    }

    [Fact]
    public void DeleteLegacyFiles_MissingFiles_ReturnsZero()
    {
        var removed = new RuleStorage(Path.Combine(_dir, "does-not-exist.json")).DeleteLegacyFiles();

        Assert.Equal(0, removed);
    }
}
