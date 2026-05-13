using System.Drawing.Drawing2D;
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

    private readonly Panel _newsPanel = new() { Left = 24, Top = 24, Width = 360, Height = 500 };
    private readonly Label _newsTitle = new() { Left = 18, Top = 16, Width = 320, Height = 30, Text = "NEWS", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold) };
    private readonly RichTextBox _newsBox = new() { Left = 18, Top = 56, Width = 324, Height = 420, ReadOnly = true, BorderStyle = BorderStyle.None };

    private readonly Panel _actionPanel = new() { Left = 410, Top = 290, Width = 540, Height = 290 };
    private readonly Label _title = new() { Left = 28, Top = 16, Width = 470, Height = 40, Text = "LASTCHAOS", ForeColor = Color.White, Font = new Font("Segoe UI Black", 24f, FontStyle.Bold) };
    private readonly Label _subtitle = new() { Left = 30, Top = 60, Width = 470, Height = 24, Text = "Modern launcher / legacy-compatible", ForeColor = Color.FromArgb(190, 220, 255), Font = new Font("Segoe UI", 10f, FontStyle.Regular) };

    protected readonly Label StatusLabel = new() { Left = 30, Top = 96, Width = 490, Height = 34, ForeColor = Color.White, BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly Label _gameProgressLabel = new() { Left = 30, Top = 132, Width = 220, Height = 20, Text = "GAME FILES", ForeColor = Color.FromArgb(211, 219, 239), BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
    private readonly Label _assetsProgressLabel = new() { Left = 30, Top = 176, Width = 220, Height = 20, Text = "ASSETS", ForeColor = Color.FromArgb(211, 219, 239), BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

    protected readonly ProgressBar GameProgress = new() { Left = 30, Top = 152, Width = 488, Height = 14, Style = ProgressBarStyle.Continuous };
    protected readonly ProgressBar AssetsProgress = new() { Left = 30, Top = 196, Width = 488, Height = 14, Style = ProgressBarStyle.Continuous };

    protected readonly Button PrimaryButton = new() { Left = 30, Top = 224, Width = 250, Height = 46, Text = "Download / Update", Font = new Font("Segoe UI", 12f, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
    protected readonly Button PauseButton = new() { Left = 292, Top = 224, Width = 70, Height = 46, Text = "Pause", FlatStyle = FlatStyle.Flat };
    protected readonly Button RepairButton = new() { Left = 372, Top = 224, Width = 70, Height = 46, Text = "Repair", FlatStyle = FlatStyle.Flat };
    protected readonly Button SaveButton = new() { Left = 452, Top = 224, Width = 66, Height = 46, Text = "Save", FlatStyle = FlatStyle.Flat };

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
        Width = 1000;
        Height = 650;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 10f);

        Settings = settings;
        UpdateService = updateService;
        RepairService = repairService;
        SettingsStore = settingsStore;
        DependencyInstaller = dependencyInstaller;
        GameLauncher = gameLauncher;
        ShortcutService = shortcutService;

        ConfigureTheme();

        _newsPanel.Controls.AddRange([_newsTitle, _newsBox]);
        _actionPanel.Controls.AddRange([
            _title,
            _subtitle,
            StatusLabel,
            _gameProgressLabel,
            GameProgress,
            _assetsProgressLabel,
            AssetsProgress,
            PrimaryButton,
            PauseButton,
            RepairButton,
            SaveButton
        ]);

        Controls.AddRange([_newsPanel, _actionPanel]);

        PrimaryButton.Click += async (_, _) => await OnPrimaryActionAsync();
        PauseButton.Click += (_, _) => TogglePause();
        RepairButton.Click += async (_, _) => await RunRepairAsync();
        SaveButton.Click += async (_, _) => await SaveSettingsAsync();

        Load += (_, _) => OnLauncherLoaded();
        Shown += (_, _) => _ = StartUpdateAsync();
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

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);

        var rect = ClientRectangle;
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(16, 24, 44), Color.FromArgb(32, 39, 63), LinearGradientMode.ForwardDiagonal);
        e.Graphics.FillRectangle(brush, rect);

        using var glow1 = new SolidBrush(Color.FromArgb(50, 0, 160, 255));
        using var glow2 = new SolidBrush(Color.FromArgb(35, 120, 40, 255));
        e.Graphics.FillEllipse(glow1, Width - 360, -120, 420, 420);
        e.Graphics.FillEllipse(glow2, Width - 420, 260, 360, 360);
    }

    private void ConfigureTheme()
    {
        _newsPanel.BackColor = Color.FromArgb(19, 24, 37);
        _actionPanel.BackColor = Color.FromArgb(12, 16, 28);

        _newsBox.BackColor = Color.FromArgb(19, 24, 37);
        _newsBox.ForeColor = Color.FromArgb(224, 234, 255);
        _newsBox.Font = new Font("Segoe UI", 10f);

        StyleButton(PrimaryButton, Color.FromArgb(50, 156, 255), Color.White);
        StyleButton(PauseButton, Color.FromArgb(47, 59, 86), Color.White);
        StyleButton(RepairButton, Color.FromArgb(47, 59, 86), Color.White);
        StyleButton(SaveButton, Color.FromArgb(47, 59, 86), Color.White);

        GameProgress.ForeColor = Color.FromArgb(77, 181, 255);
        AssetsProgress.ForeColor = Color.FromArgb(116, 219, 144);
    }

    private static void StyleButton(Button button, Color background, Color foreground)
    {
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatAppearance.BorderColor = Color.FromArgb(95, 115, 158);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(
            Math.Min(background.R + 15, 255),
            Math.Min(background.G + 15, 255),
            Math.Min(background.B + 15, 255));
    }

    private void OnLauncherLoaded()
    {
        ApplySkinBackground();
        LoadNews();
        UpdatePrimaryButtonLabel();
        StatusLabel.Text = "Launcher ready. Checking updates in background...";
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
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var original = Image.FromFile(path);
            var tinted = new Bitmap(original.Width, original.Height);
            using var g = Graphics.FromImage(tinted);
            g.DrawImage(original, 0, 0, original.Width, original.Height);
            using var darken = new SolidBrush(Color.FromArgb(95, 8, 14, 28));
            g.FillRectangle(darken, 0, 0, tinted.Width, tinted.Height);
            BackgroundImage = tinted;
            BackgroundImageLayout = ImageLayout.Stretch;
        }
        catch
        {
            // Use gradient fallback.
        }
    }

    private void LoadNews()
    {
        _newsBox.Clear();
        var newsPath = Path.Combine(AppContext.BaseDirectory, "news.json");
        if (!File.Exists(newsPath))
        {
            _newsBox.AppendText("No news yet.\n\n");
            _newsBox.AppendText("Create news.json to populate this section.");
            return;
        }

        try
        {
            var json = File.ReadAllText(newsPath);
            var items = JsonSerializer.Deserialize<List<NewsItem>>(json) ?? [];
            if (items.Count == 0)
            {
                _newsBox.AppendText("No news entries.");
                return;
            }

            foreach (var item in items)
            {
                _newsBox.SelectionFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
                _newsBox.AppendText($"[{item.Date}] {item.Title}\n");
                _newsBox.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                _newsBox.AppendText(item.Body + "\n\n");
            }
        }
        catch (Exception ex)
        {
            _newsBox.AppendText("Failed to load news.json\n\n" + ex.Message);
        }
    }

    private sealed class NewsItem
    {
        public string Date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
    }
}
