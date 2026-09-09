namespace TTMG.Interfaces
{
    public interface IScriptService
    {
        List<ScriptMetadata> DiscoverScripts();
        Task RunScript(string path);
        Task CreateNewScript(string name);
        void OpenInEditor(string filePath);

        /// <summary>
        /// Resolves a discovered script by display name or alias, case-insensitively.
        /// Returns the single match, or null after printing a clear message when there
        /// is no match or the name is ambiguous.
        /// </summary>
        ScriptMetadata? ResolveScript(string nameOrAlias);

        /// <summary>Deletes the given Lua script file.</summary>
        void DeleteScript(string fullPath);
    }
}
