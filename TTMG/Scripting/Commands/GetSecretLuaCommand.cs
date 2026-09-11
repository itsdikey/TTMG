using Lua;

namespace TTMG.Scripting.Commands
{
    [LuaCommand(
        "get_secret",
        "Retrieves an encrypted secret from the store.",
        "get_secret(name)",
        Parameters = new[] { "name: The name of the secret to retrieve." },
        Example = "local token = ttmg.get_secret(\"github_token\")",
        AnalyzesSecret = true)]
    public sealed class GetSecretLuaCommand : ILuaCommand
    {
        public LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            var name = LuaCommandArgs.GetString(args, 0, "name");
            var value = context.SecretService.GetSecret(name, context.SharedSecretPassword);
            return value == null ? LuaValue.Nil : value;
        }

        public LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args)
        {
            if (context.Analysis != null)
            {
                context.Analysis.RequiresSecret = true;
            }

            return LuaValue.Nil;
        }
    }
}
