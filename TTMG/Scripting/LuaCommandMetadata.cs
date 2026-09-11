namespace TTMG.Scripting
{
    public sealed record LuaCommandMetadata(
        string Name,
        string Signature,
        string Help,
        IReadOnlyList<string> Parameters,
        string? Example,
        bool AnalyzesSecret);
}
