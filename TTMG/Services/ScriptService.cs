using Lua;
using Lua.Standard;
using Spectre.Console;
using TTMG.Interfaces;
using TTMG.Scripting;
using YamlDotNet.Serialization;

namespace TTMG.Services
{
    public class ScriptService : IScriptService
    {
        private readonly IConfigService _configService;
        private readonly ISecretService _secretService;
        private readonly IDeserializer _yamlDeserializer;

        public ScriptService(IConfigService configService, ISecretService secretService)
        {
            _configService = configService;
            _secretService = secretService;
            _yamlDeserializer = new DeserializerBuilder().Build();
        }

        public List<ScriptMetadata> DiscoverScripts()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = _configService.DataDirectory;
            var config = _configService.Config;
            
            EnsureHelloWorldExists(dataDir);

            var discoveredScripts = Discover(baseDir, config);

            if (dataDir != baseDir)
            {
                var dataScripts = Discover(dataDir, config);
                foreach (var ds in dataScripts)
                {
                    if (!discoveredScripts.Any(s => s.FullPath == ds.FullPath))
                    {
                        discoveredScripts.Add(ds);
                    }
                }
            }

            var userScriptsDir = GetUserScriptsDirectory();
            if (Directory.Exists(userScriptsDir))
            {
                var userScripts = Discover(userScriptsDir, config);
                foreach (var us in userScripts)
                {
                    if (!discoveredScripts.Any(ds => ds.FullPath == us.FullPath))
                    {
                        discoveredScripts.Add(us);
                    }
                }
            }
            return discoveredScripts;
        }

        private string GetUserScriptsDirectory()
        {
            var config = _configService.Config;
            return !string.IsNullOrEmpty(config.UserScriptsDirectory)
                ? Environment.ExpandEnvironmentVariables(config.UserScriptsDirectory)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TTMG-Scripts");
        }

        private List<ScriptMetadata> Discover(string rootDir, AppConfig config)
        {
            var scripts = new List<ScriptMetadata>();
            if (!Directory.Exists(rootDir)) return scripts;

            var allFiles = Directory.GetFiles(rootDir, "*.lua", new EnumerationOptions 
            { 
                RecurseSubdirectories = true, 
                AttributesToSkip = 0 
            });

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var dir = Path.GetDirectoryName(file) ?? "";
                var dirName = Path.GetFileName(dir);
                
                string displayName;
                if (fileName.Equals("init.lua", StringComparison.OrdinalIgnoreCase))
                {
                    displayName = dirName;
                }
                else
                {
                    displayName = Path.GetFileNameWithoutExtension(fileName);
                }

                scripts.Add(new ScriptMetadata { DisplayName = displayName.TrimStart('.'), FullPath = file });
            }

            // Recursive Disambiguation (Prepending folders)
            bool changed;
            do
            {
                changed = false;
                var groups = scripts.GroupBy(s => s.DisplayName).Where(g => g.Count() > 1);
                foreach (var group in groups)
                {
                    foreach (var item in group)
                    {
                        var relPath = Path.GetRelativePath(rootDir, item.FullPath);
                        var pathParts = relPath.Split(Path.DirectorySeparatorChar);
                        
                        var currentDisplayParts = item.DisplayName.Split('-');
                        int currentLevel = currentDisplayParts.Length;

                        bool isInit = Path.GetFileName(item.FullPath).Equals("init.lua", StringComparison.OrdinalIgnoreCase);
                        int levelsUsed = isInit ? currentLevel : currentLevel - 1;

                        if (pathParts.Length > levelsUsed + 1)
                        {
                            var parentDir = pathParts[pathParts.Length - 2 - levelsUsed];
                            item.DisplayName = $"{parentDir}-{item.DisplayName}".TrimStart('.');
                            changed = true;
                        }
                    }
                }
            } while (changed);

            // Map aliases from config
            foreach (var s in scripts)
            {
                var cfg = config.Scripts.FirstOrDefault(c => Path.GetFullPath(c.Path, rootDir) == Path.GetFullPath(s.FullPath, rootDir));
                if (cfg != null) s.Alias = cfg.Alias;
            }

            return scripts;
        }

        public async Task RunScript(string path)
        {
            if (!File.Exists(path))
            { AnsiConsole.MarkupLine($"[red]File not found:[/] {path}"); return; }
            var scriptContent = await File.ReadAllTextAsync(path);

            var scriptResult = await RunEphemeral(scriptContent, path);

            bool requiresSecret = scriptResult.RequiresSecret;
            bool requiresStandardLibrary = scriptResult.RequiresStd;

            string? sharedPassword = null;
            if (requiresSecret)
            {
                sharedPassword = AnsiConsole.Prompt(new TextPrompt<string>("Script requires secrets. Enter password to unlock store:").Secret());
            }

            var scriptConfig = new Dictionary<string, string>();
            var configPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "config.yaml");
            if (File.Exists(configPath))
            {
                try
                {
                    var yaml = await File.ReadAllTextAsync(configPath);
                    var loaded = _yamlDeserializer.Deserialize<Dictionary<string, string>>(yaml);
                    if (loaded != null) scriptConfig = loaded;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error loading config.yaml:[/] {ex.Message}");
                }
            }

            using var state = LuaState.Create();

            if (requiresStandardLibrary)
            {
                state.OpenStandardLibraries();
                scriptContent = scriptContent.Replace("require('std')", "");
            }

            var context = new LuaCommandContext(_configService.Config, _secretService, path, scriptConfig, sharedPassword);
            LuaCommandRegistry.Default.RegisterEnvironment(state, context);

            try
            { 
                await state.DoStringAsync(scriptContent); 
            }
            catch (Exception ex) { AnsiConsole.WriteException(ex); }
        }

        private async Task<ScriptAnalysis> RunEphemeral(string scriptContent, string scriptPath)
        {
            var analysis = new ScriptAnalysis();

            using var dryRunState = LuaState.Create();

            var context = new LuaCommandContext(
                _configService.Config,
                _secretService,
                scriptPath,
                new Dictionary<string, string>(),
                null,
                analysis);

            LuaCommandRegistry.Default.RegisterDryRunEnvironment(dryRunState, context, analysis);

            dryRunState.Environment["require"] = new LuaFunction((executionContext, _) =>
            {
                var moduleName = executionContext.ArgumentCount > 0 && executionContext.GetArgument(0).Type == LuaValueType.String
                    ? executionContext.GetArgument(0).Read<string>()
                    : string.Empty;

                if (moduleName == "std")
                {
                    analysis.RequiresStd = true;
                }

                return new ValueTask<int>(executionContext.Return(new LuaTable()));
            });

            try
            {
                var dryRunTask = dryRunState.DoStringAsync(scriptContent).AsTask();

                if (await Task.WhenAny(dryRunTask, Task.Delay(1000)) == dryRunTask)
                {
                    await dryRunTask;
                }
            }
            catch (Exception)
            {
            }

            return analysis;
        }

        public async Task CreateNewScript(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            
            var baseDir = GetUserScriptsDirectory();

            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            var folderPath = Path.Combine(baseDir, "." + name);
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, "init.lua");
            if (!File.Exists(filePath))
            {
                var docLines = new[]
                {
                    $"-- TTMG Script: {name.ToUpper()}",
                    "-- Canonical API: call commands on the ttmg table.",
                    "--   ttmg.prompt_input(title[, default]) -> string | Prompts the user for text input.",
                    "--   ttmg.prompt_select(title, options_table) -> string | Shows a selection menu.",
                    "--   ttmg.run_process(command, args, detached_bool) | Runs an external process.",
                    "--   ttmg.run_shell(command, detached_bool) | Runs a command in the default shell.",
                    "--   ttmg.get_secret(name) -> string? | Retrieves an encrypted secret from the store.",
                    "--   ttmg.get_config(key) -> string | Retrieves a value from config.yaml in the script folder.",
                    "--   ttmg.print(text) | Prints text to the console (supports markup).",
                    "--   require('std') | Includes the standard libraries",
                    "-- Legacy flat globals (prompt_input, prompt_select, run_process, run_shell,",
                    "-- get_secret, get_config, print) are still supported for older scripts.",
                    "",
                    $"ttmg.print('Hello from {name}!')"
                };
                var doc = string.Join(Environment.NewLine, docLines);
                await File.WriteAllTextAsync(filePath, doc);
            }
            OpenInEditor(filePath);
        }

        public void OpenInEditor(string filePath)
        {
            try
            {
                var config = _configService.Config;
                var editor = config.DefaultEditor;
                var args = config.EditorArgs.Replace("{file}", $"\"{filePath}\"");
                
                AnsiConsole.MarkupLine($"[grey]Opening in editor: {editor} {args}[/]");
                LuaEnv.ExecuteProcess(editor, args, false);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to open editor:[/] {ex.Message}");
            }
        }

        public ScriptMetadata? ResolveScript(string nameOrAlias)
        {
            if (string.IsNullOrWhiteSpace(nameOrAlias))
            {
                AnsiConsole.MarkupLine("[red]No script name provided.[/]");
                return null;
            }

            var needle = nameOrAlias.Trim();
            var matches = DiscoverScripts().Where(m =>
                    string.Equals(m.DisplayName, needle, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(m.Alias) && string.Equals(m.Alias, needle, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No script found matching[/] [yellow]'{needle}'[/].");
                AnsiConsole.MarkupLine("[grey]Scripts are matched by display name or alias. See the main menu for available scripts.[/]");
                return null;
            }

            if (matches.Count > 1)
            {
                AnsiConsole.MarkupLine($"[red]'{needle}' is ambiguous - it matches multiple scripts:[/]");
                foreach (var m in matches)
                {
                    var aliasPart = string.IsNullOrEmpty(m.Alias) ? "" : $" (alias: {m.Alias})";
                    AnsiConsole.MarkupLine($"  [cyan]{m.DisplayName}[/]{aliasPart} [grey]{m.FullPath}[/]");
                }
                AnsiConsole.MarkupLine("[grey]Use a more specific display name or alias.[/]");
                return null;
            }

            return matches[0];
        }

        public void DeleteScript(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    AnsiConsole.MarkupLine($"[red]Script file not found:[/] {fullPath}");
                    return;
                }

                File.Delete(fullPath);
                AnsiConsole.MarkupLine($"[green]Deleted script:[/] {fullPath}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to delete script:[/] {ex.Message}");
            }
        }

        private void EnsureHelloWorldExists(string targetDir)
        {
            var scriptsDir = Path.Combine(targetDir, "scripts");
            if (!Directory.Exists(scriptsDir))
            {
                try { Directory.CreateDirectory(scriptsDir); } catch { return; }
            }
            
            var helloPath = Path.Combine(scriptsDir, "hello.lua");
            if (!File.Exists(helloPath))
            {
                try { File.WriteAllText(helloPath, "print('Hello, world! This is TTMG.')\nprint('You can create new scripts with :create <name>')"); } catch { }
            }
        }
    }
}
