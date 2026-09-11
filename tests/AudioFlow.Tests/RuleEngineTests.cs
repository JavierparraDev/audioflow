using AudioFlow.Models;
using AudioFlow.Rules;
using Xunit;

namespace AudioFlow.Tests;

public class AudioRuleTests
{
    [Fact]
    public void NewRule_IsEnabledByDefault_AndHasId()
    {
        var rule = new AudioRule
        {
            ApplicationIdentifier = "exe:spotify.exe",
            ApplicationName = "Spotify",
            OutputDeviceId = "speakers"
        };

        Assert.True(rule.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(rule.RuleId));
    }

    [Fact]
    public void TwoRules_HaveDifferentIds()
    {
        var a = new AudioRule();
        var b = new AudioRule();

        Assert.NotEqual(a.RuleId, b.RuleId);
    }

    [Fact]
    public void Rule_Defaults_AreEmptyStrings()
    {
        var rule = new AudioRule();

        Assert.Equal(string.Empty, rule.ApplicationIdentifier);
        Assert.Equal(string.Empty, rule.OutputDeviceId);
        Assert.Null(rule.PathHash);
    }
}

public class RuleEngineTests
{
    private const string Speakers = "DEV_SPEAKERS";
    private const string Headphones = "DEV_HEADPHONES";

    private static RuleEngine NewEngine(out RuleStorage storage, string? file = null)
    {
        storage = new RuleStorage(file ?? Path.Combine(Path.GetTempPath(), "audioflow-tests", Guid.NewGuid() + ".json"));
        var engine = new RuleEngine(storage);
        engine.SetDefaultDevice(Headphones);
        return engine;
    }

    [Fact]
    public void Spotify_WithRule_GoesToSpeakers()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:spotify.exe", "Spotify", Speakers);

        var resolution = engine.Resolve("exe:spotify.exe");

        Assert.True(resolution.HasExplicitRule);
        Assert.Equal(Speakers, resolution.OutputDeviceId);
    }

    [Fact]
    public void UnknownApplication_GoesToDefault()
    {
        var engine = NewEngine(out _);

        var resolution = engine.Resolve("exe:unknown.exe");

        Assert.False(resolution.HasExplicitRule);
        Assert.Equal(Headphones, resolution.OutputDeviceId);
    }

    [Fact]
    public void Discord_WithoutRule_GoesToHeadphones()
    {
        var engine = NewEngine(out _);

        Assert.Equal(Headphones, engine.Resolve("exe:discord.exe").OutputDeviceId);
    }

    [Fact]
    public void Chrome_WithoutRule_GoesToHeadphones()
    {
        var engine = NewEngine(out _);

        Assert.Equal(Headphones, engine.Resolve("exe:chrome.exe").OutputDeviceId);
    }

    [Fact]
    public void DisabledRule_FallsBackToDefault()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:spotify.exe", "Spotify", Speakers);
        engine.SetRuleEnabled("exe:spotify.exe", false);

        var resolution = engine.Resolve("exe:spotify.exe");

        Assert.False(resolution.HasExplicitRule);
        Assert.Equal(Headphones, resolution.OutputDeviceId);
    }

    [Fact]
    public void MultipleRules_ResolveIndependently()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:spotify.exe", "Spotify", Speakers);
        engine.SetRule("exe:discord.exe", "Discord", "DEV_MIC");

        Assert.Equal(Speakers, engine.Resolve("exe:spotify.exe").OutputDeviceId);
        Assert.Equal("DEV_MIC", engine.Resolve("exe:discord.exe").OutputDeviceId);
        Assert.Equal(Headphones, engine.Resolve("exe:chrome.exe").OutputDeviceId);
    }

    [Fact]
    public void PathHash_IsUsedAsSecondaryMatch()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:game.exe", "Game A", Speakers, pathHash: "ABCD1234");

        // Same file name but different install: matches by path hash.
        var resolution = engine.Resolve("exe:game.exe", "ABCD1234");
        Assert.Equal(Speakers, resolution.OutputDeviceId);
    }

    [Fact]
    public void RemoveRule_RevertsToDefault()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:spotify.exe", "Spotify", Speakers);

        Assert.True(engine.RemoveRule("exe:spotify.exe"));
        Assert.Equal(Headphones, engine.Resolve("exe:spotify.exe").OutputDeviceId);
    }

    [Fact]
    public void AudioLock_BlocksNonAllowedApplicationOnLockedDevice()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:fxsound.exe", "FxSound", Speakers);
        engine.EnableAudioLock(Speakers, Headphones, new[] { "exe:spotify.exe" });

        var blocked = engine.Resolve("exe:fxsound.exe");

        Assert.True(blocked.BlockedByAudioLock);
        Assert.Equal(Headphones, blocked.OutputDeviceId);
    }

    [Fact]
    public void AudioLock_AllowsListedApplicationOnLockedDevice()
    {
        var engine = NewEngine(out _);
        engine.SetRule("exe:spotify.exe", "Spotify", Speakers);
        engine.EnableAudioLock(Speakers, Headphones, new[] { "exe:spotify.exe" });

        var allowed = engine.Resolve("exe:spotify.exe");

        Assert.False(allowed.BlockedByAudioLock);
        Assert.Equal(Speakers, allowed.OutputDeviceId);
    }
}
