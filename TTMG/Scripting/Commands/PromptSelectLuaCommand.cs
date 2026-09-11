using Lua;
using Spectre.Console;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "prompt_select",
        "Displays a selection menu and returns the chosen option.",
        "prompt_select(title, options)",
        Parameters = new[]
        {
            "title: The prompt shown above the options.",
            "options: A Lua array of option strings."
        },
        Example = "local task = ttmg.prompt_select(\"Objective:\", { \"Scan\", \"Return\" })")]
    public sealed class PromptSelectLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var title = LuaCommandArgs.GetString(args, 0, "title");
            var table = LuaCommandArgs.GetTable(args, 1, "options");
            var options = table.Select(pair => pair.Value.ToString() ?? "").ToList();
            return AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).PageSize(10).AddChoices(options));
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            if (args.Length > 1 && args[1].Type == LuaValueType.Table)
            {
                var table = args[1].Read<LuaTable>();
                var first = table[1];
                if (first.Type != LuaValueType.Nil)
                {
                    return first.ToString();
                }
            }

            return string.Empty;
        }
    }
}
