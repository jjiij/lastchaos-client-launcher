using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public sealed class Style3Form : LauncherFormBase
{
    public Style3Form(LauncherSettings settings, IUpdateService updateService, IRepairService repairService, ISettingsStore settingsStore, IDependencyInstaller dependencyInstaller, IGameLauncher gameLauncher, IShortcutService shortcutService)
        : base("LastChaos Launcher - Style 3", settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
    {
        WebPanel.Width = 725;
        WebPanel.Height = 425;
    }

    protected override string StyleHtml() => "style3.html";
}
