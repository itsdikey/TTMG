using System.Text;
using System.Text.Json;
using TTMG.Services;
using TTMG.Tests.TestDoubles;
using Xunit;

namespace TTMG.Tests.Services;

public class SecretServiceTests
{
    [Fact]
    public void ListSecrets_WhenNoNamesFile_ReturnsEmpty()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService { DataDirectory = temp.Path };
        var service = new SecretService(config);

        Assert.Empty(service.ListSecrets());
    }

    [Fact]
    public void ListSecrets_WhenNamesFileExists_ReturnsNames()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService { DataDirectory = temp.Path };
        var json = JsonSerializer.Serialize(new[] { "alpha", "beta" });
        File.WriteAllText(Path.Combine(temp.Path, "secret_names.dat"), Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        var service = new SecretService(config);

        Assert.Equal(new[] { "alpha", "beta" }, service.ListSecrets());
    }

    [Fact]
    public void GetSecret_WhenNameNotListed_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService { DataDirectory = temp.Path };
        var service = new SecretService(config);

        Assert.Null(service.GetSecret("missing"));
    }

    [Fact]
    public void GetSecret_WhenStoreFileMissing_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var config = new FakeConfigService { DataDirectory = temp.Path };
        var json = JsonSerializer.Serialize(new[] { "alpha" });
        File.WriteAllText(Path.Combine(temp.Path, "secret_names.dat"), Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        var service = new SecretService(config);

        Assert.Null(service.GetSecret("alpha", "password"));
    }
}
