using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Launcher.Infrastructure.Utilities;

public static class SevenZipUtility
{
    public static string ResolveSevenZipPath(string root)
    {
        var candidates = new[]
        {
            Path.Combine(root, "tools", "7zip", "7zr.exe"),
            Path.Combine(root, "7zr.exe")
        };

        var hit = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(hit))
        {
            throw new FileNotFoundException("Bundled 7zr.exe not found. Expected at tools/7zip/7zr.exe.");
        }

        return hit;
    }

    public static async Task<bool> ExtractAsync(
        string root,
        string archivePath,
        string outputDirectory,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var sevenZip = ResolveSevenZipPath(root);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"x -y -aoa -bsp1 -bso1 -bse1 \"{archivePath}\" -o\"{outputDirectory}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var progressRegex = new Regex(@"(\d{1,3})%", RegexOptions.Compiled);

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data) || progress is null) return;
            var m = progressRegex.Match(e.Data);
            if (!m.Success) return;
            if (!int.TryParse(m.Groups[1].Value, out var pct)) return;
            progress.Report(Math.Clamp(pct, 0, 100));
        };

        process.ErrorDataReceived += (_, _) => { };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0;
    }
}
