using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Services;
using Launcher.UI.Forms;

namespace Launcher.UI;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var root = AppContext.BaseDirectory;
        var settingsStore = new JsonSettingsStore(root);
        var settings = await settingsStore.LoadAsync();

        var command = LauncherCommandParser.Parse(args);
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var releaseClient = new GitHubReleaseClient(http);
        var downloadService = new HttpDownloadService(http);

        var progressBridge = new UiProgressBridge();
        var updateConfig = new UpdateChannelConfig();
        var updateService = new UpdateService(root, updateConfig, releaseClient, downloadService, progressBridge);
        var repairService = new RepairService(root, settings, downloadService, progressBridge);
        var depInstaller = new DependencyInstaller();
        var gameLauncher = new GameLauncher();
        var shortcutService = new WindowsShortcutService();

        if (command.Command == LauncherCommand.ResetSettings)
        {
            settings.RunOnStartup = false;
            settings.StartGameAfterUpdate = false;
            await settingsStore.SaveAsync(settings);
            return;
        }

        if (command.Command == LauncherCommand.InstallDependencies)
        {
            var docs = Path.Combine(root, "Launcher", "Docs");
            await depInstaller.InstallDependenciesAsync(docs);
            return;
        }

        if (command.Command == LauncherCommand.CreateList && !string.IsNullOrWhiteSpace(command.Value))
        {
            await repairService.CreateChecklistAsync(command.Value!);
            return;
        }

        var form = CreateForm(settings, command.Command == LauncherCommand.Dev, updateService, repairService, settingsStore, depInstaller, gameLauncher, shortcutService);
        progressBridge.Attach(form);
        Application.Run(form);
    }

    private static LauncherFormBase CreateForm(
        LauncherSettings settings,
        bool forceDev,
        IUpdateService updateService,
        IRepairService repairService,
        ISettingsStore settingsStore,
        IDependencyInstaller dependencyInstaller,
        IGameLauncher gameLauncher,
        IShortcutService shortcutService)
    {
        if (forceDev || settings.LauncherStyle.Equals("devscreen", StringComparison.OrdinalIgnoreCase))
        {
            return new DevScreenForm(settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService);
        }

        return settings.LauncherStyle.ToLowerInvariant() switch
        {
            "style2" => new Style2Form(settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService),
            "style3" => new Style3Form(settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService),
            "style4" => new Style4Form(settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService),
            _ => new Style1Form(settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
        };
    }

    private sealed class UiProgressBridge : IProgress<ProgressSnapshot>
    {
        private LauncherFormBase? _form;

        public void Attach(LauncherFormBase form) => _form = form;

        public void Report(ProgressSnapshot value)
        {
            _form?.HandleProgress(value);
        }
    }
}
