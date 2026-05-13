namespace Launcher.Core.Models;

public sealed class LauncherSettings
{
    public string HostUrl { get; set; } = "https://example.com/";
    public string LoginServer { get; set; } = "127.0.0.1";
    public string NkspLaunchParameter { get; set; } = "fkzktlfgod!";
    public string ServerName { get; set; } = "LastChaos";
    public string LauncherStyle { get; set; } = "style1";
    public bool AllowMultiClient { get; set; }
    public bool AllowVerifyGameFilesFunction { get; set; } = true;
    public bool RunOnStartup { get; set; }
    public bool StartGameAfterUpdate { get; set; }
    public long LocalGameVersion { get; set; }
}
