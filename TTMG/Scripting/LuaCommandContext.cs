using TTMG.Interfaces;

namespace TTMG.Scripting
{
    public sealed class LuaCommandContext
    {
        public AppConfig Config { get; }
        public ISecretService SecretService { get; }
        public string ScriptPath { get; }
        public IReadOnlyDictionary<string, string> ScriptConfig { get; }
        public string? SharedSecretPassword { get; }
        public ScriptAnalysis? Analysis { get; }

        public LuaCommandContext(
            AppConfig config,
            ISecretService secretService,
            string scriptPath,
            IReadOnlyDictionary<string, string> scriptConfig,
            string? sharedSecretPassword,
            ScriptAnalysis? analysis = null)
        {
            Config = config;
            SecretService = secretService;
            ScriptPath = scriptPath;
            ScriptConfig = scriptConfig;
            SharedSecretPassword = sharedSecretPassword;
            Analysis = analysis;
        }

        public string GetConfigValue(string key)
        {
            if (ScriptConfig.TryGetValue(key, out var value))
            {
                return value;
            }

            throw new Exception($"Configuration variable '{key}' is missing in config.yaml");
        }
    }
}
