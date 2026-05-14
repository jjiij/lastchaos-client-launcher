using Launcher.Core.Enums;
using Launcher.Core.Models;

namespace Launcher.Core.Contracts;

public interface ISettingsStore
{
    Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}

public interface IGitHubReleaseClient
{
    Task<GitHubRelease> GetLatestReleaseAsync(string repo, CancellationToken cancellationToken = default);
}

public interface IDownloadService
{
    Task DownloadAsync(
        string url,
        string targetFile,
        IProgress<ProgressSnapshot>? progress = null,
        Func<bool>? shouldPause = null,
        CancellationToken cancellationToken = default);
}

public interface IUpdateService
{
    UpdateState State { get; }
    void Pause();
    void Resume();
    Task<UpdateOperationResult> UpdateGameAndAssetsAsync(CancellationToken cancellationToken = default);
    Task<UpdateOperationResult> UpdateAssetsOnlyAsync(CancellationToken cancellationToken = default);
    Task<SelfUpdateResult> UpdateLauncherAsync(CancellationToken cancellationToken = default);
}

public interface IRepairService
{
    Task<string> CreateChecklistAsync(string rootPath, CancellationToken cancellationToken = default);
    Task<RepairOperationResult> VerifyAndRepairAsync(CancellationToken cancellationToken = default);
}

public interface IShortcutService
{
    Task SetRunOnStartupAsync(string appName, string executablePath, bool enabled, CancellationToken cancellationToken = default);
}

public interface IDependencyInstaller
{
    Task<bool> InstallDependenciesAsync(
        string launcherDocsDirectory,
        bool allowInstallerExecution = false,
        CancellationToken cancellationToken = default);
}

public interface IGameLauncher
{
    Task<bool> LaunchAsync(string gameRootPath, string launchArgument, CancellationToken cancellationToken = default);
}
