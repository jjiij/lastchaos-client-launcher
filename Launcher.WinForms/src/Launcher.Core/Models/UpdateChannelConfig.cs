namespace Launcher.Core.Models;

public sealed class UpdateChannelConfig
{
    public string GameRepo { get; set; } = "jjiij/lastchaos-client";
    public string AssetsRepo { get; set; } = "jjiij/lastchaos-client-assets";
    public string LauncherRepo { get; set; } = "jjiij/lastchaos-client-launcher";
    public string AssetsBranch { get; set; } = "main";
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(15);
    public int RetryCount { get; set; } = 3;
}
