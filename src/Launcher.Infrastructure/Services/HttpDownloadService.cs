using System.Diagnostics;
using System.Net;
using Launcher.Core.Contracts;
using Launcher.Core.Enums;
using Launcher.Core.Models;

namespace Launcher.Infrastructure.Services;

public sealed class HttpDownloadService : IDownloadService
{
    private readonly HttpClient _httpClient;

    public HttpDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(string url, string targetFile, IProgress<ProgressSnapshot>? progress = null, CancellationToken cancellationToken = default)
        => await DownloadAsync(url, targetFile, progress, shouldPause: null, cancellationToken);

    public async Task DownloadAsync(
        string url,
        string targetFile,
        IProgress<ProgressSnapshot>? progress = null,
        Func<bool>? shouldPause = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? ".");
        var partFile = targetFile + ".part";
        var existingLength = File.Exists(partFile) ? new FileInfo(partFile).Length : 0L;

        using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
        using var headRes = await _httpClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        headRes.EnsureSuccessStatusCode();
        var totalLength = headRes.Content.Headers.ContentLength;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0)
        {
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
        }

        using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partFile);
            existingLength = 0;
            req.Headers.Range = null;
            using var retry = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            retry.EnsureSuccessStatusCode();
            await WriteStreamAsync(retry, partFile, false, totalLength, existingLength, progress, shouldPause, cancellationToken);
        }
        else
        {
            res.EnsureSuccessStatusCode();
            var append = res.StatusCode == HttpStatusCode.PartialContent && existingLength > 0;
            await WriteStreamAsync(res, partFile, append, totalLength, existingLength, progress, shouldPause, cancellationToken);
        }

        File.Move(partFile, targetFile, true);
    }

    private static async Task WriteStreamAsync(HttpResponseMessage response, string partFile, bool append, long? totalLength,
        long existingLength, IProgress<ProgressSnapshot>? progress, Func<bool>? shouldPause, CancellationToken cancellationToken)
    {
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(partFile, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[1024 * 256];
        var sw = Stopwatch.StartNew();
        long transferred = existingLength;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            while (shouldPause?.Invoke() == true)
            {
                progress?.Report(new ProgressSnapshot
                {
                    State = UpdateState.Paused,
                    Percent = totalLength.HasValue && totalLength.Value > 0
                        ? (int)Math.Clamp((double)transferred / totalLength.Value * 100, 0, 100)
                        : 0,
                    BytesTotal = totalLength ?? 0,
                    BytesTransferred = transferred,
                    SpeedBytesPerSecond = 0,
                    StatusText = $"Downloading {Path.GetFileName(partFile)}"
                });
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(200, cancellationToken);
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            transferred += read;

            if (progress is not null)
            {
                var pct = totalLength.HasValue && totalLength.Value > 0
                    ? (int)Math.Clamp((double)transferred / totalLength.Value * 100, 0, 100)
                    : 0;
                var speed = sw.Elapsed.TotalSeconds <= 0 ? 0 : transferred / sw.Elapsed.TotalSeconds;
                progress.Report(new ProgressSnapshot
                {
                    State = UpdateState.Downloading,
                    Percent = pct,
                    BytesTotal = totalLength ?? 0,
                    BytesTransferred = transferred,
                    SpeedBytesPerSecond = speed,
                    StatusText = $"Downloading {Path.GetFileName(partFile)}"
                });
            }
        }
    }
}
