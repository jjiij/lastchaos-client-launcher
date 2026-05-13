using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public sealed class Style1Form : LauncherFormBase
{
    public Style1Form(LauncherSettings settings, IUpdateService updateService, IRepairService repairService, ISettingsStore settingsStore, IDependencyInstaller dependencyInstaller, IGameLauncher gameLauncher, IShortcutService shortcutService)
        : base("LastChaos Launcher - Style 1", settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
    {
    }

    protected override string StyleHtml() => "style1.html";
}
