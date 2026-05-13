using System.IO.Compression;
using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;

namespace Launcher.Infrastructure.Services;

public sealed class UpdateService : IUpdateService
{
    private readonly string _root;
    private readonly UpdateChannelConfig _config;
    private readonly IGitHubReleaseClient _releaseClient;
    private readonly IDownloadService _downloadService;
    private readonly IProgress<ProgressSnapshot>? _progress;
    private volatile bool _paused;

    public UpdateService(
        string root,
        UpdateChannelConfig config,
        IGitHubReleaseClient releaseClient,
        IDownloadService downloadService,
        IProgress<ProgressSnapshot>? progress = null)
    {
        _root = root;
        _config = config;
        _releaseClient = releaseClient;
        _downloadService = downloadService;
        _progress = progress;
        State = UpdateState.Idle;
    }

    public UpdateState State { get; private set; }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public async Task<UpdateOperationResult> UpdateGameAndAssetsAsync(CancellationToken cancellationToken = default)
    {
        var game = await UpdateGameOnlyAsync(cancellationToken);
        if (!game.Success) return game;

        var assets = await UpdateAssetsOnlyAsync(cancellationToken);
        if (!assets.Success) return assets;

        return new UpdateOperationResult(true, "Game and assets updated", game.Version);
    }

    public async Task<UpdateOperationResult> UpdateAssetsOnlyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            State = UpdateState.Downloading;
            var zipUrl = $"https://github.com/{_config.AssetsRepo}/archive/refs/heads/{_config.AssetsBranch}.zip";
            var zipPath = Path.Combine(_root, "assets-main.zip");
            var extract = Path.Combine(_root, "_assets_extract");

            await _downloadService.DownloadAsync(zipUrl, zipPath, _progress, cancellationToken);
            WaitIfPaused(cancellationToken);

            State = UpdateState.Unzipping;
            if (Directory.Exists(extract)) Directory.Delete(extract, true);
            ZipFile.ExtractToDirectory(zipPath, extract, true);

            var extractedRoot = Path.Combine(extract, $"{_config.AssetsRepo.Split('/')[1]}-{_config.AssetsBranch}");
            if (!Directory.Exists(extractedRoot))
            {
                return new UpdateOperationResult(false, "Unexpected assets archive structure");
            }

            CopyDirectory(extractedRoot, _root, overwrite: true);
            Directory.Delete(extract, true);
            File.Delete(zipPath);
            await File.WriteAllTextAsync(Path.Combine(_root, ".assets_version"), _config.AssetsBranch, cancellationToken);

            State = UpdateState.Completed;
            return new UpdateOperationResult(true, "Assets updated", _config.AssetsBranch);
        }
        catch (Exception ex)
        {
            State = UpdateState.Error;
            return new UpdateOperationResult(false, ex.Message);
        }
    }

    public async Task<SelfUpdateResult> UpdateLauncherAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            State = UpdateState.Checking;
            var release = await _releaseClient.GetLatestReleaseAsync(_config.LauncherRepo, cancellationToken);
            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                return new SelfUpdateResult(false, "No launcher release ZIP artifact found.");
            }

            State = UpdateState.Downloading;
            var downloadPath = Path.Combine(_root, "launcher-update.zip");
            var stagePath = Path.Combine(_root, "_launcher_update");

            await _downloadService.DownloadAsync(asset.BrowserDownloadUrl, downloadPath, _progress, cancellationToken);

            if (Directory.Exists(stagePath)) Directory.Delete(stagePath, true);
            ZipFile.ExtractToDirectory(downloadPath, stagePath, true);

            await File.WriteAllTextAsync(Path.Combine(_root, ".launcher_version"), release.TagName, cancellationToken);
            State = UpdateState.Completed;
            return new SelfUpdateResult(true, "Launcher update staged", stagePath);
        }
        catch (Exception ex)
        {
            State = UpdateState.Error;
            return new SelfUpdateResult(false, ex.Message);
        }
    }

    private async Task<UpdateOperationResult> UpdateGameOnlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            State = UpdateState.Checking;
            var release = await _releaseClient.GetLatestReleaseAsync(_config.GameRepo, cancellationToken);
            var version = string.IsNullOrWhiteSpace(release.TagName) ? release.Name : release.TagName;
            var candidates = SelectGameAssets(release.Assets);
            if (candidates.Count == 0)
            {
                return new UpdateOperationResult(false, "No game release artifacts found.");
            }

            State = UpdateState.Downloading;
            var downloadedParts = new List<string>();
            foreach (var asset in candidates)
            {
                var path = Path.Combine(_root, asset.Name);
                downloadedParts.Add(path);
                await _downloadService.DownloadAsync(asset.BrowserDownloadUrl, path, _progress, cancellationToken);
                WaitIfPaused(cancellationToken);
            }

            var gameZip = Path.Combine(_root, "game.zip");
            State = UpdateState.Unzipping;
            if (candidates.Count > 1)
            {
                await using var output = File.Create(gameZip);
                foreach (var part in downloadedParts.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    await using var input = File.OpenRead(part);
                    await input.CopyToAsync(output, cancellationToken);
                }
                foreach (var part in downloadedParts) File.Delete(part);
            }
            else
            {
                File.Move(downloadedParts[0], gameZip, true);
            }

            ZipFile.ExtractToDirectory(gameZip, _root, true);
            File.Delete(gameZip);
            await File.WriteAllTextAsync(Path.Combine(_root, ".client_version"), version, cancellationToken);

            State = UpdateState.Completed;
            return new UpdateOperationResult(true, "Game updated", version);
        }
        catch (Exception ex)
        {
            State = UpdateState.Error;
            return new UpdateOperationResult(false, ex.Message);
        }
    }

    private List<GitHubAsset> SelectGameAssets(IReadOnlyList<GitHubAsset> assets)
    {
        var lower = assets.Select(a => (asset: a, name: a.Name.ToLowerInvariant())).ToList();

        var parts = lower
            .Where(x => x.name.Contains(".part") || System.Text.RegularExpressions.Regex.IsMatch(x.name, @"\.zip\.\d+$") || System.Text.RegularExpressions.Regex.IsMatch(x.name, @"\.z\d+$"))
            .Select(x => x.asset)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count > 0) return parts;

        var zip = lower.FirstOrDefault(x => x.name.EndsWith(".zip")).asset;
        return zip is null ? [] : [zip];
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }

    private void WaitIfPaused(CancellationToken cancellationToken)
    {
        while (_paused)
        {
            State = UpdateState.Paused;
            _progress?.Report(new ProgressSnapshot { State = UpdateState.Paused, StatusText = "Paused" });
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(200);
        }

        if (State == UpdateState.Paused)
        {
            State = UpdateState.Downloading;
        }
    }
}
