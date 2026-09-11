using Lua;

namespace TTMG.Scripting
{
    internal static class LuaCommandArgs
    {
        public static string GetString(ReadOnlySpan<LuaValue> args, int index, string parameterName)
        {
            if (index >= args.Length || args[index].Type == LuaValueType.Nil)
            {
                throw new ArgumentException($"Missing required argument '{parameterName}'.");
            }

            return ToStringValue(args[index], parameterName);
        }

        public static string? GetOptionalString(ReadOnlySpan<LuaValue> args, int index)
        {
            if (index >= args.Length || args[index].Type == LuaValueType.Nil)
            {
                return null;
            }

            return ToStringValue(args[index], $"argument {index + 1}");
        }

        public static bool GetBoolean(ReadOnlySpan<LuaValue> args, int index, string parameterName)
        {
            if (index < args.Length && args[index].TryRead<bool>(out var value))
            {
                return value;
            }

            throw new ArgumentException($"Argument '{parameterName}' must be a boolean.");
        }

        public static LuaTable GetTable(ReadOnlySpan<LuaValue> args, int index, string parameterName)
        {
            if (index < args.Length && args[index].Type == LuaValueType.Table)
            {
                return args[index].Read<LuaTable>();
            }

            throw new ArgumentException($"Argument '{parameterName}' must be a table.");
        }

        private static string ToStringValue(LuaValue value, string parameterName)
        {
            if (value.Type == LuaValueType.String)
            {
                return value.Read<string>();
            }

            if (value.TryRead<string>(out var text) && text != null)
            {
                return text;
            }

            throw new ArgumentException($"Argument '{parameterName}' must be a string.");
        }
    }
}
