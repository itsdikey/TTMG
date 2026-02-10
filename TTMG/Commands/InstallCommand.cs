using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":install", "install_scripts")]
    public class InstallCommand : ICommand
    {
        private readonly IUpdaterService _updaterService;

        public InstallCommand(IUpdaterService updaterService)
        {
            _updaterService = updaterService;
        }

        public async Task Execute(string[] args)
        {
            if (args.Length >= 2)
            {
                await _updaterService.InstallScripts(args[0], args.Skip(1).ToArray());
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Usage: :install <repo> <script1> <script2>...[/]");
            }
        }
    }
}
