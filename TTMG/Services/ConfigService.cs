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
        private readonly string _dataDirectory;

        public AppConfig Config => _config;
        public string DataDirectory => _dataDirectory;

        public ConfigService()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _dataDirectory = DetermineDataDirectory();
        }

        private string DetermineDataDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                var testFile = Path.Combine(baseDir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return baseDir;
            }
            catch
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var ttmgData = Path.Combine(appData, "TTMG");
                if (!Directory.Exists(ttmgData))
                {
                    Directory.CreateDirectory(ttmgData);
                }
                return ttmgData;
            }
        }

        public void LoadConfig()
        {
            var configPath = GetConfigPath();
            var baseConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts.yaml");
            
            string? pathToLoad = null;
            if (File.Exists(configPath))
            {
                pathToLoad = configPath;
            }
            else if (File.Exists(baseConfigPath))
            {
                pathToLoad = baseConfigPath;
            }

            if (pathToLoad != null)
            {
                try
                {
                    _config = _deserializer.Deserialize<AppConfig>(File.ReadAllText(pathToLoad));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error loading config:[/] {ex.Message}");
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
                    new () { Code = ":secret", Action = "manage_secrets" },
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
                    SaveConfig();
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
                    new () { Code = ":secret", Action = "manage_secrets" },
                    new () { Code = ":qq", Action = "exit" },
                    new () { Code = ":wq", Action = "exit" }
                }
            };

            SaveConfig();
        }

        public void SaveConfig()
        {
            try
            {
                var configPath = GetConfigPath();
                var yaml = _serializer.Serialize(_config);
                File.WriteAllText(configPath, yaml, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error saving config:[/] {ex.Message}");
            }
        }

        private string GetConfigPath()
        {
            return Path.Combine(_dataDirectory, "scripts.yaml");
        }
    }
}
