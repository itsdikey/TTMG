using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":edit", "edit_script")]
    public class EditCommand : ICommand
    {
        private readonly IScriptService _scriptService;

        public EditCommand(IScriptService scriptService)
        {
            _scriptService = scriptService;
        }

        public Task Execute(string[] args)
        {
            string? name = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            if (name == null)
            {
                name = AnsiConsole.Ask<string>("Enter the name or alias of the script to edit:");
            }

            var script = _scriptService.ResolveScript(name);
            if (script == null) return Task.CompletedTask;

            _scriptService.OpenInEditor(script.FullPath);
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetSuggestions(string[] args)
        {
            return ScriptNameSuggestions(_scriptService, args);
        }

        internal static IEnumerable<string> ScriptNameSuggestions(IScriptService scriptService, string[] args)
        {
            if (args.Length > 1) return Enumerable.Empty<string>();

            var input = args.Length == 1 ? args[0] : "";
            var suggestions = new List<string>();
            foreach (var script in scriptService.DiscoverScripts())
            {
                if (script.DisplayName.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(script.DisplayName);
                }
                if (!string.IsNullOrEmpty(script.Alias) && script.Alias.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(script.Alias);
                }
            }
            return suggestions.Distinct();
        }
    }
}
