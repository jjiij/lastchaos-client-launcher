using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.UI.Forms;

public sealed class Style2Form : LauncherFormBase
{
    public Style2Form(LauncherSettings settings, IUpdateService updateService, IRepairService repairService, ISettingsStore settingsStore, IDependencyInstaller dependencyInstaller, IGameLauncher gameLauncher, IShortcutService shortcutService)
        : base("LastChaos Launcher - Style 2", settings, updateService, repairService, settingsStore, dependencyInstaller, gameLauncher, shortcutService)
    {
    }

    protected override string StyleHtml() => "style2.html";
}
