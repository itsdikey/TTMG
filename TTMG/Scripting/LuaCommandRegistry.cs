using System.Reflection;
using Lua;

namespace TTMG.Scripting
{
    public sealed class LuaCommandRegistry
    {
        private static readonly Lazy<LuaCommandRegistry> _default = new(() => new LuaCommandRegistry());

        public static LuaCommandRegistry Default => _default.Value;

        private readonly List<Entry> _entries;

        public LuaCommandRegistry()
        {
            _entries = Discover();
        }

        public IReadOnlyList<LuaCommandMetadata> Commands =>
            _entries.Select(e => e.Metadata).ToList();

        public void RegisterEnvironment(LuaState state, LuaCommandContext context)
        {
            var table = BuildTable(context, analysis: null);
            Register(state, table);
        }

        public void RegisterDryRunEnvironment(LuaState state, LuaCommandContext context, ScriptAnalysis analysis)
        {
            var table = BuildTable(context, analysis);
            Register(state, table);
        }

        private static List<Entry> Discover()
        {
            var entries = new List<Entry>();

            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => !t.IsAbstract && typeof(ILuaCommand).IsAssignableFrom(t));

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<LuaCommandAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Lua command '{type.FullName}' must expose a public parameterless constructor.");
                }

                var command = (ILuaCommand)Activator.CreateInstance(type)!;
                var metadata = new LuaCommandMetadata(
                    attribute.Name,
                    attribute.Signature,
                    attribute.Help,
                    attribute.Parameters,
                    attribute.Example,
                    attribute.AnalyzesSecret);

                entries.Add(new Entry(metadata, command));
            }

            return entries
                .OrderBy(e => e.Metadata.Name, StringComparer.Ordinal)
                .ToList();
        }

        private LuaTable BuildTable(LuaCommandContext context, ScriptAnalysis? analysis)
        {
            var table = new LuaTable();
            foreach (var entry in _entries)
            {
                table[entry.Metadata.Name] = CreateFunction(entry, context, analysis);
            }

            return table;
        }

        private static void Register(LuaState state, LuaTable table)
        {
            state.Environment["ttmg"] = table;

            foreach (var pair in table)
            {
                state.Environment[pair.Key.Read<string>()] = pair.Value;
            }
        }

        private static LuaFunction CreateFunction(Entry entry, LuaCommandContext context, ScriptAnalysis? analysis)
        {
            return new LuaFunction((executionContext, _) =>
            {
                if (analysis != null)
                {
                    if (entry.Metadata.AnalyzesSecret)
                    {
                        analysis.RequiresSecret = true;
                        throw new LuaRuntimeException(executionContext.State, "SECRET_DETECTED", 0);
                    }

                    var dryResult = entry.Command.DryRun(context, executionContext.Arguments);
                    return new ValueTask<int>(executionContext.Return(dryResult));
                }

                var result = entry.Command.Execute(context, executionContext.Arguments);
                return new ValueTask<int>(executionContext.Return(result));
            });
        }

        private sealed record Entry(LuaCommandMetadata Metadata, ILuaCommand Command);
    }
}
