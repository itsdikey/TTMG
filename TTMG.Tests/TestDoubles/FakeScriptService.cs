using TTMG;
using TTMG.Interfaces;

namespace TTMG.Tests.TestDoubles;

public sealed class FakeScriptService : IScriptService
{
    public FakeScriptService(params string[] paths)
    {
        foreach (var path in paths)
        {
            Scripts.Add(new ScriptMetadata { DisplayName = Path.GetFileName(path), FullPath = path });
        }
    }

    public List<ScriptMetadata> Scripts { get; } = new();
    public ScriptMetadata? ResolveResult { get; set; }
    public List<string> ResolvedNames { get; } = new();
    public List<string> OpenedInEditor { get; } = new();
    public List<string> Created { get; } = new();
    public List<string> Deleted { get; } = new();
    public List<string> Run { get; } = new();

    public List<ScriptMetadata> DiscoverScripts() => Scripts;

    public Task RunScript(string path)
    {
        Run.Add(path);
        return Task.CompletedTask;
    }

    public Task CreateNewScript(string name)
    {
        Created.Add(name);
        return Task.CompletedTask;
    }

    public void OpenInEditor(string filePath) => OpenedInEditor.Add(filePath);

    public ScriptMetadata? ResolveScript(string nameOrAlias)
    {
        ResolvedNames.Add(nameOrAlias);
        return ResolveResult;
    }

    public void DeleteScript(string fullPath) => Deleted.Add(fullPath);
}
