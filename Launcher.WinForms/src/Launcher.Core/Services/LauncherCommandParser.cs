using Launcher.Core.Enums;

namespace Launcher.Core.Services;

public static class LauncherCommandParser
{
    public static (LauncherCommand Command, string? Value) Parse(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("-dev", StringComparison.OrdinalIgnoreCase))
            {
                return (LauncherCommand.Dev, null);
            }

            if (arg.Equals("-resetsettings", StringComparison.OrdinalIgnoreCase))
            {
                return (LauncherCommand.ResetSettings, null);
            }

            if (arg.Equals("-installdependencies", StringComparison.OrdinalIgnoreCase))
            {
                return (LauncherCommand.InstallDependencies, null);
            }

            if (arg.StartsWith("-createlist=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[("-createlist=".Length)..].Trim('"');
                return (LauncherCommand.CreateList, value);
            }
        }

        return (LauncherCommand.None, null);
    }
}
