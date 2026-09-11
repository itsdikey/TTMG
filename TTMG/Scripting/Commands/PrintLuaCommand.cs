using Lua;
using Spectre.Console;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "print",
        "Prints text to the console. Spectre.Console markup such as [red]text[/] is supported.",
        "print(text)",
        Parameters = new[] { "text: The text to print." },
        Example = "ttmg.print(\"[green]Hello, world![/]\")")]
    public sealed class PrintLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            AnsiConsole.MarkupLine(LuaCommandArgs.GetString(args, 0, "text"));
            return LuaValue.Nil;
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args) => LuaValue.Nil;
    }
}
