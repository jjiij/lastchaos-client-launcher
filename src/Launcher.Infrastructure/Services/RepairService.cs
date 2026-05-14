using System.Globalization;
using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;
using Launcher.Infrastructure.Utilities;

namespace Launcher.Infrastructure.Services;

public sealed class RepairService : IRepairService
{
    private readonly string _root;
    private readonly LauncherSettings _settings;
    private readonly IDownloadService _downloadService;
    private readonly IProgress<ProgressSnapshot>? _progress;

    public RepairService(string root, LauncherSettings settings, IDownloadService downloadService, IProgress<ProgressSnapshot>? progress = null)
    {
        _root = root;
        _settings = settings;
        _downloadService = downloadService;
        _progress = progress;
    }

    public async Task<string> CreateChecklistAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
        var lines = new List<string>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(file);
            var crc = Crc32.Compute(stream).ToString("X8", CultureInfo.InvariantCulture);
            var relative = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
            lines.Add($"{crc},{relative}");
        }

        var checklistPath = Path.Combine(_root, "checklist.txt");
        await File.WriteAllLinesAsync(checklistPath, lines, cancellationToken);
        return checklistPath;
    }

    public async Task<RepairOperationResult> VerifyAndRepairAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryGetBaseHostUri(out var baseUri))
            {
                return new RepairOperationResult(false, "Invalid HostUrl in launcher settings.");
            }

            var checklistUrl = new Uri(baseUri, "client/checklist.txt").ToString();
            var tmpChecklist = Path.Combine(_root, "_remote_checklist.txt");
            await _downloadService.DownloadAsync(checklistUrl, tmpChecklist, _progress, null, cancellationToken);

            var lines = await File.ReadAllLinesAsync(tmpChecklist, cancellationToken);
            var checkedFiles = 0;
            var repairedFiles = 0;

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line) || !line.Contains(',')) continue;

                var parts = line.Split(',', 2);
                var expectedCrc = parts[0].Trim();
                var relativePath = parts[1].Trim().Replace('/', Path.DirectorySeparatorChar);
                var localPath = Path.Combine(_root, relativePath);
                checkedFiles++;

                var valid = false;
                if (File.Exists(localPath))
                {
                    await using var stream = File.OpenRead(localPath);
                    var crc = Crc32.Compute(stream).ToString("X8", CultureInfo.InvariantCulture);
                    valid = string.Equals(crc, expectedCrc, StringComparison.OrdinalIgnoreCase);
                }

                if (valid) continue;

                var fileUrl = new Uri(baseUri, "client/" + parts[1].Trim().Replace('\\', '/')).ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                await _downloadService.DownloadAsync(fileUrl, localPath, _progress, null, cancellationToken);
                repairedFiles++;
            }

            File.Delete(tmpChecklist);
            return new RepairOperationResult(true, "Repair completed", checkedFiles, repairedFiles);
        }
        catch (Exception ex)
        {
            return new RepairOperationResult(false, ex.Message);
        }
    }

    private bool TryGetBaseHostUri(out Uri baseUri)
    {
        var host = (_settings.HostUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            baseUri = null!;
            return false;
        }

        if (!host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = "https://" + host;
        }

        if (!host.EndsWith("/")) host += "/";
        return Uri.TryCreate(host, UriKind.Absolute, out baseUri!);
    }
}
