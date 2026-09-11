using System.Diagnostics;

namespace TTMG
{
    public static class LuaEnv
    {
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
    }
}
