using System.Text.Json;
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

    protected readonly Label StatusLabel = new() { AutoSize = false, Width = 740, Height = 40, Top = 460, Left = 20, ForeColor = Color.White, BackColor = Color.FromArgb(150, 0, 0, 0) };
    protected readonly ProgressBar GameProgress = new() { Left = 20, Top = 510, Width = 740, Height = 18 };
    protected readonly ProgressBar AssetsProgress = new() { Left = 20, Top = 536, Width = 740, Height = 18 };
    protected readonly Label GameProgressLabel = new() { Left = 20, Top = 492, Width = 200, Height = 16, Text = "Game Download", ForeColor = Color.White, BackColor = Color.Transparent };
    protected readonly Label AssetsProgressLabel = new() { Left = 20, Top = 518, Width = 200, Height = 16, Text = "Assets Download", ForeColor = Color.White, BackColor = Color.Transparent };

    protected readonly Button PrimaryButton = new() { Left = 20, Top = 566, Width = 300, Height = 56, Text = "Download / Update", Font = new Font("Segoe UI", 13, FontStyle.Bold) };
    protected readonly Button PauseButton = new() { Left = 332, Top = 566, Width = 120, Height = 56, Text = "Pause" };
    protected readonly Button RepairButton = new() { Left = 464, Top = 566, Width = 120, Height = 56, Text = "Repair" };
    protected readonly Button SaveButton = new() { Left = 596, Top = 566, Width = 164, Height = 56, Text = "Save Settings" };

    protected readonly GroupBox NewsBox = new() { Left = 20, Top = 20, Width = 350, Height = 425, Text = "News" };
    protected readonly ListBox NewsList = new() { Left = 10, Top = 24, Width = 330, Height = 390 };

    private CancellationTokenSource? _cts;
    private bool _isUpdating;

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
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        DoubleBuffered = true;

        Settings = settings;
        UpdateService = updateService;
        RepairService = repairService;
        SettingsStore = settingsStore;
        DependencyInstaller = dependencyInstaller;
        GameLauncher = gameLauncher;
        ShortcutService = shortcutService;

        NewsBox.Controls.Add(NewsList);
        Controls.AddRange([
            NewsBox,
            StatusLabel,
            GameProgressLabel,
            GameProgress,
            AssetsProgressLabel,
            AssetsProgress,
            PrimaryButton,
            PauseButton,
            RepairButton,
            SaveButton
        ]);

        PrimaryButton.Click += async (_, _) => await OnPrimaryActionAsync();
        PauseButton.Click += (_, _) => TogglePause();
        RepairButton.Click += async (_, _) => await RunRepairAsync();
        SaveButton.Click += async (_, _) => await SaveSettingsAsync();

        Load += (_, _) => OnLauncherLoaded();
        Shown += (_, _) => _ = StartUpdateAsync();
    }

    private void OnLauncherLoaded()
    {
        ApplySkinBackground();
        LoadNews();
        UpdatePrimaryButtonLabel();
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

        var pct = Math.Clamp(snapshot.Percent, 0, 100);
        var status = snapshot.StatusText ?? string.Empty;

        if (status.Contains("assets", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("assets-main.zip", StringComparison.OrdinalIgnoreCase))
        {
            AssetsProgress.Value = pct;
        }
        else
        {
            GameProgress.Value = pct;
        }

        StatusLabel.Text = $"{snapshot.State}: {status} ({pct}%)";
    }

    private async Task OnPrimaryActionAsync()
    {
        if (_isUpdating)
        {
            return;
        }

        if (HasLaunchableGame())
        {
            await LaunchGameAsync();
            return;
        }

        await StartUpdateAsync();
    }

    private async Task StartUpdateAsync()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        PrimaryButton.Enabled = false;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        StatusLabel.Text = "Checking updates...";
        var result = await Task.Run(async () => await UpdateService.UpdateGameAndAssetsAsync(_cts.Token), _cts.Token);

        _isUpdating = false;
        PrimaryButton.Enabled = true;
        StatusLabel.Text = result.Success ? $"Completed: {result.Message}" : $"Error: {result.Message}";

        if (result.Success)
        {
            GameProgress.Value = 100;
            AssetsProgress.Value = 100;
            UpdatePrimaryButtonLabel();
        }

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
            StatusLabel.Text = "Resumed";
        }
        else
        {
            UpdateService.Pause();
            PauseButton.Text = "Resume";
            StatusLabel.Text = "Paused";
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

    private bool HasLaunchableGame()
    {
        var nksp = Path.Combine(AppContext.BaseDirectory, "Bin", "Nksp.exe");
        return File.Exists(nksp);
    }

    private void UpdatePrimaryButtonLabel()
    {
        PrimaryButton.Text = HasLaunchableGame() ? "Launch Game" : "Download / Update";
    }

    private void ApplySkinBackground()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "CD_Root", "AutoPlay", "Images", "style4", "background.png"),
            Path.Combine(AppContext.BaseDirectory, "CD_Root", "AutoPlay", "Images", "Style3", "background.bmp"),
            Path.Combine(AppContext.BaseDirectory, "CD_Root", "AutoPlay", "Images", "Style2", "background.bmp"),
            Path.Combine(AppContext.BaseDirectory, "CD_Root", "AutoPlay", "Images", "Style1", "background.bmp")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                BackgroundImage = Image.FromFile(path);
                BackgroundImageLayout = ImageLayout.Stretch;
            }
            catch
            {
                BackColor = Color.FromArgb(36, 42, 56);
            }
        }
        else
        {
            BackColor = Color.FromArgb(36, 42, 56);
        }
    }

    private void LoadNews()
    {
        NewsList.Items.Clear();
        NewsList.Items.Add("Loading news...");

        var newsPath = Path.Combine(AppContext.BaseDirectory, "news.json");
        if (!File.Exists(newsPath))
        {
            NewsList.Items.Clear();
            NewsList.Items.Add("No news yet.");
            NewsList.Items.Add("Create news.json to populate this section.");
            return;
        }

        try
        {
            var json = File.ReadAllText(newsPath);
            var items = JsonSerializer.Deserialize<List<NewsItem>>(json) ?? [];
            NewsList.Items.Clear();

            if (items.Count == 0)
            {
                NewsList.Items.Add("No news entries.");
                return;
            }

            foreach (var item in items)
            {
                NewsList.Items.Add($"[{item.Date}] {item.Title}");
                NewsList.Items.Add(item.Body);
                NewsList.Items.Add(string.Empty);
            }
        }
        catch (Exception ex)
        {
            NewsList.Items.Clear();
            NewsList.Items.Add("Failed to load news.json");
            NewsList.Items.Add(ex.Message);
        }
    }

    private sealed class NewsItem
    {
        public string Date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
    }
}
