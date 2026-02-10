using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TTMG.Interfaces;
using Spectre.Console;

namespace TTMG.Services
{
    public class ConfigService : IConfigService
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        private readonly ICommandService _commandService;
        private AppConfig _config = new();
        private readonly string _dataDirectory;

        public AppConfig Config => _config;
        public string DataDirectory => _dataDirectory;
        public string? LoadedConfig { get; private set; }

        public ConfigService(ICommandService commandService)
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _dataDirectory = DetermineDataDirectory();
            _commandService=commandService;
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

                    LoadedConfig = pathToLoad;

                    EnsureSystemCommandsExist();
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error loading config:[/] {ex.Message}");
                }
            }
            else
            {
                EnsureDefaultConfig();
            }
        }

        private void EnsureSystemCommandsExist()
        {
            if (_config==null)
            { 
                return; 
            }

            var existingCommandNames = _config.Commands.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool isConfigModified = false;

            foreach (var sysCmd in _commandService.SystemCommands)
            {
                if (!existingCommandNames.Contains(sysCmd.Code))
                {
                    _config.Commands.Add(sysCmd);
                    isConfigModified = true;
                }
            }

            if(isConfigModified)
            {
                SaveConfig();
            }
        }

        public void EnsureDefaultConfig()
        {
            var configPath = GetConfigPath();
            if (File.Exists(configPath))
                return;

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
                Commands = _commandService.SystemCommands
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
