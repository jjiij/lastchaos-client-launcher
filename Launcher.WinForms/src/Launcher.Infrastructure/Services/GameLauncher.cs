using System.Diagnostics;
using Launcher.Core.Contracts;

namespace Launcher.Infrastructure.Services;

public sealed class GameLauncher : IGameLauncher
{
    public Task<bool> LaunchAsync(string gameRootPath, string launchArgument, CancellationToken cancellationToken = default)
    {
        var nksp = Path.Combine(gameRootPath, "Bin", "Nksp.exe");
        if (!File.Exists(nksp)) return Task.FromResult(false);

        Process.Start(new ProcessStartInfo(nksp, launchArgument)
        {
            WorkingDirectory = Path.GetDirectoryName(nksp) ?? gameRootPath,
            UseShellExecute = true
        });

        return Task.FromResult(true);
    }
}
