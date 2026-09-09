using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":delete", "delete_script")]
    public class DeleteCommand : ICommand
    {
        private readonly IScriptService _scriptService;

        public DeleteCommand(IScriptService scriptService)
        {
            _scriptService = scriptService;
        }

        public Task Execute(string[] args)
        {
            string? name = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;
            if (name == null)
            {
                name = AnsiConsole.Ask<string>("Enter the name or alias of the script to delete:");
            }

            var script = _scriptService.ResolveScript(name);
            if (script == null) return Task.CompletedTask;

            if (!AnsiConsole.Confirm($"[yellow]Delete script[/] '[cyan]{script.DisplayName}[/]' ([grey]{script.FullPath}[/])?", false))
            {
                AnsiConsole.MarkupLine("[grey]Deletion cancelled.[/]");
                return Task.CompletedTask;
            }

            _scriptService.DeleteScript(script.FullPath);
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetSuggestions(string[] args)
        {
            return EditCommand.ScriptNameSuggestions(_scriptService, args);
        }
    }
}
