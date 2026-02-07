using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TTMG.Interfaces;
using TTMG.Services;

namespace TTMG
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var appService = host.Services.GetRequiredService<IAppService>();
            await appService.Run(args);
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IConfigService, ConfigService>();
                    services.AddSingleton<IScriptService, ScriptService>();
                    services.AddSingleton<IUpdaterService, UpdaterService>();
                    services.AddSingleton<IAppService, AppService>();
                });
    }
}