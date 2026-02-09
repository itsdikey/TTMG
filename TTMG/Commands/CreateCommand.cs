using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":create", "create_script")]
    public class CreateCommand : ICommand
    {
        private readonly IScriptService _scriptService;

        public CreateCommand(IScriptService scriptService)
        {
            _scriptService = scriptService;
        }

        public async Task Execute(string[] args)
        {
            string? name = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrEmpty(name))
            {
                name = AnsiConsole.Ask<string>("Enter a name for your new script:");
            }
            await _scriptService.CreateNewScript(name);
        }
    }
}
