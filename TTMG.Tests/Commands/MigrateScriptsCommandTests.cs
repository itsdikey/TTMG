using System.Reflection;
using System.Text;
using TTMG.Commands;
using TTMG.Interfaces;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Commands;

public class MigrateScriptsCommandTests
{
    [Fact]
    public void CommandMetadata_IsRegistered()
    {
        var attribute = typeof(MigrateScriptsCommand).GetCustomAttribute<CommandAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(":migrate-scripts", attribute!.Code);
        Assert.Equal("migrate_scripts", attribute.Action);
        Assert.True(typeof(ICommand).IsAssignableFrom(typeof(MigrateScriptsCommand)));
    }

    [Fact]
    public async Task DeclinedConfirmation_WritesNothing()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("legacy.lua");
        var original = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("print('hello')\r\nprompt_input('name')\r\n"))
            .ToArray();
        await File.WriteAllBytesAsync(path, original);

        await new TestMigrateScriptsCommand(new FakeScriptService(path), confirm: false)
            .Execute(Array.Empty<string>());

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task AcceptedConfirmation_MigratesAndPreservesBomAndCrlf()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("legacy.lua");
        await File.WriteAllBytesAsync(path, new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("print('hello')\r\nprompt_input('name')\r\n"))
            .ToArray());

        await new TestMigrateScriptsCommand(new FakeScriptService(path), confirm: true)
            .Execute(Array.Empty<string>());

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal("ttmg.print('hello')\r\nttmg.prompt_input('name')\r\n",
            Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task FileWithoutBom_RemainsWithoutBom()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("nobom.lua");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("get_config('k')\n"));

        await new TestMigrateScriptsCommand(new FakeScriptService(path), confirm: true)
            .Execute(Array.Empty<string>());

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal("ttmg.get_config('k')\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task AlreadyCanonicalFile_IsNotTouched()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("canonical.lua");
        await File.WriteAllTextAsync(path, "ttmg.print('x')\n");
        var before = File.GetLastWriteTimeUtc(path);

        await new TestMigrateScriptsCommand(new FakeScriptService(path), confirm: true)
            .Execute(Array.Empty<string>());

        Assert.Equal("ttmg.print('x')\n", await File.ReadAllTextAsync(path));
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task UnexpectedArguments_AreRejectedWithoutWriting()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("args.lua");
        await File.WriteAllTextAsync(path, "print('x')\n");
        var before = await File.ReadAllBytesAsync(path);

        await new TestMigrateScriptsCommand(new FakeScriptService(path), confirm: true)
            .Execute(new[] { "unexpected" });

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task PerFileReadError_DoesNotBlockOtherFiles()
    {
        using var temp = new TempDirectory();
        var good = temp.Combine("good.lua");
        var missing = temp.Combine("missing.lua");
        await File.WriteAllTextAsync(good, "run_shell('x', false)\n");

        await new TestMigrateScriptsCommand(new FakeScriptService(missing, good), confirm: true)
            .Execute(Array.Empty<string>());

        Assert.Equal("ttmg.run_shell('x', false)\n", await File.ReadAllTextAsync(good));
    }

    private sealed class TestMigrateScriptsCommand : MigrateScriptsCommand
    {
        private readonly bool _confirm;

        public TestMigrateScriptsCommand(IScriptService scriptService, bool confirm) : base(scriptService)
        {
            _confirm = confirm;
        }

        protected override bool Confirm(string message) => _confirm;
    }
}
