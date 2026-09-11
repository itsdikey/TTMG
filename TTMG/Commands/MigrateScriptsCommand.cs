using System.Text;
using Spectre.Console;
using TTMG.Interfaces;
using TTMG.Scripting;

namespace TTMG.Commands
{
    [Command(":migrate-scripts", "migrate_scripts")]
    public class MigrateScriptsCommand : ICommand
    {
        private readonly IScriptService _scriptService;

        public MigrateScriptsCommand(IScriptService scriptService)
        {
            _scriptService = scriptService;
        }

        public Task Execute(string[] args)
        {
            if (args.Length > 0)
            {
                AnsiConsole.MarkupLine("[red]Usage: :migrate-scripts[/]");
                return Task.CompletedTask;
            }

            var scripts = _scriptService.DiscoverScripts();
            var pending = new List<PendingMigration>();
            var failures = new List<(string Path, string Error)>();

            foreach (var script in scripts)
            {
                try
                {
                    var (encoding, text) = ReadPreservingEncoding(script.FullPath);
                    var result = LuaScriptMigrator.Migrate(text);
                    if (result.Changed)
                    {
                        pending.Add(new PendingMigration(script, encoding, result));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add((script.FullPath, ex.Message));
                }
            }

            var readFailureCount = failures.Count;
            var unchanged = scripts.Count - pending.Count - readFailureCount;

            if (pending.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No legacy Lua calls found. Nothing to migrate.[/]");
                ReportFailures(failures);
                return Task.CompletedTask;
            }

            AnsiConsole.MarkupLine($"[bold cyan]{pending.Count} script(s) contain legacy Lua calls:[/]");
            foreach (var item in pending)
            {
                AnsiConsole.MarkupLine($"  [yellow]{item.Script.DisplayName}[/] [grey]({item.Script.FullPath})[/] - {item.Result.ReplacementCount} replacement(s)");
            }

            if (!Confirm($"Migrate {pending.Count} script(s) to the canonical ttmg.* API?"))
            {
                AnsiConsole.MarkupLine("[grey]Migration cancelled. No files were changed.[/]");
                ReportFailures(failures);
                return Task.CompletedTask;
            }

            var migrated = 0;
            foreach (var item in pending)
            {
                try
                {
                    File.WriteAllText(item.Script.FullPath, item.Result.Text, item.Encoding);
                    migrated++;
                }
                catch (Exception ex)
                {
                    failures.Add((item.Script.FullPath, ex.Message));
                }
            }

            AnsiConsole.MarkupLine($"[green]Migrated:[/] {migrated}");
            AnsiConsole.MarkupLine($"[grey]Unchanged:[/] {unchanged}");
            ReportFailures(failures);
            return Task.CompletedTask;
        }

        protected virtual bool Confirm(string message) => AnsiConsole.Confirm(message, false);

        private static void ReportFailures(List<(string Path, string Error)> failures)
        {
            if (failures.Count == 0)
            {
                return;
            }

            AnsiConsole.MarkupLine($"[red]Failed:[/] {failures.Count}");
            foreach (var failure in failures)
            {
                AnsiConsole.MarkupLine($"  [red]{failure.Path}[/] - {failure.Error}");
            }
        }

        private static (Encoding Encoding, string Text) ReadPreservingEncoding(string path)
        {
            var bytes = File.ReadAllBytes(path);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return (new UTF8Encoding(true), Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
            }

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                return (new UTF32Encoding(false, true), Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4));
            }

            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                return (new UTF32Encoding(true, true), Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4));
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return (new UnicodeEncoding(false, true), Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return (new UnicodeEncoding(true, true), Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
            }

            return (new UTF8Encoding(false), Encoding.UTF8.GetString(bytes));
        }

        private sealed class PendingMigration
        {
            public ScriptMetadata Script { get; }
            public Encoding Encoding { get; }
            public LuaMigrationResult Result { get; }

            public PendingMigration(ScriptMetadata script, Encoding encoding, LuaMigrationResult result)
            {
                Script = script;
                Encoding = encoding;
                Result = result;
            }
        }
    }
}
