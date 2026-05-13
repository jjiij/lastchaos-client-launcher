using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public sealed class Style4Form : LauncherFormBase
{
    public Style4Form(LauncherSettings settings, IUpdateService updateService, IRepairService repairService, ISettingsStore settingsStore, IDependencyInstaller dependencyInstaller, IGameLauncher gameLauncher, IShortcutService shortcutService)
        : base("LastChaos Launcher - Style 4", settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
    {
        WebPanel.Width = 647;
        WebPanel.Height = 398;
    }

    protected override string StyleHtml() => "style4.html";
}
