using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":config", "system_config")]
    public class SystemConfigCommand : ICommand
    {
        private readonly IScriptService _scriptService;
        private readonly IConfigService _configService;

        public SystemConfigCommand(IScriptService scriptService, IConfigService configService)
        {
            _scriptService = scriptService;
            _configService=configService;
        }

        public Task Execute(string[] args)
        {
            string? name = args.Length > 0 ? args[0] : null;
            if (!string.IsNullOrEmpty(name))
            {
                AnsiConsole.MarkupLine("[yellow]Script configs are not supported yet :(!");
                return Task.CompletedTask;
            }

            if(string.IsNullOrEmpty(_configService.LoadedConfig))
            {
                AnsiConsole.MarkupLine("[yellow]No config found![/]");
                return Task.CompletedTask;
            }

            _scriptService.OpenInEditor(_configService.LoadedConfig);
            return Task.CompletedTask;
        }
    }
}
