namespace TTMG.Interfaces
{
    public interface ISecretService
    {
        void CreateSecret(string name);
        List<string> ListSecrets();
        string? GetSecret(string name, string? password = null);
    }
}