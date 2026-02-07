using System.Text.Json;
using System.Net.Http.Headers;
using Spectre.Console;

namespace TTMG
{
    public static class Updater
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static Updater()
        {
            // GitHub API requires a User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TTMG", "1.0"));
        }

        public static async Task CheckForUpdates(AppConfig config, bool manual = false)
        {
            if (string.IsNullOrEmpty(config.VersionUrl))
            {
                if (manual) AnsiConsole.MarkupLine("[red]Version URL not configured in yaml.[/]");
                return;
            }

            try
            {
                if (manual) AnsiConsole.MarkupLine("[yellow]Checking for updates...[/]");
                
                var request = new HttpRequestMessage(HttpMethod.Get, config.VersionUrl);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                
                var remoteInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (remoteInfo != null && IsNewer(remoteInfo.Version, config.CurrentVersion))
                {
                    AnsiConsole.MarkupLine($"[bold green]A new version is available: {remoteInfo.Version}[/]");
                    AnsiConsole.MarkupLine($"[grey]Notes: {remoteInfo.ReleaseNotes}[/]");
                    
                    if (AnsiConsole.Confirm("Would you like to download the update?"))
                    {
                        await PerformUpdate(config, remoteInfo);
                    }
                }
                else if (manual)
                {
                    AnsiConsole.MarkupLine("[green]You are on the latest version.[/]");
                }
            }
            catch (Exception ex)
            {
                if (manual) AnsiConsole.MarkupLine($"[red]Update check failed:[/] {ex.Message}");
            }
        }

        private static bool IsNewer(string remoteVersion, string currentVersion)
        {
            if (Version.TryParse(remoteVersion, out var v1) && Version.TryParse(currentVersion, out var v2))
            {
                return v1 > v2;
            }
            return remoteVersion != currentVersion;
        }

        private static async Task PerformUpdate(AppConfig config, VersionInfo info)
        {
            try
            {
                var updatePath = Path.Combine(Directory.GetCurrentDirectory(), config.UpdateDirectory);
                if (!Directory.Exists(updatePath)) Directory.CreateDirectory(updatePath);

                var fileName = Path.GetFileName(new Uri(info.DownloadUrl).LocalPath);
                var destination = Path.Combine(updatePath, fileName);

                await AnsiConsole.Status().StartAsync($"Downloading {fileName}...", async ctx =>
                {
                    var data = await _httpClient.GetByteArrayAsync(info.DownloadUrl);
                    await File.WriteAllBytesAsync(destination, data);
                });

                AnsiConsole.MarkupLine($"[green]Update downloaded to:[/] [blue]{destination}[/]");
                AnsiConsole.MarkupLine("[yellow]Please restart the application to apply the update.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Download failed:[/] {ex.Message}");
            }
        }

        public static async Task InstallScripts(AppConfig config, string repoName, string[] scriptNames)
        {
            var repo = config.Repositories.FirstOrDefault(r => r.ShortName.Equals(repoName, StringComparison.OrdinalIgnoreCase));
            if (repo == null)
            {
                AnsiConsole.MarkupLine($"[red]Repository '{repoName}' not found.[/]");
                return;
            }

            var scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
            if (!Directory.Exists(scriptsPath)) Directory.CreateDirectory(scriptsPath);

            foreach (var name in scriptNames)
            {
                var scriptFile = name.EndsWith(".lua") ? name : name + ".lua";
                var url = repo.Url.TrimEnd('/') + "/" + scriptFile;
                var destination = Path.Combine(scriptsPath, scriptFile);

                try
                {
                    AnsiConsole.MarkupLine($"[yellow]Installing {scriptFile}...[/]");
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrEmpty(repo.Token))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("token", repo.Token);
                    }

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(destination, data);
                    AnsiConsole.MarkupLine($"[green]Successfully installed {scriptFile}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to install {scriptFile}:[/] {ex.Message}");
                }
            }
        }
    }
}