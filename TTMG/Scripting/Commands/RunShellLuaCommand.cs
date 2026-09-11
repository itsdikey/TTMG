using Lua;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "run_shell",
        "Runs a command using the default shell configured in scripts.yaml.",
        "run_shell(command, detached)",
        Parameters = new[]
        {
            "command: The shell command to run.",
            "detached: true to launch without waiting; false to wait for the process to exit."
        },
        Example = "ttmg.run_shell(\"echo hello\", false)")]
    public sealed class RunShellLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var command = LuaCommandArgs.GetString(args, 0, "command");
            var detached = LuaCommandArgs.GetBoolean(args, 1, "detached");

            var (shell, argsPrefix) = LuaEnv.GetShellInfo(context.Config.DefaultShell);
            LuaEnv.ExecuteProcess(shell, $"{argsPrefix} \"{command.Replace("\"", "\\\"")}\"", detached);
            return LuaValue.Nil;
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args) => LuaValue.Nil;
    }
}
