using Launcher.Core.Enums;
using Launcher.Core.Services;

namespace Launcher.Core.Tests;

public class LauncherCommandParserTests
{
    [Fact]
    public void ParsesDevArg()
    {
        var result = LauncherCommandParser.Parse(["-dev"]);
        Assert.Equal(LauncherCommand.Dev, result.Command);
    }

    [Fact]
    public void ParsesCreateListArg()
    {
        var result = LauncherCommandParser.Parse(["-createlist=\"C:\\Games\\LC\""]);
        Assert.Equal(LauncherCommand.CreateList, result.Command);
        Assert.Equal("C:\\Games\\LC", result.Value);
    }
}
