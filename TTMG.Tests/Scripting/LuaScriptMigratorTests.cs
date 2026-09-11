using TTMG.Scripting;
using Xunit;

namespace TTMG.Tests.Scripting;

public class LuaScriptMigratorTests
{
    [Theory]
    [InlineData("print('x')", "ttmg.print('x')")]
    [InlineData("prompt_input('t')", "ttmg.prompt_input('t')")]
    [InlineData("prompt_select('t', {'a'})", "ttmg.prompt_select('t', {'a'})")]
    [InlineData("run_process('c', 'a', false)", "ttmg.run_process('c', 'a', false)")]
    [InlineData("run_shell('c', false)", "ttmg.run_shell('c', false)")]
    [InlineData("get_secret('s')", "ttmg.get_secret('s')")]
    [InlineData("get_config('k')", "ttmg.get_config('k')")]
    public void LegacyCalls_AreRewrittenToCanonicalForm(string input, string expected)
    {
        var result = LuaScriptMigrator.Migrate(input);

        Assert.Equal(expected, result.Text);
        Assert.Equal(1, result.ReplacementCount);
        Assert.True(result.Changed);
    }

    [Theory]
    [InlineData("ttmg.print('x')")]
    [InlineData("ttmg.prompt_input('x'); ttmg.get_config('k')")]
    [InlineData("ttmg . print ('x')")]
    [InlineData("local s = \"print('x')\"")]
    [InlineData("local s = 'print(1)'")]
    [InlineData("local s = [[print(1)]]")]
    [InlineData("local s = [==[print(1)]==]")]
    [InlineData("-- print(1)")]
    [InlineData("--[[ print(1) ]]")]
    [InlineData("--[==[ print(1) ]==]")]
    [InlineData("myprint('x')")]
    [InlineData("print2('x')")]
    [InlineData("_print('x')")]
    [InlineData("foo.print('x')")]
    [InlineData("foo:print('x')")]
    [InlineData("a.b.print('x')")]
    [InlineData("function print(x) end")]
    [InlineData("local function print(x) end")]
    [InlineData("local print = 1")]
    [InlineData("local print, x = 1, 2")]
    [InlineData("function foo.print(x) end")]
    [InlineData("local t = { print = 1 }")]
    [InlineData("local t = { ['print'] = 1 }")]
    [InlineData("local t = { print = function() end }")]
    [InlineData("local t = { print = print }")]
    [InlineData("local f = print")]
    [InlineData("return print")]
    [InlineData("local t = { print }")]
    [InlineData("local print = function() end\nprint('x')")]
    public void ProtectedOccurrences_AreLeftUnchanged(string input)
    {
        var result = LuaScriptMigrator.Migrate(input);

        Assert.Equal(input, result.Text);
        Assert.Equal(0, result.ReplacementCount);
        Assert.False(result.Changed);
    }

    [Theory]
    [InlineData("print('a')\nprint('b')\n", "ttmg.print('a')\nttmg.print('b')\n", 2)]
    [InlineData("print('print(1)')", "ttmg.print('print(1)')", 1)]
    [InlineData("-- print(1)\nprint(2)", "-- print(1)\nttmg.print(2)", 1)]
    [InlineData("--[[ print(1) ]] print(2)", "--[[ print(1) ]] ttmg.print(2)", 1)]
    [InlineData("local t = { print = print('x') }", "local t = { print = ttmg.print('x') }", 1)]
    [InlineData("print ('x')", "ttmg.print ('x')", 1)]
    [InlineData("print --[[c]] ('x')", "ttmg.print --[[c]] ('x')", 1)]
    [InlineData("local s = 'a' .. print('b')", "local s = 'a' .. ttmg.print('b')", 1)]
    [InlineData("local function f() print('x') end", "local function f() ttmg.print('x') end", 1)]
    [InlineData("do local print = 1 print('x') end\nprint('y')", "do local print = 1 print('x') end\nttmg.print('y')", 1)]
    [InlineData("if true then local print = 1 print('a') else print('b') end", "if true then local print = 1 print('a') else ttmg.print('b') end", 1)]
    public void CallsInComplexContexts_AreRewrittenExactly(string input, string expected, int expectedCount)
    {
        var result = LuaScriptMigrator.Migrate(input);

        Assert.Equal(expected, result.Text);
        Assert.Equal(expectedCount, result.ReplacementCount);
    }

    [Fact]
    public void EmptySource_ReturnsEmptyResult()
    {
        var result = LuaScriptMigrator.Migrate(string.Empty);

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0, result.ReplacementCount);
    }
}
