using System.Net;
using System.Net.Http;
using System.Text;
using AudioFlow.Updates;
using Xunit;

namespace AudioFlow.Tests;

public class GitHubReleaseSourceTests
{
    private const string Repository = "owner/repo";

    private static string ReleasesJson => """
        [
          { "tag_name": "v0.1.0", "draft": false, "prerelease": false, "assets": [] },
          { "tag_name": "v0.4.0-beta.1", "draft": false, "prerelease": true, "assets": [] },
          {
            "tag_name": "v0.3.0",
            "name": "AudioFlow 0.3.0",
            "draft": false,
            "prerelease": false,
            "assets": [
              {
                "name": "AudioFlow-Setup-v0.3.0.exe",
                "browser_download_url": "https://example.invalid/setup.exe",
                "size": 123
              }
            ]
          },
          { "tag_name": "v9.9.9", "draft": true, "prerelease": false, "assets": [] },
          { "tag_name": "not-a-version", "draft": false, "prerelease": false, "assets": [] }
        ]
        """;

    private static GitHubReleaseSource Source(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var http = new HttpClient(new StubHandler(json, status));
        return new GitHubReleaseSource(http, Repository);
    }

    [Fact]
    public async Task Stable_IgnoresPrereleasesDraftsAndInvalidTags()
    {
        using var source = Source(ReleasesJson);

        var release = await source.GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("0.3.0", release!.Version.ToString());
    }

    [Fact]
    public async Task Beta_AllowsPrereleases()
    {
        using var source = Source(ReleasesJson);

        var release = await source.GetLatestAsync(UpdateChannel.Beta, CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal("0.4.0-beta.1", release!.Version.ToString());
        Assert.True(release.IsPreRelease);
    }

    [Fact]
    public async Task MapsAssetsFromTheSelectedRelease()
    {
        using var source = Source(ReleasesJson);

        var release = await source.GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.NotNull(release);
        Assert.Single(release!.Assets);
        Assert.Equal("AudioFlow-Setup-v0.3.0.exe", release.Assets[0].Name);
        Assert.NotNull(release.Installer);
    }

    [Fact]
    public async Task NonSuccessStatus_ReturnsNull()
    {
        using var source = Source("[]", HttpStatusCode.Forbidden);

        var release = await source.GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.Null(release);
    }

    [Fact]
    public async Task EmptyReleaseList_ReturnsNull()
    {
        using var source = Source("[]");

        var release = await source.GetLatestAsync(UpdateChannel.Stable, CancellationToken.None);

        Assert.Null(release);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _status;

        public StubHandler(string json, HttpStatusCode status)
        {
            _json = json;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
