using System.Text.Json;
using Launcher.Core.Contracts;
using Launcher.Core.Models;

namespace Launcher.Infrastructure.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _basePath;

    public JsonSettingsStore(string basePath)
    {
        _basePath = basePath;
    }

    public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var jsonPath = Path.Combine(_basePath, "launcher.settings.json");
        if (File.Exists(jsonPath))
        {
            await using var stream = File.OpenRead(jsonPath);
            var data = await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, cancellationToken: cancellationToken);
            return data ?? new LauncherSettings();
        }

        var legacy = LoadLegacySettings();
        await SaveAsync(legacy, cancellationToken);
        return legacy;
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        var jsonPath = Path.Combine(_basePath, "launcher.settings.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(jsonPath);
        await JsonSerializer.SerializeAsync(stream, settings, options, cancellationToken);

        var legacyLine = string.Join(",",
            settings.HostUrl,
            settings.NkspLaunchParameter,
            settings.AllowMultiClient.ToString().ToLowerInvariant(),
            settings.ServerName,
            settings.LauncherStyle,
            settings.AllowVerifyGameFilesFunction.ToString().ToLowerInvariant());
        await File.WriteAllTextAsync(Path.Combine(_basePath, "lccnct.dta"), legacyLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_basePath, "sl.dta"), settings.LoginServer, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_basePath, "vtm.brn"), settings.LocalGameVersion.ToString(), cancellationToken);
    }

    private LauncherSettings LoadLegacySettings()
    {
        var settings = new LauncherSettings();
        var lccnct = Path.Combine(_basePath, "lccnct.dta");
        if (File.Exists(lccnct))
        {
            var parts = File.ReadAllText(lccnct).Split(',');
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) settings.HostUrl = parts[0];
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) settings.NkspLaunchParameter = parts[1];
            if (parts.Length > 2) settings.AllowMultiClient = string.Equals(parts[2], "true", StringComparison.OrdinalIgnoreCase);
            if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3])) settings.ServerName = parts[3];
            if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4])) settings.LauncherStyle = parts[4];
            if (parts.Length > 5) settings.AllowVerifyGameFilesFunction = string.Equals(parts[5], "true", StringComparison.OrdinalIgnoreCase);
        }

        var sl = Path.Combine(_basePath, "sl.dta");
        if (File.Exists(sl)) settings.LoginServer = File.ReadAllText(sl).Trim();

        var brn = Path.Combine(_basePath, "vtm.brn");
        if (File.Exists(brn) && long.TryParse(File.ReadAllText(brn).Trim(), out var version))
        {
            settings.LocalGameVersion = version;
        }

        return settings;
    }
}
