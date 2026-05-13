using System.Diagnostics;
using Launcher.Core.Contracts;

namespace Launcher.Infrastructure.Services;

public sealed class DependencyInstaller : IDependencyInstaller
{
    public async Task<bool> InstallDependenciesAsync(string launcherDocsDirectory, CancellationToken cancellationToken = default)
    {
        var vc = Path.Combine(launcherDocsDirectory, "vcredist_2010_x86.exe");
        var dx = Path.Combine(launcherDocsDirectory, "dxwebsetup.exe");

        var ok = true;
        if (File.Exists(vc)) ok &= await RunInstallerAsync(vc, "/q", cancellationToken);
        if (File.Exists(dx)) ok &= await RunInstallerAsync(dx, "/q", cancellationToken);
        return ok;
    }

    private static async Task<bool> RunInstallerAsync(string path, string args, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(path, args)
            {
                UseShellExecute = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;
    }
}
