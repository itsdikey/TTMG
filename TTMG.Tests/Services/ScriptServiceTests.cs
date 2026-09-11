using TTMG;
using TTMG.Services;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Services;

public class ScriptServiceTests
{
    private static ScriptService CreateService(TempDirectory temp, FakeConfigService config)
    {
        var userDir = temp.Combine("user");
        var dataDir = temp.Combine("data");
        Directory.CreateDirectory(userDir);
        Directory.CreateDirectory(dataDir);

        config.DataDirectory = dataDir;
        config.Config = new AppConfig
        {
            UserScriptsDirectory = userDir,
            DefaultEditor = "ttmg-nonexistent-editor-xyz",
            EditorArgs = "{file}"
        };

        return new ScriptService(config, new FakeSecretService());
    }

    [Fact]
    public void DiscoverScripts_FindsUserScriptsAndCreatesHelloWorld()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var userDir = config.Config.UserScriptsDirectory;

        Directory.CreateDirectory(Path.Combine(userDir, "nested"));
        File.WriteAllText(Path.Combine(userDir, "alpha.lua"), "print('a')");
        File.WriteAllText(Path.Combine(userDir, "nested", "init.lua"), "print('n')");

        var scripts = service.DiscoverScripts().Where(s => s.FullPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Contains(scripts, s => s.DisplayName == "alpha");
        Assert.Contains(scripts, s => s.DisplayName == "nested");
        Assert.True(File.Exists(Path.Combine(config.DataDirectory, "scripts", "hello.lua")));
    }

    [Fact]
    public void DiscoverScripts_DisambiguatesDuplicateNamesWithParentFolders()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var userDir = config.Config.UserScriptsDirectory;

        Directory.CreateDirectory(Path.Combine(userDir, "a"));
        Directory.CreateDirectory(Path.Combine(userDir, "b"));
        File.WriteAllText(Path.Combine(userDir, "a", "shared.lua"), "print('a')");
        File.WriteAllText(Path.Combine(userDir, "b", "shared.lua"), "print('b')");

        var scripts = service.DiscoverScripts()
            .Where(s => s.FullPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
            .Where(s => s.DisplayName.EndsWith("shared", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, scripts.Count);
        Assert.Equal(2, scripts.Select(s => s.DisplayName).Distinct().Count());
    }

    [Fact]
    public void DiscoverScripts_MapsAliasesFromConfig()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var userDir = config.Config.UserScriptsDirectory;
        File.WriteAllText(Path.Combine(userDir, "alpha.lua"), "print('a')");
        config.Config.Scripts.Add(new ScriptEntry { Name = "alpha", Path = "alpha.lua", Alias = ":a" });

        var script = service.DiscoverScripts().Single(s => s.FullPath == Path.Combine(userDir, "alpha.lua"));

        Assert.Equal(":a", script.Alias);
    }

    [Fact]
    public void ResolveScript_ResolvesByNameAndAliasCaseInsensitively()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var userDir = config.Config.UserScriptsDirectory;
        var name = "unique-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(userDir, name + ".lua");
        File.WriteAllText(path, "print('x')");
        config.Config.Scripts.Add(new ScriptEntry { Name = name, Path = name + ".lua", Alias = ":myalias" });

        var byName = service.ResolveScript(name.ToUpperInvariant());
        var byAlias = service.ResolveScript(":MYALIAS");

        Assert.NotNull(byName);
        Assert.Equal(path, byName!.FullPath);
        Assert.NotNull(byAlias);
        Assert.Equal(path, byAlias!.FullPath);
    }

    [Fact]
    public void ResolveScript_WhenAmbiguous_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var userDir = config.Config.UserScriptsDirectory;
        File.WriteAllText(Path.Combine(userDir, "alpha.lua"), "print('a')");
        File.WriteAllText(Path.Combine(userDir, "beta.lua"), "print('b')");
        config.Config.Scripts.Add(new ScriptEntry { Name = "alpha", Path = "alpha.lua", Alias = "dup" });
        config.Config.Scripts.Add(new ScriptEntry { Name = "beta", Path = "beta.lua", Alias = "dup" });

        var resolved = service.ResolveScript("dup");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveScript_WhenMissing_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);

        Assert.Null(service.ResolveScript("definitely-not-a-script-" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task CreateNewScript_CreatesInitLuaUnderUserDirectory()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);

        await service.CreateNewScript("myscript");

        var initPath = Path.Combine(config.Config.UserScriptsDirectory, ".myscript", "init.lua");
        Assert.True(File.Exists(initPath));
        var content = await File.ReadAllTextAsync(initPath);
        Assert.Contains("myscript", content);
        Assert.Contains("ttmg.print", content);
    }

    [Fact]
    public void DeleteScript_DeletesExistingFileAndIgnoresMissing()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var path = temp.Combine("delete-me.lua");
        File.WriteAllText(path, "print('x')");

        service.DeleteScript(path);
        Assert.False(File.Exists(path));

        service.DeleteScript(path);
    }

    [Fact]
    public void OpenInEditor_WithMissingEditor_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);

        service.OpenInEditor(temp.Combine("some.lua"));
    }

    [Fact]
    public async Task RunScript_WhenMissingFile_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);

        await service.RunScript(temp.Combine("missing.lua"));
    }

    [Fact]
    public async Task RunScript_WithStandardLibraryRequest_ExecutesStandardLibraryCode()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService();
        var service = CreateService(temp, config);
        var marker = temp.Combine("marker.txt").Replace('\\', '/');
        var scriptPath = temp.Combine("std.lua");
        await File.WriteAllTextAsync(scriptPath,
            "require('std')\n" +
            $"local f = io.open('{marker}', 'w')\n" +
            "f:write('ok')\n" +
            "f:close()\n");

        await service.RunScript(scriptPath);

        Assert.True(File.Exists(marker), "standard library was not available; require('std') handling failed");
        Assert.Equal("ok", await File.ReadAllTextAsync(marker));
    }
}
