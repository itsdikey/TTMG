using Xunit;

namespace TTMG.Tests;

public class LuaEnvTests
{
    [Theory]
    [InlineData("powershell", "powershell.exe", "-Command")]
    [InlineData("PowerShell", "powershell.exe", "-Command")]
    [InlineData("pwsh", "pwsh", "-Command")]
    [InlineData("bash", "bash", "-c")]
    [InlineData("zsh", "zsh", "-c")]
    [InlineData("sh", "sh", "-c")]
    [InlineData("cmd", "cmd.exe", "/c")]
    [InlineData("unknown", "cmd.exe", "/c")]
    public void GetShellInfo_MapsConfiguredShell(string configured, string expectedShell, string expectedPrefix)
    {
        var (shell, prefix) = LuaEnv.GetShellInfo(configured);

        Assert.Equal(expectedShell, shell);
        Assert.Equal(expectedPrefix, prefix);
    }
}
