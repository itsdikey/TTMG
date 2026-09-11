using Lua;
using Spectre.Console;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "prompt_input",
        "Displays a text input prompt and returns the entered string. When a default is supplied it is shown and returned on Enter.",
        "prompt_input(title[, default])",
        Parameters = new[]
        {
            "title: The prompt shown to the user.",
            "default (optional): Value returned when the user presses Enter without typing."
        },
        Example = "local name = ttmg.prompt_input(\"Callsign:\", \"Maverick\")")]
    public sealed class PromptInputLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var title = LuaCommandArgs.GetString(args, 0, "title");
            var defaultValue = LuaCommandArgs.GetOptionalString(args, 1);

            if (defaultValue == null)
            {
                return AnsiConsole.Ask<string>(title);
            }

            return AnsiConsole.Prompt(new TextPrompt<string>(title).DefaultValue(defaultValue).ShowDefaultValue());
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            return LuaCommandArgs.GetOptionalString(args, 1) ?? string.Empty;
        }
    }
}
