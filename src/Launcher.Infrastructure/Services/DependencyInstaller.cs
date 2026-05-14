using Launcher.Core.Contracts;
using Launcher.Infrastructure.Utilities;

namespace Launcher.Infrastructure.Services;

public sealed class DependencyInstaller : IDependencyInstaller
{
    private static readonly string[] RequiredRuntimeDlls = ["msvcp100.dll", "msvcr100.dll"];

    private const string Vc2010X86Url = "https://download.microsoft.com/download/C/6/D/C6D0FD4E-9E53-4897-9B91-836EBA2AACD3/vcredist_x86.exe";

    public async Task<bool> InstallDependenciesAsync(
        string launcherRootPath,
        bool allowInstallerExecution = false,
        CancellationToken cancellationToken = default)
    {
        if (HasRuntimeDllsInBin(launcherRootPath))
        {
            return true;
        }

        var vc = ResolveLocalInstaller(launcherRootPath, "vcredist_2010_x86.exe", "vcredist_x86.exe");
        var cacheRoot = Path.Combine(launcherRootPath, "_prereq-cache");
        Directory.CreateDirectory(cacheRoot);
        vc ??= await DownloadInstallerAsync(Vc2010X86Url, Path.Combine(cacheRoot, "vcredist_x86.exe"), cancellationToken);

        if (!string.IsNullOrWhiteSpace(vc) && File.Exists(vc))
        {
            var extracted = await TryExtractRuntimeDllsWithSevenZipAsync(vc, launcherRootPath, cacheRoot, cancellationToken);
            if (extracted && HasRuntimeDllsInBin(launcherRootPath))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TryExtractRuntimeDllsWithSevenZipAsync(
        string vcInstallerPath,
        string launcherRootPath,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        var stageDir = Path.Combine(cacheRoot, "vc-stage");
        var expandedDir = Path.Combine(cacheRoot, "vc-expanded");
        var binDir = Path.Combine(launcherRootPath, "Bin");

        RecreateDir(stageDir);
        RecreateDir(expandedDir);
        Directory.CreateDirectory(binDir);

        // 1) extract payload from installer
        var ok = await SevenZipUtility.ExtractAsync(launcherRootPath, vcInstallerPath, stageDir, null, cancellationToken);
        if (!ok)
        {
            return false;
        }

        // 2) extract each cab payload
        var cabs = Directory.GetFiles(stageDir, "*.cab", SearchOption.AllDirectories);
        foreach (var cab in cabs)
        {
            await SevenZipUtility.ExtractAsync(launcherRootPath, cab, expandedDir, null, cancellationToken);
        }

        var msvcrSource = FindRuntimePayload(expandedDir, "msvcr100");
        var msvcpSource = FindRuntimePayload(expandedDir, "msvcp100");
        if (msvcrSource is null || msvcpSource is null)
        {
            return false;
        }

        File.Copy(msvcrSource, Path.Combine(binDir, "msvcr100.dll"), overwrite: true);
        File.Copy(msvcpSource, Path.Combine(binDir, "msvcp100.dll"), overwrite: true);
        return true;
    }

    private static string? FindRuntimePayload(string root, string token)
    {
        // Supports both plain dll names and cab payload labels like F_CENTRAL_msvcr100_x86
        var matches = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileName(path).ToLowerInvariant();
                return name.Contains(token, StringComparison.OrdinalIgnoreCase)
                       && (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || name.Contains("f_central"));
            })
            .OrderByDescending(path => Path.GetFileName(path).EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matches.FirstOrDefault();
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

    private static bool HasRuntimeDllsInBin(string launcherRootPath)
    {
        var binDir = Path.Combine(launcherRootPath, "Bin");
        return RequiredRuntimeDlls.All(name => File.Exists(Path.Combine(binDir, name)));
    }

    private static void RecreateDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
        Directory.CreateDirectory(dir);
    }
}
