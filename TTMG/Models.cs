namespace TTMG
{
    public class ScriptEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Alias { get; set; } = "";
    }

    public class CommandEntry
    {
        public string Code { get; set; } = "";
        public string Action { get; set; } = "";
    }

    public class RepoConfig
    {
        public string ShortName { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Token { get; set; }
    }

    public class AppConfig
    {
        public bool IMakeNoMistakes { get; set; } = false;
        public bool SuppressUpdateChecks { get; set; } = false;
        public string UpdateDirectory { get; set; } = "update";
        public string UserScriptsDirectory { get; set; } = "";
        public string VersionUrl { get; set; } = "";
        public string CurrentVersion { get; set; } = "1.0.0";
        public string DefaultShell { get; set; } = "cmd"; // cmd, powershell, pwsh, bash, zsh
        public string DefaultEditor { get; set; } = "notepad";
        public string EditorArgs { get; set; } = "{file}";
        public List<ScriptEntry> Scripts { get; set; } = new();
        public List<CommandEntry> Commands { get; set; } = new();
        public List<RepoConfig> Repositories { get; set; } = new();
    }

    public class ScriptMetadata
    {
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string? Alias { get; set; }
        public int Index { get; set; }
        public bool IsCommand { get; set; }
    }

    public class VersionInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }
}
