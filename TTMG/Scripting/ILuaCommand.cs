using Lua;

namespace TTMG.Scripting
{
    public interface ILuaCommand
    {
        LuaValue Execute(LuaCommandContext context, ReadOnlySpan<LuaValue> args);
        LuaValue DryRun(LuaCommandContext context, ReadOnlySpan<LuaValue> args);
    }
}
