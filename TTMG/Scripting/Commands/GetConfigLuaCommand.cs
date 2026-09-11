using Lua;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "get_config",
        "Reads a value from the config.yaml file next to the script.",
        "get_config(key)",
        Parameters = new[] { "key: The configuration key to read." },
        Example = "local region = ttmg.get_config(\"region\")")]
    public sealed class GetConfigLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var key = LuaCommandArgs.GetString(args, 0, "key");
            return context.GetConfigValue(key);
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args) => string.Empty;
    }
}
