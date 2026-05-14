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

    private readonly Panel _newsPanel = new() { Left = 24, Top = 24, Width = 390, Height = 560, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
    private readonly Label _newsTitle = new() { Left = 18, Top = 16, Width = 340, Height = 36, Text = "NEWS", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), AutoSize = true };
    private readonly RichTextBox _newsBox = new() { Left = 18, Top = 56, Width = 354, Height = 486, ReadOnly = true, BorderStyle = BorderStyle.None };

    private readonly Panel _actionPanel = new() { Left = 430, Top = 200, Width = 620, Height = 400, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
    private readonly Label _title = new() { Left = 28, Top = 16, Width = 560, Height = 52, Text = "LASTCHAOS", ForeColor = Color.White, Font = new Font("Segoe UI Black", 22f, FontStyle.Bold), AutoSize = true };
    private readonly Label _subtitle = new() { Left = 30, Top = 72, Width = 560, Height = 24, Text = "Modern launcher / legacy-compatible", ForeColor = Color.FromArgb(190, 220, 255), Font = new Font("Segoe UI", 11f, FontStyle.Regular), AutoSize = true };

    protected readonly Label StatusLabel = new() { Left = 30, Top = 112, Width = 560, Height = 34, ForeColor = Color.White, BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), AutoSize = true };
    private readonly Label _downloadDetailsLabel = new() { Left = 30, Top = 146, Width = 560, Height = 44, ForeColor = Color.FromArgb(197, 206, 231), BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), Text = "No active download.", AutoSize = false };
    private readonly Label _gameProgressLabel = new() { Left = 30, Top = 178, Width = 260, Height = 20, Text = "GAME FILES", ForeColor = Color.FromArgb(211, 219, 239), BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true };
    private readonly Label _assetsProgressLabel = new() { Left = 30, Top = 228, Width = 260, Height = 20, Text = "ASSETS", ForeColor = Color.FromArgb(211, 219, 239), BackColor = Color.FromArgb(12, 16, 28), Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true };

    protected readonly ProgressBar GameProgress = new() { Left = 30, Top = 200, Width = 560, Height = 16, Style = ProgressBarStyle.Continuous };
    protected readonly ProgressBar AssetsProgress = new() { Left = 30, Top = 250, Width = 560, Height = 16, Style = ProgressBarStyle.Continuous };

    protected readonly Button PrimaryButton = new() { Left = 30, Top = 290, Width = 298, Height = 64, Text = "Download / Update", Font = new Font("Segoe UI", 13f, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
    protected readonly Button PauseButton = new() { Left = 338, Top = 290, Width = 78, Height = 64, Text = "Pause", FlatStyle = FlatStyle.Flat };
    protected readonly Button RepairButton = new() { Left = 426, Top = 290, Width = 78, Height = 64, Text = "Repair", FlatStyle = FlatStyle.Flat };
    protected readonly Button SaveButton = new() { Left = 514, Top = 290, Width = 76, Height = 64, Text = "Save", FlatStyle = FlatStyle.Flat };

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
        Width = 1200;
        Height = 760;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(1100, 720);

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
            _downloadDetailsLabel,
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
        Resize += (_, _) => ApplyResponsiveLayout();
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
        var (headline, details) = BuildDownloadTextParts(snapshot);
        StatusLabel.Text = headline;
        _downloadDetailsLabel.Text = details;

        if (status.Contains("assets", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("assets-main.zip", StringComparison.OrdinalIgnoreCase))
        {
            AssetsProgress.Value = pct;
        }
        else
        {
            GameProgress.Value = pct;
        }

        if (string.IsNullOrWhiteSpace(StatusLabel.Text))
        {
            StatusLabel.Text = $"{snapshot.State}: {status} ({pct}%)";
        }
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
        _newsBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        _newsBox.WordWrap = true;

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
        ApplyResponsiveLayout();
        LoadNews();
        UpdatePrimaryButtonLabel();
        StatusLabel.Text = "Launcher ready. Checking updates in background...";
    }

    private void ApplyResponsiveLayout()
    {
        const int outerPadding = 24;
        const int panelGap = 16;
        const int newsWidth = 390;
        const int panelInnerPadding = 30;
        const int buttonGap = 10;
        const int buttonHeight = 58;
        const int actionBottomPadding = 16;

        _newsPanel.SetBounds(outerPadding, outerPadding, newsWidth, ClientSize.Height - (outerPadding * 2));
        _newsBox.SetBounds(18, 56, _newsPanel.Width - 36, _newsPanel.Height - 74);

        var actionLeft = _newsPanel.Right + panelGap;
        var actionTop = Math.Max(150, ClientSize.Height - 450);
        var actionWidth = ClientSize.Width - actionLeft - outerPadding;
        var actionHeight = ClientSize.Height - actionTop - outerPadding;
        _actionPanel.SetBounds(actionLeft, actionTop, actionWidth, actionHeight);

        var contentWidth = _actionPanel.Width - (panelInnerPadding * 2);
        var y = 16;

        _title.Location = new Point(30, y);
        y = _title.Bottom + 4;

        _subtitle.Location = new Point(30, y);
        y = _subtitle.Bottom + 12;

        StatusLabel.Location = new Point(30, y);
        y = StatusLabel.Bottom + 6;

        _downloadDetailsLabel.Location = new Point(30, y);
        y = _downloadDetailsLabel.Bottom + 10;

        _gameProgressLabel.Location = new Point(30, y);
        y = _gameProgressLabel.Bottom + 4;

        GameProgress.SetBounds(30, y, contentWidth, 16);
        y = GameProgress.Bottom + 10;

        _assetsProgressLabel.Location = new Point(30, y);
        y = _assetsProgressLabel.Bottom + 4;

        AssetsProgress.SetBounds(30, y, contentWidth, 16);

        var secondaryTotal = 3;
        var secondaryWidth = Math.Max(82, (contentWidth - 280 - (buttonGap * 3)) / secondaryTotal);
        var buttonTop = _actionPanel.Height - buttonHeight - actionBottomPadding;
        PrimaryButton.SetBounds(30, buttonTop, 280, buttonHeight);
        PauseButton.SetBounds(PrimaryButton.Right + buttonGap, buttonTop, secondaryWidth, buttonHeight);
        RepairButton.SetBounds(PauseButton.Right + buttonGap, buttonTop, secondaryWidth, buttonHeight);
        SaveButton.SetBounds(RepairButton.Right + buttonGap, buttonTop, secondaryWidth, buttonHeight);
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
        PrimaryButton.Text = "Updating...";
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        StatusLabel.Text = "Checking updates...";
        var result = await Task.Run(async () => await UpdateService.UpdateGameAndAssetsAsync(_cts.Token), _cts.Token);

        _isUpdating = false;
        PrimaryButton.Enabled = true;
        StatusLabel.Text = result.Success ? $"Completed: {result.Message}" : $"Error: {result.Message}";
        _downloadDetailsLabel.Text = result.Success ? "Update finished." : "Update failed.";

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
            _downloadDetailsLabel.Text = "Download resumed.";
        }
        else
        {
            UpdateService.Pause();
            PauseButton.Text = "Resume";
            StatusLabel.Text = "Paused";
            _downloadDetailsLabel.Text = "Download paused.";
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
        if (!HasVc100Runtime())
        {
            StatusLabel.Text = "Runtime missing. Preparing prerequisites...";
            _downloadDetailsLabel.Text = "Downloading/extracting VC++ runtime DLLs...";
            var installOk = await DependencyInstaller.InstallDependenciesAsync(AppContext.BaseDirectory);
            if (!installOk || !HasVc100Runtime())
            {
                StatusLabel.Text = "Dependency install failed";
                _downloadDetailsLabel.Text = "Automatic runtime setup failed (VC++/DirectX).";
                return;
            }

            StatusLabel.Text = "Runtime ready. Launching game...";
            _downloadDetailsLabel.Text = "VC++ runtime setup completed automatically.";
        }

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

    private static bool HasVc100Runtime()
    {
        var probes = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Bin", "msvcp100.dll"),
            Path.Combine(AppContext.BaseDirectory, "Bin", "msvcr100.dll")
        };

        var systemDir = Environment.SystemDirectory;
        if (!string.IsNullOrWhiteSpace(systemDir))
        {
            probes.Add(Path.Combine(systemDir, "msvcp100.dll"));
            probes.Add(Path.Combine(systemDir, "msvcr100.dll"));
        }

        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(winDir))
        {
            probes.Add(Path.Combine(winDir, "SysWOW64", "msvcp100.dll"));
            probes.Add(Path.Combine(winDir, "SysWOW64", "msvcr100.dll"));
        }

        var hasMsvcp = probes.Any(p => p.EndsWith("msvcp100.dll", StringComparison.OrdinalIgnoreCase) && File.Exists(p));
        var hasMsvcr = probes.Any(p => p.EndsWith("msvcr100.dll", StringComparison.OrdinalIgnoreCase) && File.Exists(p));
        return hasMsvcp && hasMsvcr;
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

    private static (string headline, string details) BuildDownloadTextParts(ProgressSnapshot snapshot)
    {
        if (snapshot.StatusText.StartsWith("Unpacking ", StringComparison.OrdinalIgnoreCase))
        {
            var marker = snapshot.StatusText.IndexOf(':');
            if (marker > -1 && marker < snapshot.StatusText.Length - 1)
            {
                var headline = $"Unpacking... Progress {snapshot.Percent}%";
                var details = snapshot.StatusText[(marker + 1)..].Trim();
                return (headline, details);
            }

            return ($"Unpacking... Progress {snapshot.Percent}%", snapshot.StatusText);
        }

        if (snapshot.BytesTotal <= 0)
        {
            return (snapshot.StatusText, string.Empty);
        }

        var transferred = FormatBytes(snapshot.BytesTransferred);
        var total = FormatBytes(snapshot.BytesTotal);
        var speed = snapshot.SpeedBytesPerSecond > 0
            ? $"{FormatBytes((long)snapshot.SpeedBytesPerSecond)}/s"
            : "0 B/s";
        var summary = $"{transferred}/{total}    {speed}    Progress {snapshot.Percent}%";

        const string prefix = "Downloading ";
        var fileName = snapshot.StatusText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? snapshot.StatusText[prefix.Length..].Trim()
            : snapshot.StatusText.Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (summary, string.Empty);
        }

        return (summary, fileName);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{value:0} {units[unit]}";
        }

        return $"{value:0.00} {units[unit]}";
    }

    private sealed class NewsItem
    {
        public string Date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
    }
}
