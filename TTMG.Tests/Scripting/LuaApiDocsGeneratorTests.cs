using System.Text;
using TTMG.Scripting;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Scripting;

public class LuaApiDocsGeneratorTests
{
    [Fact]
    public void Generate_IncludesCanonicalAndLegacyGuidanceAndEveryCommand()
    {
        var markdown = LuaApiDocsGenerator.Generate();

        Assert.Contains("# TTMG Lua API", markdown);
        Assert.Contains("ttmg` table", markdown);
        Assert.Contains("flat global", markdown);

        foreach (var command in LuaCommandRegistry.Default.Commands)
        {
            Assert.Contains($"ttmg.{command.Signature}", markdown);
        }

        Assert.Contains("Requires access to the encrypted secret store", markdown);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        Assert.Equal(LuaApiDocsGenerator.Generate(), LuaApiDocsGenerator.Generate());
    }

    [Fact]
    public void WriteToFile_CreatesDirectoryAndWritesLfOnlyUtf8WithoutBom()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("nested", "lua-api.md");

        LuaApiDocsGenerator.WriteToFile(path);

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes));
        Assert.Equal(LuaApiDocsGenerator.Generate(), Encoding.UTF8.GetString(bytes));
    }
}
