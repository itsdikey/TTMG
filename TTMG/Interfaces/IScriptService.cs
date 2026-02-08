namespace TTMG.Interfaces
{
    public interface IScriptService
    {
        List<ScriptMetadata> DiscoverScripts();
        Task RunScript(string path);
        Task CreateNewScript(string name);
        void OpenInEditor(string filePath);
    }
}
