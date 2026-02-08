using Lua;
using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Services
{
    public class ScriptService : IScriptService
    {
        private readonly IConfigService _configService;
        private readonly ISecretService _secretService;

        public ScriptService(IConfigService configService, ISecretService secretService)
        {
            _configService = configService;
            _secretService = secretService;
        }

        public List<ScriptMetadata> DiscoverScripts()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var config = _configService.Config;
            
            EnsureHelloWorldExists(baseDir);

            var discoveredScripts = Discover(baseDir, config);
            if (!string.IsNullOrEmpty(config.UserScriptsDirectory))
            {
                var userScripts = Discover(Environment.ExpandEnvironmentVariables(config.UserScriptsDirectory), config);
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

                scripts.Add(new ScriptMetadata { DisplayName = displayName, FullPath = file });
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
                            item.DisplayName = $"{parentDir}-{item.DisplayName}";
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
            if (!File.Exists(path)) { AnsiConsole.MarkupLine($"[red]File not found:[/] {path}"); return; }
            using var state = LuaState.Create();
            var env = new LuaEnv(_configService.Config, _secretService);
            state.Environment["env"] = LuaValue.FromObject(env);
            string wrapper = @"
                prompt_input = function(t) return env:prompt_input(t) end
                prompt_select = function(t, o) return env:prompt_select(t, o) end
                run_process = function(c, a, d) env:run_process(c, a, d) end
                run_shell = function(c, d) env:run_shell(c, d) end
                get_secret = function(n) return env:get_secret(n) end
                print = function(t) env:print(t) end";
            await state.DoStringAsync(wrapper);
            try { await state.DoStringAsync(await File.ReadAllTextAsync(path)); }
            catch (Exception ex) { AnsiConsole.WriteException(ex); }
        }

        public async Task CreateNewScript(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            
            var config = _configService.Config;
            var baseDir = !string.IsNullOrEmpty(config.UserScriptsDirectory) 
                ? Environment.ExpandEnvironmentVariables(config.UserScriptsDirectory) 
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TTMG-Scripts");

            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            var folderPath = Path.Combine(baseDir, "." + name);
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, "init.lua");
            if (!File.Exists(filePath))
            {
                await File.WriteAllTextAsync(filePath, $"-- New script: {name}\nprint('Hello from {name}!')");
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

        private void EnsureHelloWorldExists(string baseDir)
        {
            var scriptsDir = Path.Combine(baseDir, "scripts");
            if (!Directory.Exists(scriptsDir)) Directory.CreateDirectory(scriptsDir);
            
            var helloPath = Path.Combine(scriptsDir, "hello.lua");
            if (!File.Exists(helloPath))
            {
                File.WriteAllText(helloPath, "print('Hello, world! This is TTMG.')\nprint('You can create new scripts with :create <name>')");
            }
        }
    }
}
