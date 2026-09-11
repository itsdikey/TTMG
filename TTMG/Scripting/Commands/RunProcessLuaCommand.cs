using Lua;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "run_process",
        "Runs an external process with arguments.",
        "run_process(command, args, detached)",
        Parameters = new[]
        {
            "command: The executable to run.",
            "args: The command-line arguments string.",
            "detached: true to launch without waiting; false to wait for the process to exit."
        },
        Example = "ttmg.run_process(\"cmd.exe\", \"/c dir\", false)")]
    public sealed class RunProcessLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var command = LuaCommandArgs.GetString(args, 0, "command");
            var arguments = LuaCommandArgs.GetString(args, 1, "args");
            var detached = LuaCommandArgs.GetBoolean(args, 2, "detached");

            LuaEnv.ExecuteProcess(command, arguments, detached);
            return LuaValue.Nil;
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args) => LuaValue.Nil;
    }
}
