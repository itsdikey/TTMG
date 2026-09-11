using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TTMG.Interfaces;
using TTMG.Scripting;
using TTMG.Services;

namespace TTMG
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--emit-lua-docs")
            {
                var outputPath = args.Length > 1 ? args[1] : Path.Combine("docs", "lua-api.md");
                LuaApiDocsGenerator.WriteToFile(outputPath);
                Console.WriteLine($"Lua API documentation written to {Path.GetFullPath(outputPath)}");
                return;
            }

            var host = CreateHostBuilder(args).Build();
            var appService = host.Services.GetRequiredService<IAppService>();
            await appService.Run(args);
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IConfigService, ConfigService>();
                    services.AddSingleton<ISecretService, SecretService>();
                    services.AddSingleton<IScriptService, ScriptService>();
                    services.AddSingleton<IUpdaterService, UpdaterService>();
                    services.AddSingleton<ICommandService, CommandService>();
                    services.AddSingleton<IAppService, AppService>();
                });
    }
}