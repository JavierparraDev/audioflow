namespace AudioFlow.Updates;

public enum UpdateChannel
{
    Stable,
    Beta,
    Nightly
}

public enum UpdateStatus
{
    Unknown,
    UpToDate,
    UpdateAvailable,
    NoReleaseFound,
    Failed
}

/// <summary>Metadata about a downloadable release asset.</summary>
public sealed record UpdateAsset(string Name, string DownloadUrl, long Size)
{
    public bool IsInstaller =>
        Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public bool IsPortable =>
        Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A GitHub release resolved to a version and its assets.</summary>
public sealed record ReleaseInfo(
    AppVersion Version,
    string Tag,
    string Name,
    string Body,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    bool IsPreRelease,
    IReadOnlyList<UpdateAsset> Assets)
{
    public UpdateAsset? FindAsset(string suffix) =>
        Assets.FirstOrDefault(a => a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    public UpdateAsset? Installer => FindAsset("-Setup-v" + Version + ".exe") ?? FindAsset("-Setup.exe") ?? FindAsset(".exe");

    public UpdateAsset? Portable => FindAsset("-win-x64.zip") ?? FindAsset(".zip");
}

/// <summary>Result of an update check.</summary>
public sealed record UpdateCheckResult(
    UpdateStatus Status,
    AppVersion Current,
    AppVersion? Latest,
    ReleaseInfo? Release,
    string Message,
    DateTimeOffset CheckedAt)
{
    public bool UpdateAvailable => Status == UpdateStatus.UpdateAvailable;
}

/// <summary>Parsed release.json metadata (optional asset in a release).</summary>
public sealed class UpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public DateTimeOffset PublishedAt { get; set; }
    public string? Installer { get; set; }
    public string? Portable { get; set; }
    public string? Sha256 { get; set; }

    public static UpdateManifest? Parse(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<UpdateManifest>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Abstraction over the release provider (GitHub by default).</summary>
public interface IReleaseSource
{
    Task<ReleaseInfo?> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken);
}

/// <summary>Repository defaults. Centralized so no URL is duplicated.</summary>
public static class UpdateDefaults
{
    public const string Repository = "JavierparraDev/audioflow";
    public const string RepositoryUrl = "https://github.com/" + Repository;
    public const string ReleasesApiUrl = "https://api.github.com/repos/" + Repository + "/releases";
}
