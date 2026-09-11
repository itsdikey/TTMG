using Microsoft.Extensions.DependencyInjection;
using TTMG.Interfaces;
using TTMG.Services;

namespace TTMG.Tests.TestDoubles;

public sealed class CommandServiceHost
{
    public CommandService Service { get; }
    public FakeScriptService Scripts { get; } = new();
    public FakeSecretService Secrets { get; } = new();
    public FakeUpdaterService Updater { get; } = new();
    public FakeConfigService Config { get; } = new();

    public CommandServiceHost()
    {
        var services = new ServiceCollection()
            .AddSingleton<IScriptService>(Scripts)
            .AddSingleton<ISecretService>(Secrets)
            .AddSingleton<IUpdaterService>(Updater)
            .AddSingleton<IConfigService>(Config)
            .BuildServiceProvider();

        Service = new CommandService(services);
    }
}
