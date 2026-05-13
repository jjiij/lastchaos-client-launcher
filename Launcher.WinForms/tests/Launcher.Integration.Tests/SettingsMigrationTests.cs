using Launcher.Infrastructure.Services;

namespace Launcher.Integration.Tests;

public class SettingsMigrationTests
{
    [Fact]
    public async Task LoadsLegacyFilesAndPersistsJson()
    {
        var temp = Path.Combine(Path.GetTempPath(), "launcher-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(temp);

        await File.WriteAllTextAsync(Path.Combine(temp, "lccnct.dta"), "https://host/,fkzktlfgod!,true,MySrv,style3,true");
        await File.WriteAllTextAsync(Path.Combine(temp, "sl.dta"), "127.0.0.2");
        await File.WriteAllTextAsync(Path.Combine(temp, "vtm.brn"), "12");

        var store = new JsonSettingsStore(temp);
        var settings = await store.LoadAsync();

        Assert.Equal("https://host/", settings.HostUrl);
        Assert.Equal("MySrv", settings.ServerName);
        Assert.True(settings.AllowMultiClient);
        Assert.Equal(12, settings.LocalGameVersion);
        Assert.True(File.Exists(Path.Combine(temp, "launcher.settings.json")));

        Directory.Delete(temp, true);
    }
}
