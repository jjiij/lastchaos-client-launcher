using Launcher.Core.Contracts;

namespace Launcher.Infrastructure.Services;

public sealed class WindowsShortcutService : IShortcutService
{
    public Task SetRunOnStartupAsync(string appName, string executablePath, bool enabled, CancellationToken cancellationToken = default)
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var shortcutPath = Path.Combine(startup, appName + ".url");

        if (!enabled)
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
            return Task.CompletedTask;
        }

        var content = "[InternetShortcut]\r\n" +
                      $"URL=file:///{executablePath.Replace('\\', '/')}\r\n" +
                      "IconIndex=0\r\n" +
                      $"IconFile={executablePath}\r\n";
        File.WriteAllText(shortcutPath, content);
        return Task.CompletedTask;
    }
}
