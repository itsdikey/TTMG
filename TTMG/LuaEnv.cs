using System.Diagnostics;
using Lua;
using Spectre.Console;

namespace TTMG
{
    [LuaObject]
    public partial class LuaEnv
    {
        private readonly AppConfig _config;

        public LuaEnv(AppConfig config)
        {
            _config = config;
        }

        [LuaMember]
        public string prompt_input(string title) => AnsiConsole.Ask<string>(title);

        [LuaMember]
        public string prompt_select(string title, LuaTable optionsTable)
        {
            var options = optionsTable.Select(pair => pair.Value.ToString() ?? "").ToList();
            return AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).PageSize(10).AddChoices(options));
        }

        [LuaMember]
        public void run_process(string command, string args, bool detached) => ExecuteProcess(command, args, detached);

        [LuaMember]
        public void run_shell(string command, bool detached)
        {
            var (shell, argsPrefix) = GetShellInfo(_config.DefaultShell);
            ExecuteProcess(shell, $"{argsPrefix} \"{command.Replace("\"", "\\\"")}\"", detached);
        }

        public static (string shell, string argsPrefix) GetShellInfo(string configShell)
        {
            return configShell.ToLower() switch
            {
                "powershell" => ("powershell.exe", "-Command"),
                "pwsh" => ("pwsh", "-Command"),
                "bash" => ("bash", "-c"),
                "zsh" => ("zsh", "-c"),
                "sh" => ("sh", "-c"),
                _ => ("cmd.exe", "/c")
            };
        }

        public static void ExecuteProcess(string command, string args, bool detached)
        {
            var psi = new ProcessStartInfo { FileName = command, Arguments = args, UseShellExecute = detached, CreateNoWindow = false };
            if (detached) Process.Start(psi);
            else { using var process = Process.Start(psi); process?.WaitForExit(); }
        }
        
        [LuaMember]
        public void print(string text) => AnsiConsole.MarkupLine(text);
    }
}
