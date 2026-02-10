namespace TTMG.Interfaces
{
    public interface IConfigService
    {
        AppConfig Config { get; }
        string DataDirectory { get; }
        string? LoadedConfig { get; }

        void LoadConfig();
        void SaveConfig();
        void EnsureDefaultConfig();
    }
}
