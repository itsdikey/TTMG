using NeoSmart.SecureStore;
using Spectre.Console;
using System.Text;
using TTMG.Interfaces;

namespace TTMG.Services
{
    public class SecretService : ISecretService
    {
        private const string StoreFileName = "secrets.bin";
        private const string SecretsNamesFileName = "secret_names.dat";
        private readonly IConfigService _configService;

        public SecretService(IConfigService configService)
        {
            _configService = configService;
        }

        private string GetStorePath() => Path.Combine(_configService.DataDirectory, StoreFileName);
        private string GetNamesPath() => Path.Combine(_configService.DataDirectory, SecretsNamesFileName);

        public void CreateSecret(string name)
        {
            var value = AnsiConsole.Prompt(new TextPrompt<string>($"Enter value for secret [yellow]{name}[/]:").Secret());
            var password = AnsiConsole.Prompt(new TextPrompt<string>("Enter password to protect the store:").Secret());

            var path = GetStorePath();
            bool exists = File.Exists(path);

            using (var sman = exists ? SecretsManager.LoadStore(path) : SecretsManager.CreateStore())
            {
                sman.LoadKeyFromPassword(password);

                sman.Set(name, value);
                sman.SaveStore(path);
            }

            var names = LoadSecretNames();
            if (!names.Contains(name))
            {
                names.Add(name);
                SaveSecretNames(names);
            }

            AnsiConsole.MarkupLine($"[green]Secret '{name}' saved successfully.[/]");
        }

        public List<string> ListSecrets()
        {
            return LoadSecretNames();
        }

        public string? GetSecret(string name, string? password = null)
        {
            var names = LoadSecretNames();

            if (!names.Contains(name))
            {
                return null;
            }

            password ??= AnsiConsole.Prompt(new TextPrompt<string>($"Enter password to retrieve secret [yellow]{name}[/]:").Secret());
            var path = GetStorePath();

            var exists = File.Exists(path);

            if (!exists)
            {
                AnsiConsole.MarkupLine("[red]Secrets store file not found.[/]");
                return null;
            }

            try
            {
                using (var sman = exists ? SecretsManager.LoadStore(path) : SecretsManager.CreateStore())
                {
                    sman.LoadKeyFromPassword(password);
                    return sman.Get(name);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to retrieve secret: {ex.Message}[/]");
                return null;
            }
        }

        private List<string> LoadSecretNames()
        {
            var path = GetNamesPath();
            if (!File.Exists(path))
                return new List<string>();
            try
            {
                var base64 = File.ReadAllText(path);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        private void SaveSecretNames(List<string> names)
        {
            var path = GetNamesPath();
            var json = System.Text.Json.JsonSerializer.Serialize(names);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            File.WriteAllText(path, base64);
        }
    }
}
