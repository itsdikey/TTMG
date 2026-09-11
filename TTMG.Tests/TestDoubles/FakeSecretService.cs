using TTMG.Interfaces;

namespace TTMG.Tests.TestDoubles;

public sealed class FakeSecretService : ISecretService
{
    public List<string> Names { get; } = new();
    public List<string> Created { get; } = new();
    public List<(string Name, string? Password)> Requested { get; } = new();
    public int ListCalls { get; private set; }
    public string? Value { get; set; } = "secret-value";

    public void CreateSecret(string name) => Created.Add(name);

    public List<string> ListSecrets()
    {
        ListCalls++;
        return Names;
    }

    public string? GetSecret(string name, string? password = null)
    {
        Requested.Add((name, password));
        return Value;
    }
}
