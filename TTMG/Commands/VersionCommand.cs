using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":version", "print_version")]
    public class VersionCommand : ICommand
    {
        public Task Execute(string[] args)
        {
            string version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            AnsiConsole.MarkupLine($"[bold cyan]TTMG version[/] [green]{version}[/]");
            return Task.CompletedTask;
        }
    }
}
