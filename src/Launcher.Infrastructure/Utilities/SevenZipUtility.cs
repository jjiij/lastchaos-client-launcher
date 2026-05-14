using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Launcher.Infrastructure.Utilities;

public static class SevenZipUtility
{
    private const string SevenZipUrl = "https://www.7-zip.org/a/7zr.exe";

    public static async Task<string> ResolveSevenZipPathAsync(string root, CancellationToken cancellationToken = default)
    {
        var target = Path.Combine(root, "7zr.exe");
        if (File.Exists(target))
        {
            return target;
        }

        Directory.CreateDirectory(root);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var response = await http.GetAsync(SevenZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
        return target;
    }

    public static async Task<bool> ExtractAsync(
        string root,
        string archivePath,
        string outputDirectory,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var sevenZip = await ResolveSevenZipPathAsync(root, cancellationToken);

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
