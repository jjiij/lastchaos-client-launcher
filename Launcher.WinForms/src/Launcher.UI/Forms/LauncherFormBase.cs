using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public abstract class LauncherFormBase : Form
{
    protected readonly LauncherSettings Settings;
    protected readonly IUpdateService UpdateService;
    protected readonly IRepairService RepairService;
    protected readonly ISettingsStore SettingsStore;
    protected readonly IDependencyInstaller DependencyInstaller;
    protected readonly IGameLauncher GameLauncher;
    protected readonly IShortcutService ShortcutService;

    protected readonly Label StatusLabel = new() { AutoSize = false, Width = 760, Height = 40, Top = 520, Left = 16 };
    protected readonly ProgressBar Progress = new() { Left = 16, Top = 560, Width = 760, Height = 20 };
    protected readonly Button StartButton = new() { Left = 16, Top = 590, Width = 140, Height = 30, Text = "Start Update" };
    protected readonly Button PauseButton = new() { Left = 166, Top = 590, Width = 140, Height = 30, Text = "Pause" };
    protected readonly Button RepairButton = new() { Left = 316, Top = 590, Width = 140, Height = 30, Text = "Repair" };
    protected readonly Button LaunchButton = new() { Left = 466, Top = 590, Width = 140, Height = 30, Text = "Launch" };
    protected readonly Button SaveButton = new() { Left = 616, Top = 590, Width = 140, Height = 30, Text = "Save Settings" };
    protected readonly Panel WebPanel = new() { Left = 16, Top = 48, Width = 740, Height = 360, BorderStyle = BorderStyle.FixedSingle };
    protected readonly Label WebPlaceholder = new() { Left = 24, Top = 56, Width = 724, Height = 28, Text = "Embedded web panel disabled in this build. Open style page in your browser:", AutoSize = false };
    protected readonly LinkLabel WebLink = new() { Left = 24, Top = 84, Width = 724, Height = 24, AutoSize = false };

    private CancellationTokenSource? _cts;

    protected LauncherFormBase(
        string title,
        LauncherSettings settings,
        IUpdateService updateService,
        IRepairService repairService,
        ISettingsStore settingsStore,
        IDependencyInstaller dependencyInstaller,
        IGameLauncher gameLauncher,
        IShortcutService shortcutService)
    {
        Text = title;
        Width = 800;
        Height = 700;

        Settings = settings;
        UpdateService = updateService;
        RepairService = repairService;
        SettingsStore = settingsStore;
        DependencyInstaller = dependencyInstaller;
        GameLauncher = gameLauncher;
        ShortcutService = shortcutService;

        Controls.AddRange([WebPanel, WebPlaceholder, WebLink, StatusLabel, Progress, StartButton, PauseButton, RepairButton, LaunchButton, SaveButton]);

        StartButton.Click += async (_, _) => await StartUpdateAsync();
        PauseButton.Click += (_, _) => TogglePause();
        RepairButton.Click += async (_, _) => await RunRepairAsync();
        LaunchButton.Click += async (_, _) => await LaunchGameAsync();
        SaveButton.Click += async (_, _) => await SaveSettingsAsync();
        WebLink.LinkClicked += OnWebLinkClicked;

        Load += (_, _) => OnLauncherLoaded();
        Shown += (_, _) => _ = StartUpdateAsync();
    }

    private void OnLauncherLoaded()
    {
        var stylePage = BuildSafeStyleUrl();
        WebLink.Text = stylePage;
        StatusLabel.Text = "Launcher ready. Checking updates in background...";
    }

    protected abstract string StyleHtml();

    public void HandleProgress(ProgressSnapshot snapshot)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => HandleProgress(snapshot));
            return;
        }

        Progress.Value = Math.Clamp(snapshot.Percent, 0, 100);
        StatusLabel.Text = $"{snapshot.State}: {snapshot.StatusText} ({snapshot.Percent}%)";
    }

    private async Task StartUpdateAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        StatusLabel.Text = "Checking updates...";
        var result = await Task.Run(
            async () => await UpdateService.UpdateGameAndAssetsAsync(_cts.Token),
            _cts.Token);
        StatusLabel.Text = result.Success ? $"Completed: {result.Message}" : $"Error: {result.Message}";

        if (result.Success && Settings.StartGameAfterUpdate)
        {
            await LaunchGameAsync();
        }
    }

    private void TogglePause()
    {
        if (UpdateService.State == UpdateState.Paused)
        {
            UpdateService.Resume();
            PauseButton.Text = "Pause";
        }
        else
        {
            UpdateService.Pause();
            PauseButton.Text = "Resume";
        }
    }

    private async Task RunRepairAsync()
    {
        var result = await RepairService.VerifyAndRepairAsync();
        StatusLabel.Text = result.Success
            ? $"Repair completed ({result.RepairedFiles}/{result.CheckedFiles})"
            : $"Repair failed: {result.Message}";
    }

    private async Task LaunchGameAsync()
    {
        var ok = await GameLauncher.LaunchAsync(AppContext.BaseDirectory, Settings.NkspLaunchParameter);
        StatusLabel.Text = ok ? "Game launched" : "Nksp.exe not found";
    }

    private async Task SaveSettingsAsync()
    {
        await SettingsStore.SaveAsync(Settings);
        await ShortcutService.SetRunOnStartupAsync($"LastChaos {Settings.ServerName} Launcher", Application.ExecutablePath, Settings.RunOnStartup);
        StatusLabel.Text = "Settings saved";
    }

    private void OnWebLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        var url = WebLink.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (url.Contains("#discord_join=", StringComparison.OrdinalIgnoreCase))
        {
            var invite = url[(url.IndexOf("#discord_join=", StringComparison.OrdinalIgnoreCase) + "#discord_join=".Length)..];
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"discord:///invite/{invite}") { UseShellExecute = true });
            return;
        }

        if (url.Contains("#open=", StringComparison.OrdinalIgnoreCase))
        {
            var open = url[(url.IndexOf("#open=", StringComparison.OrdinalIgnoreCase) + "#open=".Length)..];
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(open) { UseShellExecute = true });
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    private string BuildSafeStyleUrl()
    {
        var host = (Settings.HostUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return "about:blank";
        }

        if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = "https://" + host;
        }

        if (!host.EndsWith("/"))
        {
            host += "/";
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out var baseUri))
        {
            return "about:blank";
        }

        return new Uri(baseUri, StyleHtml()).ToString();
    }
}
