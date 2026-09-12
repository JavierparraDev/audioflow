using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioFlow.Configuration;

namespace AudioFlow.Updates;

/// <summary>
/// Queries the public GitHub Releases API. Makes only the requests needed to
/// resolve the latest release: no telemetry, no user data is ever sent.
/// </summary>
public sealed class GitHubReleaseSource : IReleaseSource, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _repository;

    public GitHubReleaseSource(HttpClient? httpClient = null, string repository = UpdateDefaults.Repository)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _ownsHttpClient = httpClient is null;
        _repository = repository;
    }

    public async Task<ReleaseInfo?> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{_repository}/releases?per_page=15";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd($"AudioFlow/{AudioFlowVersion.Current}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(json, JsonOptions);
        if (releases is null)
        {
            return null;
        }

        ReleaseInfo? best = null;

        foreach (var dto in releases)
        {
            if (dto.Draft || string.IsNullOrWhiteSpace(dto.TagName))
            {
                continue;
            }

            if (channel == UpdateChannel.Stable && dto.Prerelease)
            {
                continue;
            }

            if (!AppVersion.TryParse(dto.TagName, out var version) || version is null)
            {
                continue;
            }

            if (best is not null && version.CompareTo(best.Version) <= 0)
            {
                continue;
            }

            best = Map(dto, version);
        }

        return best;
    }

    private static ReleaseInfo Map(GitHubReleaseDto dto, AppVersion version)
    {
        var assets = (dto.Assets ?? new List<GitHubAssetDto>())
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
            .Select(a => new UpdateAsset(a.Name!, a.BrowserDownloadUrl!, a.Size))
            .ToList();

        return new ReleaseInfo(
            version,
            dto.TagName ?? version.ToString(),
            dto.Name ?? $"AudioFlow {version}",
            dto.Body ?? string.Empty,
            dto.HtmlUrl ?? UpdateDefaults.RepositoryUrl,
            dto.PublishedAt ?? DateTimeOffset.MinValue,
            dto.Prerelease,
            assets);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
