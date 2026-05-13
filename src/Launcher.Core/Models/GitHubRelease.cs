namespace Launcher.Core.Models;

public sealed class GitHubRelease
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<GitHubAsset> Assets { get; set; } = Array.Empty<GitHubAsset>();
}

public sealed class GitHubAsset
{
    public string Name { get; set; } = string.Empty;
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
}
