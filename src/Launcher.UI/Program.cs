using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Services;
using Launcher.UI.Forms;

namespace Launcher.UI;

internal static class Program
{
    private const string InstallDirName = "LastChaos Genesis";
    private const string RelocatedArg = "--relocated";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));

        try
        {
            Run(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ReportFatal(ex);
        }
    }

    private static async Task Run(string[] args)
    {
        if (!EnsureInstalledLocation(args))
        {
            return;
        }

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
            await depInstaller.InstallDependenciesAsync(root, allowInstallerExecution: true);
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

    private static bool EnsureInstalledLocation(string[] args)
    {
        if (args.Any(a => a.Equals(RelocatedArg, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var currentRoot = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var targetRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallDirName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(currentRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            EnsureDesktopShortcut(targetRoot);
            return true;
        }

        Directory.CreateDirectory(targetRoot);
        CopyDirectory(currentRoot, targetRoot);
        EnsureDesktopShortcut(targetRoot);

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return true;
        }

        var targetExe = Path.Combine(targetRoot, Path.GetFileName(exePath));
        var relaunchArgs = string.Join(" ", args.Concat([RelocatedArg]).Select(QuoteArg));
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(targetExe, relaunchArgs)
            {
                WorkingDirectory = targetRoot,
                UseShellExecute = true
            });

            return false;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Relaunch cancelled by user/UAC flow; continue from current location.
            return true;
        }
        catch
        {
            // If restart fails for any reason, keep launcher usable in current location.
            return true;
        }
    }

    private static void EnsureDesktopShortcut(string installRoot)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Directory.CreateDirectory(desktop);
            var shortcutPath = Path.Combine(desktop, "LastChaos Genesis.lnk");
            var legacyUrlPath = Path.Combine(desktop, "LastChaos Genesis.url");
            var exeName = Path.GetFileName(Environment.ProcessPath);
            if (string.IsNullOrWhiteSpace(exeName))
            {
                exeName = "Launcher.UI.exe";
            }

            var exePath = Path.Combine(installRoot, exeName);
            if (!File.Exists(exePath))
            {
                // Fallback for first run before relocation copy settles.
                exePath = Environment.ProcessPath ?? exePath;
            }

            var gameExe = Path.Combine(installRoot, "Bin", "Nksp.exe");
            var iconCandidates = new[]
            {
                Path.Combine(installRoot, "LastChaosGenesis.ico"),
                Path.Combine(installRoot, "LastChaos.ico"),
                Path.Combine(installRoot, "icon.ico"),
                gameExe,
                exePath
            };
            var iconPath = iconCandidates.FirstOrDefault(File.Exists) ?? exePath;
            if (File.Exists(legacyUrlPath))
            {
                File.Delete(legacyUrlPath);
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = installRoot;
            shortcut.IconLocation = $"{iconPath},0";
            shortcut.Description = "LastChaos Genesis";
            shortcut.Save();
        }
        catch
        {
            // shortcut creation failure is non-fatal
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relativeDir = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, relativeDir));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string QuoteArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (!arg.Contains(' ') && !arg.Contains('"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\"", "\\\"") + "\"";
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

    private static void ReportFatal(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "launcher-fatal.log");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {ex}\n\n");
            MessageBox.Show($"Launcher failed to start.\n\n{ex.Message}\n\nSee: {logPath}", "Launcher Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // ignored
        }
    }
}
