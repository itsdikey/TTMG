using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Commands;

public class CommandServiceTests
{
    [Fact]
    public void DiscoveredCommands_IncludeAllExpectedCodesAndActions()
    {
        var host = new CommandServiceHost();

        var commands = host.Service.GetAvailableCommands().ToDictionary(c => c.Code, c => c.Action);

        Assert.Equal("exit", commands[":qq"]);
        Assert.Equal("exit", commands[":wq"]);
        Assert.Equal("check_updates", commands[":update"]);
        Assert.Equal("install_scripts", commands[":install"]);
        Assert.Equal("create_script", commands[":create"]);
        Assert.Equal("edit_script", commands[":edit"]);
        Assert.Equal("delete_script", commands[":delete"]);
        Assert.Equal("manage_secrets", commands[":secret"]);
        Assert.Equal("system_config", commands[":config"]);
        Assert.Equal("print_version", commands[":version"]);
        Assert.Equal("migrate_scripts", commands[":migrate-scripts"]);
    }

    [Fact]
    public async Task TryExecuteCommand_UnknownCommand_ReturnsFalse()
    {
        var host = new CommandServiceHost();

        Assert.False(await host.Service.TryExecuteCommand(":does-not-exist"));
        Assert.False(await host.Service.TryExecuteCommand(""));
        Assert.False(await host.Service.TryExecuteCommand("   "));
    }

    [Fact]
    public async Task TryExecuteCommand_IsCaseInsensitiveAndReturnsTrue()
    {
        var host = new CommandServiceHost();

        Assert.True(await host.Service.TryExecuteCommand(":VERSION"));
        Assert.True(await host.Service.TryExecuteCommand("  :Version  "));
    }

    [Fact]
    public async Task UpdateCommand_DispatchesToUpdaterWithManualFlag()
    {
        var host = new CommandServiceHost();

        Assert.True(await host.Service.TryExecuteCommand(":update"));

        Assert.Equal(new[] { true }, host.Updater.CheckForUpdatesCalls);
    }

    [Fact]
    public async Task InstallCommand_ParsesRepositoryAndScriptNames()
    {
        var host = new CommandServiceHost();

        Assert.True(await host.Service.TryExecuteCommand(":install official one two"));

        var install = Assert.Single(host.Updater.Installs);
        Assert.Equal("official", install.Repo);
        Assert.Equal(new[] { "one", "two" }, install.Scripts);
    }

    [Fact]
    public async Task InstallCommand_WithTooFewArguments_DoesNotInstall()
    {
        var host = new CommandServiceHost();

        Assert.True(await host.Service.TryExecuteCommand(":install onlyrepo"));

        Assert.Empty(host.Updater.Installs);
    }

    [Fact]
    public async Task SecretCommand_RoutesCreateListAndGet()
    {
        var host = new CommandServiceHost();
        host.Secrets.Names.Add("alpha");

        Assert.True(await host.Service.TryExecuteCommand(":secret create mytoken"));
        Assert.True(await host.Service.TryExecuteCommand(":secret list"));
        Assert.True(await host.Service.TryExecuteCommand(":secret get alpha"));

        Assert.Equal(new[] { "mytoken" }, host.Secrets.Created);
        Assert.Equal(1, host.Secrets.ListCalls);
        var requested = Assert.Single(host.Secrets.Requested);
        Assert.Equal("alpha", requested.Name);
        Assert.Null(requested.Password);
    }

    [Fact]
    public async Task SecretCommand_WithNoArguments_PrintsUsageWithoutTouchingService()
    {
        var host = new CommandServiceHost();

        Assert.True(await host.Service.TryExecuteCommand(":secret"));

        Assert.Empty(host.Secrets.Created);
        Assert.Empty(host.Secrets.Requested);
        Assert.Equal(0, host.Secrets.ListCalls);
    }

    [Fact]
    public void GetSuggestions_ForSecretSubcommands()
    {
        var host = new CommandServiceHost();
        host.Secrets.Names.AddRange(new[] { "alpha", "beta" });

        Assert.Equal(new[] { "create", "list", "get" }, host.Service.GetSuggestions(":secret "));
        Assert.Equal(new[] { "create" }, host.Service.GetSuggestions(":secret c"));
        Assert.Equal(new[] { "alpha", "beta" }, host.Service.GetSuggestions(":secret get "));
    }

    [Fact]
    public void GetSuggestions_ForEditDelegatesToScriptDiscovery()
    {
        var host = new CommandServiceHost();
        host.Scripts.Scripts.Add(new TTMG.ScriptMetadata { DisplayName = "my-script", FullPath = "my-script.lua" });
        host.Scripts.Scripts.Add(new TTMG.ScriptMetadata { DisplayName = "other", FullPath = "other.lua" });

        Assert.Equal(new[] { "my-script" }, host.Service.GetSuggestions(":edit my"));
    }

    [Fact]
    public void GetSuggestions_ForUnknownOrEmptyInput_ReturnsNothing()
    {
        var host = new CommandServiceHost();

        Assert.Empty(host.Service.GetSuggestions(":unknown"));
        Assert.Empty(host.Service.GetSuggestions(":"));
        Assert.Empty(host.Service.GetSuggestions(""));
    }
}
