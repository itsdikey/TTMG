namespace TTMG.Scripting
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class LuaCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Help { get; }
        public string Signature { get; }
        public string[] Parameters { get; set; } = Array.Empty<string>();
        public string? Example { get; set; }
        public bool AnalyzesSecret { get; set; }

        public LuaCommandAttribute(string name, string help, string signature)
        {
            Name = name;
            Help = help;
            Signature = signature;
        }
    }
}
