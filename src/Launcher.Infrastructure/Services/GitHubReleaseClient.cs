using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.Infrastructure.Services;

public sealed class GitHubReleaseClient : IGitHubReleaseClient
{
    private readonly HttpClient _httpClient;

    public GitHubReleaseClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LastChaos-WinForms-Launcher");
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(string repo, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{repo}/releases";
        var releases = await _httpClient.GetFromJsonAsync<List<GitHubReleaseDto>>(url, cancellationToken);
        var latest = releases?.FirstOrDefault() ?? throw new InvalidOperationException($"No releases found for {repo}.");

        return new GitHubRelease
        {
            TagName = latest.TagName ?? string.Empty,
            Name = latest.Name ?? string.Empty,
            Assets = (latest.Assets ?? []).Select(a => new GitHubAsset
            {
                Name = a.Name ?? string.Empty,
                BrowserDownloadUrl = a.BrowserDownloadUrl ?? string.Empty,
                Size = a.Size
            }).ToArray()
        };
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
