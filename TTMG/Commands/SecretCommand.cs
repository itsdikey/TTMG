using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":secret", "manage_secrets")]
    public class SecretCommand : ICommand
    {
        private readonly ISecretService _secretService;

        public SecretCommand(ISecretService secretService)
        {
            _secretService = secretService;
        }

        public Task Execute(string[] args)
        {
            if (args.Length >= 1)
            {
                var subCommand = args[0].ToLower();
                if (subCommand == "create" && args.Length >= 2)
                {
                    _secretService.CreateSecret(args[1]);
                }
                else if (subCommand == "list")
                {
                    var secrets = _secretService.ListSecrets();
                    if (secrets.Any())
                    {
                        AnsiConsole.MarkupLine("[bold cyan]Available secrets:[/]");
                        foreach (var s in secrets) AnsiConsole.MarkupLine($"- {s}");
                    }
                    else AnsiConsole.MarkupLine("[grey]No secrets found.[/]");
                }
                else if (subCommand == "get" && args.Length >= 2)
                {
                    var val = _secretService.GetSecret(args[1]);
                    if (val != null) AnsiConsole.MarkupLine($"Secret [yellow]{args[1]}[/]: [green]{val}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Usage: :secret <create|list|get> [name][/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Usage: :secret <create|list|get> [[name]][/]");
            }
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetSuggestions(string[] args)
        {
            if (args.Length <= 1)
            {
                var subCommands = new[] { "create", "list", "get" };
                var input = args.Length == 1 ? args[0] : "";
                return subCommands.Where(s => s.StartsWith(input, StringComparison.OrdinalIgnoreCase));
            }
            
            if (args.Length == 2 && (args[0].Equals("get", StringComparison.OrdinalIgnoreCase) || args[0].Equals("create", StringComparison.OrdinalIgnoreCase)))
            {
                var secrets = _secretService.ListSecrets();
                return secrets.Where(s => s.StartsWith(args[1], StringComparison.OrdinalIgnoreCase));
            }

            return Enumerable.Empty<string>();
        }
    }
}
