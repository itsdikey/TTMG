using TTMG;
using TTMG.Interfaces;

namespace TTMG.Tests.TestDoubles;

public sealed class FakeConfigService : IConfigService
{
    public AppConfig Config { get; set; } = new();
    public string DataDirectory { get; set; } = Path.GetTempPath();
    public string? LoadedConfig { get; set; }

    public void LoadConfig() { }
    public void SaveConfig() { }
    public void EnsureDefaultConfig() { }
}
