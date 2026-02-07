using System.Text;
using Lua;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TTMG
{
    internal class Program
    {
        private enum Mode { Command, Menu }
        private static MiniValueState<Mode> _currentMode = new() { Value = Mode.Command };
        private static string _commandInput = "";
        private static string _filterInput = "";
        private static int _selectedIndex = 0;
        private static AppConfig _config = new();

        static async Task Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                PrintVersion();
                return;
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText("TTMG").LeftJustified().Color(Color.Cyan1));

                var currentDir = Directory.GetCurrentDirectory();
                var configPath = Path.Combine(currentDir, "TTMG", "scripts.yaml");
                if (!File.Exists(configPath)) configPath = Path.Combine(currentDir, "scripts.yaml");

                if (File.Exists(configPath))
                {
                    try { _config = deserializer.Deserialize<AppConfig>(File.ReadAllText(configPath)); }
                    catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Error loading config:[/] {ex.Message}"); }
                }

                if (!_config.SuppressUpdateChecks) { await Updater.CheckForUpdates(_config); }

                var discoveredScripts = ScriptDiscovery.Discover(currentDir, _config);
                var actionMap = new Dictionary<string, Func<Task>>();
                var allItems = new List<ScriptMetadata>();

                int idx = 1;
                foreach (var cmd in _config.Commands)
                {
                    var meta = new ScriptMetadata { DisplayName = cmd.Code, Index = idx++ };
                    allItems.Add(meta);
                    actionMap[meta.DisplayName] = () => { if (cmd.Action == "exit") Environment.Exit(0); return Task.CompletedTask; };
                }

                foreach (var script in discoveredScripts)
                {
                    script.Index = idx++;
                    allItems.Add(script);
                    actionMap[script.DisplayName] = () => RunScript(script.FullPath);
                    if (!string.IsNullOrEmpty(script.Alias)) actionMap[script.Alias] = actionMap[script.DisplayName];
                }

                _currentMode.Reset();
                string? result = await RunInteractiveLoop(allItems, actionMap);

                if (result == null) continue;
                if (result == ":qq" || result == ":wq") break;
                if (result.StartsWith(":update")) { await Updater.CheckForUpdates(_config, true); continue; }
                if (result.StartsWith(":version")) { PrintVersion(); AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]"); Console.ReadKey(true); continue; }
                if (result.StartsWith(":install")) 
                { 
                    var parts = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3) await Updater.InstallScripts(_config, parts[1], parts.Skip(2).ToArray());
                    else AnsiConsole.MarkupLine("[red]Usage: :install <repo> <script1> <script2>...[/]");
                    continue; 
                }

                AnsiConsole.Clear();
                if (result.StartsWith("\\"))
                {
                    var commandToRun = result.Substring(1);
                    var (shell, argsPrefix) = LuaEnv.GetShellInfo(_config.DefaultShell);
                    
                    AnsiConsole.MarkupLine($"[bold blue]Executing terminal command ([yellow]{shell}[/]):[/] [green]{commandToRun}[/]");
                    AnsiConsole.Write(new Rule());
                    
                    LuaEnv.ExecuteProcess(shell, $"{argsPrefix} \"{commandToRun.Replace("\"", "\\\"")}\"", false);
                }
                else if (actionMap.TryGetValue(result, out var action))
                {
                    await action();
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Unknown command or script:[/] {result}");
                }

                AnsiConsole.Write(new Rule());
                AnsiConsole.MarkupLine("[bold grey]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }

        private static void PrintVersion()
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            Console.WriteLine($"TTMG version {version}");
        }

        static Task<string?> RunInteractiveLoop(List<ScriptMetadata> allItems, Dictionary<string, Func<Task>> actionMap)
        {
            while (true)
            {
                List<ScriptMetadata> filtered;
                if (_currentMode == Mode.Command)
                {
                    filtered = allItems
                        .Where(m => string.IsNullOrEmpty(_commandInput) || 
                                    m.DisplayName.Contains(_commandInput, StringComparison.OrdinalIgnoreCase) ||
                                    m.Index.ToString() == _commandInput ||
                                    (m.Alias != null && m.Alias.Contains(_commandInput, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }
                else
                {
                    filtered = allItems
                        .Where(m => string.IsNullOrEmpty(_filterInput) || 
                                    m.DisplayName.Contains(_filterInput, StringComparison.OrdinalIgnoreCase) ||
                                    m.Index.ToString() == _filterInput)
                        .ToList();
                }

                if (_config.IMakeNoMistakes && filtered.Count == 1 && _currentMode == Mode.Command && !string.IsNullOrEmpty(_commandInput))
                {
                    var res = filtered[0].DisplayName;
                    _commandInput = "";
                    _filterInput = "";
                    return Task.FromResult<string?>(res);
                }

                if (_currentMode == Mode.Command)
                {
                    if (_currentMode.CheckDirtyAndClean())
                    {
                        AnsiConsole.MarkupLine("[grey]Type script name, [yellow]number[/], or [bold yellow]\\command[/]. [blue]Tab[/] for menu.[/]");
                        AnsiConsole.Markup("[bold cyan]goose>[/] " + _commandInput);
                    }
                    
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        var cmd = _commandInput;
                        _commandInput = "";
                        Console.WriteLine();
                        return Task.FromResult<string?>(cmd);
                    }
                    if (key.Key == ConsoleKey.Tab) { _currentMode.Value = Mode.Menu; _filterInput = ""; _selectedIndex = 0; return Task.FromResult<string?>(null); }
                    if (key.Key == ConsoleKey.Backspace && _commandInput.Length > 0) { _commandInput = _commandInput[..^1]; Console.Write("\b \b"); }
                    else if (!char.IsControl(key.KeyChar)) { _commandInput += key.KeyChar; Console.Write(key.KeyChar); }
                }
                else 
                {
                    if (_selectedIndex >= filtered.Count) _selectedIndex = Math.Max(0, filtered.Count - 1);
                    AnsiConsole.Clear();
                    AnsiConsole.Write(new FigletText("TTMG").LeftJustified().Color(Color.Cyan1));
                    AnsiConsole.MarkupLine("[grey]Arrows to select, type to filter. [blue]Tab[/] for command mode.[/]");
                    AnsiConsole.MarkupLine($"[bold cyan]Search:[/] {_filterInput}_");
                    AnsiConsole.Write(new Rule());

                    for (int i = 0; i < Math.Min(filtered.Count, 15); i++)
                    {
                        var m = filtered[i];
                        var text = $"{m.Index}. {m.DisplayName}";
                        if (i == _selectedIndex) AnsiConsole.MarkupLine($"[black on white] > {text} [/]");
                        else AnsiConsole.MarkupLine($"   {text}");
                    }
                    if (filtered.Count == 0) AnsiConsole.MarkupLine("[red]No matches found.[/]");

                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Tab) { _currentMode.Value = Mode.Command; return Task.FromResult<string?>(null); }
                    if (key.Key == ConsoleKey.Enter && filtered.Count > 0) return Task.FromResult<string?>(filtered[_selectedIndex].DisplayName);
                    if (key.Key == ConsoleKey.UpArrow) _selectedIndex = (_selectedIndex - 1 + filtered.Count) % Math.Max(1, filtered.Count);
                    if (key.Key == ConsoleKey.DownArrow) _selectedIndex = (_selectedIndex + 1) % Math.Max(1, filtered.Count);
                    if (key.Key == ConsoleKey.Backspace && _filterInput.Length > 0) { _filterInput = _filterInput[..^1]; _selectedIndex = 0; }
                    else if (!char.IsControl(key.KeyChar)) { _filterInput += key.KeyChar; _selectedIndex = 0; }
                }
            }
        }

        static async Task RunScript(string path)
        {
            if (!File.Exists(path)) { AnsiConsole.MarkupLine($"[red]File not found:[/] {path}"); return; }
            using var state = LuaState.Create();
            var env = new LuaEnv(_config);
            state.Environment["env"] = LuaValue.FromObject(env);
            string wrapper = @"
                prompt_input = function(t) return env:prompt_input(t) end
                prompt_select = function(t, o) return env:prompt_select(t, o) end
                run_process = function(c, a, d) env:run_process(c, a, d) end
                run_shell = function(c, d) env:run_shell(c, d) end
                print = function(t) env:print(t) end";
            await state.DoStringAsync(wrapper);
            try { await state.DoStringAsync(await File.ReadAllTextAsync(path)); }
            catch (Exception ex) { AnsiConsole.WriteException(ex); }
        }

        class MiniValueState<T>
        {
            public class ValueWrapper { public T? Value { get; set; } = default; public bool Unset { get; set; } = true; }
            private ValueWrapper _lastValue = new();
            public T Value { get; set; } = default!;
            public bool IsDirty => _lastValue.Unset || !EqualityComparer<T>.Default.Equals(_lastValue.Value, Value);
            public bool CheckDirtyAndClean() { if (IsDirty) { _lastValue.Value = Value; _lastValue.Unset = false; return true; } return false; }
            public void Reset() => _lastValue = new();
            public static implicit operator T(MiniValueState<T> state) => state.Value;
        }
    }
}
