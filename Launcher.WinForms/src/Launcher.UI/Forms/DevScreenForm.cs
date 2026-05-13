using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public sealed class DevScreenForm : LauncherFormBase
{
    private readonly TextBox _hostBox = new() { Left = 16, Top = 10, Width = 280 };
    private readonly TextBox _serverBox = new() { Left = 310, Top = 10, Width = 180 };
    private readonly TextBox _nkspBox = new() { Left = 500, Top = 10, Width = 200 };
    private readonly CheckBox _runStartup = new() { Left = 16, Top = 420, Width = 200, Text = "Run on startup" };
    private readonly CheckBox _startAfterUpdate = new() { Left = 220, Top = 420, Width = 200, Text = "Start game after update" };
    private readonly Button _createChecklist = new() { Left = 430, Top = 420, Width = 170, Height = 24, Text = "Create Checklist" };

    public DevScreenForm(LauncherSettings settings, IUpdateService updateService, IRepairService repairService, ISettingsStore settingsStore, IDependencyInstaller dependencyInstaller, IGameLauncher gameLauncher, IShortcutService shortcutService)
        : base("LastChaos Launcher - DevScreen", settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
    {
        Controls.AddRange([_hostBox, _serverBox, _nkspBox, _runStartup, _startAfterUpdate, _createChecklist]);

        _hostBox.Text = settings.HostUrl;
        _serverBox.Text = settings.ServerName;
        _nkspBox.Text = settings.NkspLaunchParameter;
        _runStartup.Checked = settings.RunOnStartup;
        _startAfterUpdate.Checked = settings.StartGameAfterUpdate;

        _createChecklist.Click += async (_, _) =>
        {
            using var picker = new FolderBrowserDialog();
            if (picker.ShowDialog() == DialogResult.OK)
            {
                var path = await RepairService.CreateChecklistAsync(picker.SelectedPath);
                StatusLabel.Text = $"Checklist created: {path}";
            }
        };

        SaveButton.Click += (_, _) =>
        {
            settings.HostUrl = _hostBox.Text.Trim();
            settings.ServerName = _serverBox.Text.Trim();
            settings.NkspLaunchParameter = _nkspBox.Text.Trim();
            settings.RunOnStartup = _runStartup.Checked;
            settings.StartGameAfterUpdate = _startAfterUpdate.Checked;
        };
    }

    protected override string StyleHtml() => "style1.html";
}
