namespace TTMG.Interfaces
{
    public interface IConfigService
    {
        AppConfig Config { get; }
        string DataDirectory { get; }
        void LoadConfig();
        void SaveConfig();
        void EnsureDefaultConfig();
    }
}
