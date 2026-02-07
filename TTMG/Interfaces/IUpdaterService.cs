namespace TTMG.Interfaces
{
    public interface IUpdaterService
    {
        Task CheckForUpdates(bool manual = false);
        Task InstallScripts(string repoName, string[] scriptNames);
    }
}
