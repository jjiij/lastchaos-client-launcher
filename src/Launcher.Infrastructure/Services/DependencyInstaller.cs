using Launcher.Core.Contracts;

namespace Launcher.Infrastructure.Services;

public sealed class DependencyInstaller : IDependencyInstaller
{
    private static readonly string[] RequiredRuntimeDlls = ["msvcp100.dll", "msvcr100.dll"];

    // Official Microsoft download center hosted files.
    private const string Vc2010X86Url = "https://download.microsoft.com/download/C/6/D/C6D0FD4E-9E53-4897-9B91-836EBA2AACD3/vcredist_x86.exe";
    private const string DirectXWebUrl = "https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe";

    public async Task<bool> InstallDependenciesAsync(
        string launcherRootPath,
        bool allowInstallerExecution = false,
        CancellationToken cancellationToken = default)
    {
        if (HasRuntimeDllsInBin(launcherRootPath))
        {
            return true;
        }

        if (!allowInstallerExecution)
        {
            return false;
        }

        var vc = ResolveLocalInstaller(
            launcherRootPath,
            "vcredist_2010_x86.exe",
            "vcredist_x86.exe");

        var cacheRoot = Path.Combine(launcherRootPath, "_prereq-cache");
        Directory.CreateDirectory(cacheRoot);

        var dx = ResolveLocalInstaller(
            launcherRootPath,
            "dxwebsetup.exe");
        vc ??= await DownloadInstallerAsync(Vc2010X86Url, Path.Combine(cacheRoot, "vcredist_x86.exe"), cancellationToken);
        dx ??= await DownloadInstallerAsync(DirectXWebUrl, Path.Combine(cacheRoot, "dxwebsetup.exe"), cancellationToken);

        var ranAnyInstaller = false;
        var ok = true;

        if (!string.IsNullOrWhiteSpace(vc) && File.Exists(vc))
        {
            ranAnyInstaller = true;
            ok &= await RunInstallerAsync(vc, "/passive /norestart", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(dx) && File.Exists(dx))
        {
            ranAnyInstaller = true;
            ok &= await RunInstallerAsync(dx, "/q", cancellationToken);
        }

        if (!(ranAnyInstaller && ok))
        {
            return false;
        }

        return HasRuntimeDllsInBin(launcherRootPath);
    }

    private static string? ResolveLocalInstaller(string rootOrDocsPath, params string[] names)
    {
        var searchRoots = new[]
        {
            rootOrDocsPath,
            Path.Combine(rootOrDocsPath, "Docs"),
            Path.Combine(rootOrDocsPath, "Launcher", "Docs"),
            Path.Combine(rootOrDocsPath, "dependencies"),
            Path.Combine(rootOrDocsPath, "prerequisites")
        };

        foreach (var dir in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static async Task<string?> DownloadInstallerAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> DownloadInstallerAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
            return targetPath;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> RunInstallerAsync(string path, string args, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(path, args)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                }
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasRuntimeDllsInBin(string launcherRootPath)
    {
        var binDir = Path.Combine(launcherRootPath, "Bin");
        return RequiredRuntimeDlls.All(name => File.Exists(Path.Combine(binDir, name)));
    }
}
