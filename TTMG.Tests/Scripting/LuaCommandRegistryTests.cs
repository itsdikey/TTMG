using Lua;
using Lua.Standard;
using TTMG;
using TTMG.Scripting;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Scripting;

public class LuaCommandRegistryTests
{
    private static readonly string[] LegacyNames =
    {
        "print", "prompt_input", "prompt_select", "run_process", "run_shell", "get_secret", "get_config"
    };

    [Fact]
    public void Registry_ExposesExactlyTheSupportedCommands()
    {
        var names = LuaCommandRegistry.Default.Commands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "get_config", "get_secret", "print", "prompt_input", "prompt_select", "run_process", "run_shell" }, names);
        Assert.True(LuaCommandRegistry.Default.Commands.Single(c => c.Name == "get_secret").AnalyzesSecret);
    }

    [Fact]
    public async Task RealBinding_RegistersCanonicalTableAndMatchingLegacyGlobals()
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var context = new LuaCommandContext(new AppConfig(), new FakeSecretService(), "script.lua", new Dictionary<string, string>(), null);
        LuaCommandRegistry.Default.RegisterEnvironment(state, context);

        var aliasChecks = string.Join(", ", LegacyNames.Select(n => $"ttmg.{n} == {n}"));
        var results = await state.DoStringAsync($"return {aliasChecks}, type(ttmg), env == nil, pass == nil");

        for (var i = 0; i < LegacyNames.Length; i++)
        {
            Assert.True(results[i].ToBoolean(), $"ttmg.{LegacyNames[i]} should alias {LegacyNames[i]}");
        }

        Assert.Equal("table", results[LegacyNames.Length].ToString());
        Assert.True(results[LegacyNames.Length + 1].ToBoolean());
        Assert.True(results[LegacyNames.Length + 2].ToBoolean());
    }

    [Fact]
    public async Task GetConfig_ReturnsConfiguredValueAndThrowsWhenMissing()
    {
        using var state = LuaState.Create();
        var context = new LuaCommandContext(
            new AppConfig(),
            new FakeSecretService(),
            "script.lua",
            new Dictionary<string, string> { ["region"] = "eu" },
            null);
        LuaCommandRegistry.Default.RegisterEnvironment(state, context);

        var value = await state.DoStringAsync("return ttmg.get_config('region')");
        Assert.Equal("eu", value[0].Read<string>());

        await Assert.ThrowsAsync<LuaRuntimeException>(() => state.DoStringAsync("return ttmg.get_config('missing')").AsTask());
    }

    [Fact]
    public async Task GetSecret_UsesSharedPasswordFromContext()
    {
        using var state = LuaState.Create();
        var secrets = new FakeSecretService { Value = "token-value" };
        var context = new LuaCommandContext(new AppConfig(), secrets, "script.lua", new Dictionary<string, string>(), "shared-password");
        LuaCommandRegistry.Default.RegisterEnvironment(state, context);

        var value = await state.DoStringAsync("return get_secret('token')");

        Assert.Equal("token-value", value[0].Read<string>());
        var request = Assert.Single(secrets.Requested);
        Assert.Equal("token", request.Name);
        Assert.Equal("shared-password", request.Password);
    }

    [Fact]
    public async Task DryRun_ReturnsSafeMocksForPromptCommands()
    {
        using var state = LuaState.Create();
        var analysis = new ScriptAnalysis();
        var context = new LuaCommandContext(new AppConfig(), new FakeSecretService(), "script.lua", new Dictionary<string, string>(), null, analysis);
        LuaCommandRegistry.Default.RegisterDryRunEnvironment(state, context, analysis);

        var results = await state.DoStringAsync(
            "run_process('cmd.exe', '/c echo hi', false); run_shell('echo hi', false); " +
            "return ttmg.prompt_input('t', 'defaulted'), prompt_select('t', {'first', 'second'}), get_config('k')");

        Assert.Equal("defaulted", results[0].Read<string>());
        Assert.Equal("first", results[1].Read<string>());
        Assert.Equal(string.Empty, results[2].Read<string>());
        Assert.False(analysis.RequiresSecret);
    }

    [Fact]
    public async Task DryRun_GetSecretMarksSecretRequirement()
    {
        using var state = LuaState.Create();
        var analysis = new ScriptAnalysis();
        var context = new LuaCommandContext(new AppConfig(), new FakeSecretService(), "script.lua", new Dictionary<string, string>(), null, analysis);
        LuaCommandRegistry.Default.RegisterDryRunEnvironment(state, context, analysis);

        await Assert.ThrowsAsync<LuaRuntimeException>(() => state.DoStringAsync("local s = ttmg.get_secret('token')").AsTask());

        Assert.True(analysis.RequiresSecret);
        Assert.False(analysis.RequiresStd);
    }
}
