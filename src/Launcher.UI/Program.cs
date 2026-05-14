using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Infrastructure.Services;
using Launcher.Infrastructure.Utilities;
using Launcher.UI.Forms;

namespace Launcher.UI;

internal static class Program
{
    private const string InstallDirName = "LastChaos Genesis";
    private const string InstalledExeName = "LastChaosGenesis-Launcher.exe";
    private const string RelocatedArg = "--relocated";
    private const string PortableArg = "--portable";
    private const string LauncherRepo = "jjiij/lastchaos-client-launcher";
    private const string PdbAssetName = "LastChaosGenesis-pdb.zip";

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
        _ = Task.Run(() => EnsurePdbSymbolsAsync(root));
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

    private static async Task EnsurePdbSymbolsAsync(string root)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var installRoot = GetInstallRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(normalizedRoot, installRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var marker = Path.Combine(root, ".pdb_symbols_ready");
            if (File.Exists(marker) && Directory.GetFiles(root, "*.pdb", SearchOption.TopDirectoryOnly).Length > 0)
            {
                return;
            }

            var symbolsDir = Path.Combine(root, "_symbols");
            Directory.CreateDirectory(symbolsDir);
            var zipPath = Path.Combine(symbolsDir, PdbAssetName);
            var url = $"https://github.com/{LauncherRepo}/releases/latest/download/{PdbAssetName}";

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output);
            }

            var extracted = await SevenZipUtility.ExtractAsync(root, zipPath, root, null);
            if (extracted)
            {
                await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("O"));
            }
        }
        catch (Exception ex)
        {
            try
            {
                var log = Path.Combine(root, "launcher-pdb.log");
                await File.AppendAllTextAsync(log, $"[{DateTime.UtcNow:O}] {ex}\n\n");
            }
            catch
            {
                // ignored
            }
        }
    }

    private static string GetInstallRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallDirName);
    }

    private static bool EnsureInstalledLocation(string[] args)
    {
        if (args.Any(a => a.Equals(RelocatedArg, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (args.Any(a => a.Equals(PortableArg, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var currentRoot = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsPortableLocation(currentRoot))
        {
            return true;
        }

        var targetRoot = GetInstallRoot().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(currentRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            EnsureDesktopShortcut(targetRoot);
            return true;
        }

        Directory.CreateDirectory(targetRoot);
        using var progress = new RelocationProgressForm();
        progress.Show();
        progress.BringToFront();
        Application.DoEvents();

        var sourceExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe))
        {
            return true;
        }

        var targetExe = Path.Combine(targetRoot, InstalledExeName);
        var currentDir = Path.GetDirectoryName(sourceExe) ?? currentRoot;
        var source7zr = Path.Combine(currentDir, "7zr.exe");
        var target7zr = Path.Combine(targetRoot, "7zr.exe");
        var sourceIcon = Path.Combine(currentDir, "LastChaosGenesis.ico");
        var targetIcon = Path.Combine(targetRoot, "LastChaosGenesis.ico");

        progress.UpdateProgress(10, "Installing to AppData...");
        Application.DoEvents();
        File.Copy(sourceExe, targetExe, overwrite: true);
        if (File.Exists(source7zr))
        {
            File.Copy(source7zr, target7zr, overwrite: true);
        }
        if (File.Exists(sourceIcon))
        {
            File.Copy(sourceIcon, targetIcon, overwrite: true);
        }
        progress.UpdateProgress(85, "Creating shortcut...");
        Application.DoEvents();

        EnsureDesktopShortcut(targetRoot);
        progress.UpdateProgress(100, "Finalizing...");
        Application.DoEvents();
        progress.Close();

        if (!File.Exists(targetExe))
        {
            return true;
        }

        var relaunchArgs = string.Join(" ", args.Concat([RelocatedArg]).Select(QuoteArg));
        try
        {
            if (!File.Exists(targetExe))
            {
                return true;
            }

            if (TryStartInstalledInstance(targetExe, targetRoot, relaunchArgs))
            {
                return false;
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Relaunch cancelled by user/UAC flow.
            return true;
        }
        catch
        {
            // If restart fails for any reason, keep launcher usable in current location.
            return true;
        }
    }

    private static bool TryStartInstalledInstance(string targetExe, string targetRoot, string relaunchArgs)
    {
        try
        {
            var direct = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = relaunchArgs,
                WorkingDirectory = targetRoot,
                UseShellExecute = false
            });
            return direct is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPortableLocation(string currentRoot)
    {
        try
        {
            var rootPath = Path.GetPathRoot(currentRoot);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            var drive = new DriveInfo(rootPath);
            return drive.DriveType == DriveType.Removable;
        }
        catch
        {
            return false;
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
            var exePath = Path.Combine(installRoot, InstalledExeName);
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

    private sealed class RelocationProgressForm : Form
    {
        private readonly Label _label = new()
        {
            Left = 20,
            Top = 18,
            Width = 440,
            Height = 24,
            Text = "Installing to AppData..."
        };

        private readonly ProgressBar _progress = new()
        {
            Left = 20,
            Top = 52,
            Width = 440,
            Height = 18,
            Style = ProgressBarStyle.Continuous
        };

        public RelocationProgressForm()
        {
            Text = "LastChaos Genesis Setup";
            Width = 500;
            Height = 130;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(_label);
            Controls.Add(_progress);
        }

        public void UpdateProgress(int percent, string text)
        {
            _progress.Value = Math.Clamp(percent, 0, 100);
            _label.Text = $"{text} ({percent}%)";
        }
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
