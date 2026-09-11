using TTMG;
using TTMG.Commands;
using TTMG.Interfaces;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Commands;

public class CliCommandTests
{
    [Fact]
    public async Task CreateCommand_WithName_CreatesScript()
    {
        var scripts = new FakeScriptService();
        var command = new CreateCommand(scripts);

        await command.Execute(new[] { "myscript" });

        Assert.Equal(new[] { "myscript" }, scripts.Created);
    }

    [Fact]
    public async Task EditCommand_WhenResolved_OpensEditor()
    {
        var scripts = new FakeScriptService
        {
            ResolveResult = new ScriptMetadata { DisplayName = "myscript", FullPath = "myscript.lua" }
        };
        var command = new EditCommand(scripts);

        await command.Execute(new[] { "myscript" });

        Assert.Equal(new[] { "myscript" }, scripts.ResolvedNames);
        Assert.Equal(new[] { "myscript.lua" }, scripts.OpenedInEditor);
    }

    [Fact]
    public async Task EditCommand_WhenNotResolved_DoesNotOpenEditor()
    {
        var scripts = new FakeScriptService { ResolveResult = null };
        var command = new EditCommand(scripts);

        await command.Execute(new[] { "missing" });

        Assert.Empty(scripts.OpenedInEditor);
    }

    [Fact]
    public void EditCommand_Suggestions_IncludeMatchingDisplayNamesAndAliases()
    {
        var scripts = new FakeScriptService();
        scripts.Scripts.Add(new ScriptMetadata { DisplayName = "my-script", FullPath = "a.lua" });
        scripts.Scripts.Add(new ScriptMetadata { DisplayName = "other", Alias = "mine", FullPath = "b.lua" });
        var command = new EditCommand(scripts);

        var suggestions = command.GetSuggestions(new[] { "m" }).ToList();

        Assert.Contains("my-script", suggestions);
        Assert.Contains("mine", suggestions);
    }

    [Fact]
    public async Task DeleteCommand_WhenNotResolved_DoesNotDelete()
    {
        var scripts = new FakeScriptService { ResolveResult = null };
        var command = new TestDeleteCommand(scripts, confirm: true);

        await command.Execute(new[] { "missing" });

        Assert.Empty(scripts.Deleted);
    }

    [Fact]
    public async Task DeleteCommand_WhenDeclined_DoesNotDelete()
    {
        var scripts = new FakeScriptService
        {
            ResolveResult = new ScriptMetadata { DisplayName = "myscript", FullPath = "myscript.lua" }
        };
        var command = new TestDeleteCommand(scripts, confirm: false);

        await command.Execute(new[] { "myscript" });

        Assert.Empty(scripts.Deleted);
    }

    [Fact]
    public async Task DeleteCommand_WhenConfirmed_DeletesResolvedPath()
    {
        var scripts = new FakeScriptService
        {
            ResolveResult = new ScriptMetadata { DisplayName = "myscript", FullPath = "myscript.lua" }
        };
        var command = new TestDeleteCommand(scripts, confirm: true);

        await command.Execute(new[] { "myscript" });

        Assert.Equal(new[] { "myscript.lua" }, scripts.Deleted);
    }

    [Fact]
    public async Task SystemConfigCommand_WithoutName_OpensLoadedConfig()
    {
        var scripts = new FakeScriptService();
        var config = new FakeConfigService { LoadedConfig = "scripts.yaml" };
        var command = new SystemConfigCommand(scripts, config);

        await command.Execute(Array.Empty<string>());

        Assert.Equal(new[] { "scripts.yaml" }, scripts.OpenedInEditor);
    }

    [Fact]
    public async Task SystemConfigCommand_WithoutLoadedConfig_DoesNotOpenEditor()
    {
        var scripts = new FakeScriptService();
        var config = new FakeConfigService { LoadedConfig = null };
        var command = new SystemConfigCommand(scripts, config);

        await command.Execute(Array.Empty<string>());

        Assert.Empty(scripts.OpenedInEditor);
    }

    [Fact]
    public async Task VersionCommand_CompletesWithoutThrowing()
    {
        var command = new VersionCommand();

        await command.Execute(Array.Empty<string>());

        Assert.Empty(((ICommand)command).GetSuggestions(Array.Empty<string>()));
    }

    private sealed class TestDeleteCommand : DeleteCommand
    {
        private readonly bool _confirm;

        public TestDeleteCommand(IScriptService scriptService, bool confirm) : base(scriptService)
        {
            _confirm = confirm;
        }

        protected override bool Confirm(string message, bool defaultValue) => _confirm;
    }
}
