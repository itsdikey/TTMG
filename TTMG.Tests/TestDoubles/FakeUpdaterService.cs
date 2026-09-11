using TTMG.Interfaces;

namespace TTMG.Tests.TestDoubles;

public sealed class FakeUpdaterService : IUpdaterService
{
    public List<bool> CheckForUpdatesCalls { get; } = new();
    public List<(string Repo, string[] Scripts)> Installs { get; } = new();

    public Task CheckForUpdates(bool manual = false)
    {
        CheckForUpdatesCalls.Add(manual);
        return Task.CompletedTask;
    }

    public Task InstallScripts(string repoName, string[] scriptNames)
    {
        Installs.Add((repoName, scriptNames));
        return Task.CompletedTask;
    }
}
