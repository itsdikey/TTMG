namespace TTMG.Interfaces
{
    public interface IConfigService
    {
        AppConfig Config { get; }
        void LoadConfig();
        void SaveConfig();
        void EnsureDefaultConfig();
    }
}
