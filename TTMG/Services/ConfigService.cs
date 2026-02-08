using System.Text;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TTMG.Interfaces;

namespace TTMG.Services
{
    public class ConfigService : IConfigService
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        private AppConfig _config = new();

        public AppConfig Config => _config;

        public ConfigService()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public void LoadConfig()
        {
            var configPath = GetConfigPath();
            if (File.Exists(configPath))
            {
                try
                {
                    _config = _deserializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config: {ex.Message}");
                }

                // Ensure system commands exist in the loaded config
                bool updated = false;
                if (_config.Commands == null)
                {
                    _config.Commands = new List<CommandEntry>();
                    updated = true;
                }

                var systemCommands = new List<CommandEntry>
                {
                    new () { Code = ":create", Action = "create_script" },
                    new () { Code = ":update", Action = "check_updates" },
                    new () { Code = ":version", Action = "print_version" },
                    new () { Code = ":qq", Action = "exit" },
                    new () { Code = ":wq", Action = "exit" }
                };

                foreach (var sys in systemCommands)
                {
                    if (!_config.Commands.Any(c => string.Equals(c.Code, sys.Code, StringComparison.OrdinalIgnoreCase)))
                    {
                        _config.Commands.Add(sys);
                        updated = true;
                    }
                }

                if (updated)
                {
                    try
                    {
                        var yaml = _serializer.Serialize(_config);
                        File.WriteAllText(configPath, yaml, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving updated config: {ex.Message}");
                    }
                }
            }
            else
            {
                EnsureDefaultConfig();
            }
        }

        public void EnsureDefaultConfig()
        {
            var configPath = GetConfigPath();
            if (File.Exists(configPath)) return;

            string defaultEditor = "notepad";
            string defaultShell = "cmd";

            if (OperatingSystem.IsMacOS())
            {
                defaultEditor = "open";
                defaultShell = "zsh";
            }
            else if (OperatingSystem.IsLinux())
            {
                defaultEditor = "vi";
                defaultShell = "bash";
            }

            _config = new AppConfig
            {
                VersionUrl = "https://github.com/itsdikey/TTMG/releases/download/v1.1.0/version.json",
                DefaultShell = defaultShell,
                DefaultEditor = defaultEditor,
                EditorArgs = "{file}",
                UserScriptsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TTMG-Scripts"),
                IMakeNoMistakes = false,
                Commands = new List<CommandEntry>
                {
                    new () { Code = ":create", Action = "create_script" },
                    new () { Code = ":update", Action = "check_updates" },
                    new () { Code = ":version", Action = "print_version" },
                    new () { Code = ":qq", Action = "exit" },
                    new () { Code = ":wq", Action = "exit" }
                }
            };

            var yaml = _serializer.Serialize(_config);
            File.WriteAllText(configPath, yaml, Encoding.UTF8);
        }

        private string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts.yaml");
        }
    }
}
