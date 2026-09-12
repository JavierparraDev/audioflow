using AudioFlow.Configuration;

namespace AudioFlow.Updates;

public sealed record DownloadResult(bool Success, string? FilePath, bool Verified, string? Error);

/// <summary>
/// Orchestrates update checks and verified downloads.
///
/// Checks are asynchronous and never block startup. If the network is
/// unavailable, <see cref="UpdateStatus.Failed"/> is returned and AudioFlow
/// keeps working normally.
/// </summary>
public sealed class UpdateService : IDisposable
{
    public static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromHours(12);

    private readonly IReleaseSource _source;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly AppVersion _current;
    private readonly TimeSpan _minInterval;

    public UpdateService(
        IReleaseSource source,
        AppVersion? current = null,
        UpdateChannel channel = UpdateChannel.Stable,
        TimeSpan? minInterval = null,
        HttpClient? httpClient = null)
    {
        _source = source;
        _current = current ?? AppVersion.Parse(AudioFlowVersion.Current);
        Channel = channel;
        _minInterval = minInterval ?? DefaultCheckInterval;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _ownsHttpClient = httpClient is null;
    }

    public UpdateChannel Channel { get; }

    public AppVersion Current => _current;

    public UpdateCheckResult? LastResult { get; private set; }

    public DateTimeOffset? LastCheckedUtc => LastResult?.CheckedAt;

    public static UpdateChannel ParseChannel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "beta" => UpdateChannel.Beta,
        "nightly" => UpdateChannel.Nightly,
        _ => UpdateChannel.Stable
    };

    /// <summary>
    /// Checks for an update. When <paramref name="force"/> is false and the last
    /// check is recent, the previous result is returned without a network call.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        DateTimeOffset? lastCheckUtc = null,
        bool force = true,
        CancellationToken cancellationToken = default)
    {
        if (!force && lastCheckUtc is { } last && DateTimeOffset.UtcNow - last < _minInterval && LastResult is not null)
        {
            return LastResult;
        }

        try
        {
            var release = await _source.GetLatestAsync(Channel, cancellationToken);

            if (release is null)
            {
                LastResult = new UpdateCheckResult(
                    UpdateStatus.NoReleaseFound, _current, null, null,
                    "No release found.", DateTimeOffset.UtcNow);
                return LastResult;
            }

            var available = release.Version.CompareTo(_current) > 0;
            LastResult = new UpdateCheckResult(
                available ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate,
                _current,
                release.Version,
                release,
                available ? "Update available." : "You are up to date.",
                DateTimeOffset.UtcNow);

            return LastResult;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or System.Net.Sockets.SocketException)
        {
            LastResult = new UpdateCheckResult(
                UpdateStatus.Failed, _current, null, null,
                "Unable to check for updates.", DateTimeOffset.UtcNow);
            return LastResult;
        }
        catch (Exception ex)
        {
            LastResult = new UpdateCheckResult(
                UpdateStatus.Failed, _current, null, null,
                $"Update check failed: {ex.Message}", DateTimeOffset.UtcNow);
            return LastResult;
        }
    }

    /// <summary>
    /// Downloads a file and, when an expected hash is provided, verifies it.
    /// The file is deleted if verification fails.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(
        string url,
        string destinationPath,
        string? expectedSha256,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            long read = 0;

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(destinationPath))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    read += count;
                    if (total > 0)
                    {
                        progress?.Report((double)read / total);
                    }
                }

                await output.FlushAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                if (!Checksum.Verify(destinationPath, expectedSha256))
                {
                    TryDelete(destinationPath);
                    return new DownloadResult(false, null, false, "Checksum verification failed.");
                }

                return new DownloadResult(true, destinationPath, true, null);
            }

            return new DownloadResult(true, destinationPath, false, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            TryDelete(destinationPath);
            return new DownloadResult(false, null, false, $"Download failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            TryDelete(destinationPath);
            return new DownloadResult(false, null, false, ex.Message);
        }
    }

    /// <summary>Downloads a small text resource (e.g. SHA256SUMS.txt).</summary>
    public async Task<string?> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(cancellationToken)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        (_source as IDisposable)?.Dispose();
    }
}
