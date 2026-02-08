using Spectre.Console;
using TTMG.Interfaces;

namespace TTMG.Services
{

    public class AppService : IAppService
    {
        private readonly IConfigService _configService;
        private readonly IScriptService _scriptService;
        private readonly IUpdaterService _updaterService;
        private readonly ISecretService _secretService;

        private enum Mode { Command, Menu }
        private class MiniValueState<T>
        {
            public class ValueWrapper { public T? Value { get; set; } = default; public bool Unset { get; set; } = true; }
            private ValueWrapper _lastValue = new();
            public T Value { get; set; } = default!;
            public bool IsDirty => _lastValue.Unset || !EqualityComparer<T>.Default.Equals(_lastValue.Value, Value);
            public bool CheckDirtyAndClean() { if (IsDirty) { _lastValue.Value = Value; _lastValue.Unset = false; return true; } return false; }
            public void Reset() => _lastValue = new();
        }

        private readonly MiniValueState<Mode> _currentMode = new() { Value = Mode.Command };
        private string _commandInput = "";
        private string _filterInput = "";
        private int _selectedIndex = 0;

        public AppService(IConfigService configService, IScriptService scriptService, IUpdaterService updaterService, ISecretService secretService)
        {
            _configService = configService;
            _scriptService = scriptService;
            _updaterService = updaterService;
            _secretService = secretService;
        }

        public async Task Run(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                PrintVersion();
                return;
            }

            _configService.LoadConfig();

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText("TTMG").LeftJustified().Color(Color.Cyan1));

                AppConfig config = _configService.Config;

                if (!config.SuppressUpdateChecks)
                { await _updaterService.CheckForUpdates(); }

                List<ScriptMetadata> discoveredScripts = _scriptService.DiscoverScripts();

                Dictionary<string, Func<Task>> actionMap = new();
                List<ScriptMetadata> allItems = new();

                int idx = 1;
                foreach (CommandEntry cmd in config.Commands)
                {
                    ScriptMetadata meta = new() { DisplayName = cmd.Code, Index = idx++, IsCommand = true };
                    allItems.Add(meta);
                    actionMap[meta.DisplayName] = () => { if (cmd.Action == "exit") { Environment.Exit(0); } return Task.CompletedTask; };
                }

                idx=1;

                foreach (ScriptMetadata script in discoveredScripts)
                {
                    script.Index = idx++;
                    allItems.Add(script);
                    actionMap[script.DisplayName] = () => _scriptService.RunScript(script.FullPath);
                    if (!string.IsNullOrEmpty(script.Alias))
                    {
                        actionMap[script.Alias] = actionMap[script.DisplayName];
                    }
                }

                _currentMode.Reset();
                string? result = await RunInteractiveLoop(allItems, actionMap);

                if (result == null)
                {
                    continue;
                }

                if (result == ":qq" || result == ":wq")
                {
                    break;
                }

                if (result.StartsWith(":update"))
                { await _updaterService.CheckForUpdates(true); continue; }
                if (result.StartsWith(":version"))
                { PrintVersion(); AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]"); Console.ReadKey(true); continue; }
                if (result.StartsWith(":create"))
                {
                    string[] parts = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string? name = parts.Length > 1 ? parts[1] : null;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = AnsiConsole.Ask<string>("Enter a name for your new script:");
                    }
                    await _scriptService.CreateNewScript(name);
                    continue;
                }
                if (result.StartsWith(":install"))
                {
                    string[] parts = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        await _updaterService.InstallScripts(parts[1], parts.Skip(2).ToArray());
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Usage: :install <repo> <script1> <script2>...[/]");
                    }

                    continue;
                }

                if (result.StartsWith(":secret"))
                {
                    string[] parts = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var subCommand = parts[1].ToLower();
                        if (subCommand == "create" && parts.Length >= 3)
                        {
                            _secretService.CreateSecret(parts[2]);
                        }
                        else if (subCommand == "list")
                        {
                            var secrets = _secretService.ListSecrets();
                            if (secrets.Any())
                            {
                                AnsiConsole.MarkupLine("[bold cyan]Available secrets:[/]");
                                foreach (var s in secrets) AnsiConsole.MarkupLine($"- {s}");
                            }
                            else AnsiConsole.MarkupLine("[grey]No secrets found.[/]");
                        }
                        else if (subCommand == "get" && parts.Length >= 3)
                        {
                            var val = _secretService.GetSecret(parts[2]);
                            if (val != null) AnsiConsole.MarkupLine($"Secret [yellow]{parts[2]}[/]: [green]{val}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]Usage: :secret <create|list|get> [name][/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Usage: :secret <create|list|get> [[name]][/]");
                    }
                    AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                    Console.ReadKey(true);
                    continue;
                }

                AnsiConsole.Clear();
                if (result.StartsWith("\\"))
                {
                    string commandToRun = result.Substring(1);
                    (string shell, string argsPrefix) = LuaEnv.GetShellInfo(config.DefaultShell);

                    AnsiConsole.MarkupLine($"[bold blue]Executing terminal command ([yellow]{shell}[/]):[/] [green]{commandToRun}[/]");
                    AnsiConsole.Write(new Rule());

                    LuaEnv.ExecuteProcess(shell, $"{argsPrefix} \"{commandToRun.Replace("\"", "\\\"")}\"", false);
                }
                else if (actionMap.TryGetValue(result, out Func<Task>? action))
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

        private Task<string?> RunInteractiveLoop(List<ScriptMetadata> allItems, Dictionary<string, Func<Task>> actionMap)
        {
            AppConfig config = _configService.Config;
            while (true)
            {
                List<ScriptMetadata> filtered;
                if (_currentMode.Value == Mode.Command)
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
                        .Where(m => !m.IsCommand)
                        .Where(m => string.IsNullOrEmpty(_filterInput) ||
                                    m.DisplayName.Contains(_filterInput, StringComparison.OrdinalIgnoreCase) ||
                                    m.Index.ToString() == _filterInput)
                        .ToList();
                }

                if (config.IMakeNoMistakes && filtered.Count == 1 && _currentMode.Value == Mode.Command && !string.IsNullOrEmpty(_commandInput))
                {
                    string res = filtered[0].DisplayName;
                    _commandInput = "";
                    _filterInput = "";
                    return Task.FromResult<string?>(res);
                }

                // local scoring helper prefers exact > numeric index match > startswith > contains
                static int ScoreMatch(string input, ScriptMetadata m)
                {
                    if (string.IsNullOrEmpty(input)) return -1;
                    int score = 0;
                    if (string.Equals(m.DisplayName, input, StringComparison.OrdinalIgnoreCase)) score += 1000;
                    if (m.Index.ToString() == input) score += 500;
                    if (m.DisplayName.StartsWith(input, StringComparison.OrdinalIgnoreCase)) score += 100;
                    if (m.DisplayName.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
                    return score;
                }

                if (_currentMode.Value == Mode.Command)
                {
                    AnsiConsole.Clear();

                    AnsiConsole.Write(new FigletText("TTMG").LeftJustified().Color(Color.Cyan1));

                    AnsiConsole.MarkupLine("[grey]Type script name, [yellow]number[/], or [bold yellow]\\command[/]. [blue]Tab[/] for menu.[/]");

                    var bestMatch = filtered
                        .Select(m => new { Meta = m, Score = ScoreMatch(_commandInput, m) })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.Meta.Index)
                        .Select(x => x.Meta)
                        .FirstOrDefault();

                    var output = $"[bold cyan]goose>[/] {_commandInput}";

                    if (bestMatch != null && _commandInput.Length > 0)
                    {
                        output += $"[grey]{bestMatch.DisplayName.Substring(_commandInput.Length)}[/]";
                    }

                    AnsiConsole.Markup(output);

                    if(bestMatch != null)
                    {
                        AnsiConsole.Cursor.MoveLeft(bestMatch.DisplayName.Length-_commandInput.Length);
                    }


                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        string cmd = _commandInput;
                        _commandInput = "";
                        Console.WriteLine();
                        return Task.FromResult<string?>(cmd);
                    }
                    if (key.Key == ConsoleKey.Tab)
                    {
                        _currentMode.Value = Mode.Menu;
                        _filterInput = "";
                        _selectedIndex = 0;
                        return Task.FromResult<string?>(null);
                    }
                    if (key.Key == ConsoleKey.RightArrow
                        && bestMatch!=null
                        && _commandInput.Length>0)
                    {
                        _commandInput = bestMatch.DisplayName;
                    }
                    if (key.Key == ConsoleKey.Backspace && _commandInput.Length > 0)
                    {
                        _commandInput = _commandInput[..^1];
                        Console.Write("\b \b");
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        _commandInput += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                }
                else if (_currentMode.Value == Mode.Menu)
                {
                    if (_selectedIndex >= filtered.Count)
                    {
                        _selectedIndex = Math.Max(0, filtered.Count - 1);
                    }

                    AnsiConsole.Clear();
                    AnsiConsole.Write(new FigletText("TTMG").LeftJustified().Color(Color.Cyan1));
                    AnsiConsole.MarkupLine("[grey]Arrows to select, type to filter. [blue]Tab[/] for command mode.[/]");

                    // scored filtering here: exact / numeric / startswith / contains
                    var bestMatch = filtered
                        .Select(m => new { Meta = m, Score = ScoreMatch(_filterInput, m) })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.Meta.Index)
                        .Select(x => x.Meta)
                        .FirstOrDefault();

                    var output = $"[bold cyan]Run:[/] {_filterInput}";

                    if (bestMatch != null
                        && _filterInput.Length > 0)
                    {
                        output += $"[grey]{bestMatch.DisplayName.Substring(_filterInput.Length)}[/]";
                    }

                    AnsiConsole.MarkupLine(output);
                    var line = Console.CursorTop;

                    AnsiConsole.Write(new Rule());

                    var len = filtered.Count > 15 ? 15 : filtered.Count;

                    for (int i = 0; i < len; i++)
                    {
                        ScriptMetadata m = filtered[i];
                        string text = $"{m.Index}. {m.DisplayName}";
                        if (i == _selectedIndex)
                        {
                            AnsiConsole.MarkupLine($"[black on white] > {text} [/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"   {text}");
                        }
                    }
                    if (filtered.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[red]No matches found.[/]");
                    }

                    AnsiConsole.Cursor.SetPosition(6+_filterInput.Length, line);

                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Tab)
                    {
                        _currentMode.Value = Mode.Command;
                        return Task.FromResult<string?>(null);
                    }
                    if (key.Key == ConsoleKey.Enter && filtered.Count > 0)
                    {
                        _filterInput = "";
                        return Task.FromResult<string?>(filtered[_selectedIndex].DisplayName);
                    }

                    if (key.Key == ConsoleKey.UpArrow)
                    {
                        _selectedIndex = (_selectedIndex - 1 + filtered.Count) % Math.Max(1, filtered.Count);
                    }

                    if (key.Key == ConsoleKey.DownArrow)
                    {
                        _selectedIndex = (_selectedIndex + 1) % Math.Max(1, filtered.Count);
                    }

                    if(key.Key == ConsoleKey.RightArrow
                        && bestMatch!=null
                        && _filterInput.Length>0)
                    {
                        _filterInput = bestMatch.DisplayName;
                    }

                    if (key.Key == ConsoleKey.Backspace && _filterInput.Length > 0)
                    {
                        _filterInput = _filterInput[..^1];
                        _selectedIndex = 0;
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        _filterInput += key.KeyChar; _selectedIndex = 0;
                    }
                }
            }
        }

        private void PrintVersion()
        {
            string version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            Console.WriteLine($"TTMG version {version}");
        }
    }
}