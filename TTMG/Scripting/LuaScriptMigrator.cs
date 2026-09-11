using System.Text;

namespace TTMG.Scripting
{
    public sealed class LuaMigrationResult
    {
        public string Text { get; }
        public int ReplacementCount { get; }

        public LuaMigrationResult(string text, int replacementCount)
        {
            Text = text;
            ReplacementCount = replacementCount;
        }

        public bool Changed => ReplacementCount > 0;
    }

    public static class LuaScriptMigrator
    {
        private static readonly HashSet<string> LegacyNames = new(StringComparer.Ordinal)
        {
            "print", "prompt_input", "prompt_select", "run_process", "run_shell", "get_secret", "get_config"
        };

        public static LuaMigrationResult Migrate(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return new LuaMigrationResult(source ?? string.Empty, 0);
            }

            var tokens = LuaLexer.Tokenize(source);
            var significant = new List<int>();
            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Kind != LuaTokenKind.Whitespace && tokens[i].Kind != LuaTokenKind.Comment)
                {
                    significant.Add(i);
                }
            }

            var position = new Dictionary<int, int>(significant.Count);
            for (var k = 0; k < significant.Count; k++)
            {
                position[significant[k]] = k;
            }

            var output = new StringBuilder(source.Length + 32);
            var scopes = new List<HashSet<string>> { new(StringComparer.Ordinal) };
            var replacements = 0;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var rewritten = false;

                if (token.Kind == LuaTokenKind.Identifier &&
                    LegacyNames.Contains(token.Text) &&
                    position.TryGetValue(i, out var k))
                {
                    var previousIsMemberOrDeclaration = k > 0 && IsMemberOrDeclaration(tokens[significant[k - 1]]);
                    var isCall = k + 1 < significant.Count && tokens[significant[k + 1]].Text == "(";
                    var isShadowed = IsShadowed(scopes, token.Text);

                    if (!previousIsMemberOrDeclaration && isCall && !isShadowed)
                    {
                        output.Append("ttmg.").Append(token.Text);
                        replacements++;
                        rewritten = true;
                    }
                }

                if (!rewritten)
                {
                    output.Append(token.Text);
                }

                HandleScope(token, i, tokens, significant, position, scopes);
            }

            return new LuaMigrationResult(output.ToString(), replacements);
        }

        private static bool IsMemberOrDeclaration(LuaToken previous) =>
            previous.Text == "." || previous.Text == ":" || previous.Text == "function";

        private static bool IsShadowed(List<HashSet<string>> scopes, string name)
        {
            for (var i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].Contains(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static void HandleScope(
            LuaToken token,
            int index,
            List<LuaToken> tokens,
            List<int> significant,
            Dictionary<int, int> position,
            List<HashSet<string>> scopes)
        {
            if (token.Kind != LuaTokenKind.Keyword && token.Kind != LuaTokenKind.Symbol)
            {
                return;
            }

            switch (token.Text)
            {
                case "function":
                    AddFunctionName(tokens, significant, position, index, scopes);
                    PushScope(scopes);
                    break;
                case "do":
                case "then":
                case "repeat":
                    PushScope(scopes);
                    break;
                case "else":
                    PopScope(scopes);
                    PushScope(scopes);
                    break;
                case "elseif":
                case "end":
                case "until":
                    PopScope(scopes);
                    break;
                case "local":
                    AddLocalNames(tokens, significant, position, index, scopes);
                    break;
            }
        }

        private static void PushScope(List<HashSet<string>> scopes) =>
            scopes.Add(new HashSet<string>(StringComparer.Ordinal));

        private static void PopScope(List<HashSet<string>> scopes)
        {
            if (scopes.Count > 1)
            {
                scopes.RemoveAt(scopes.Count - 1);
            }
        }

        private static void AddFunctionName(
            List<LuaToken> tokens,
            List<int> significant,
            Dictionary<int, int> position,
            int index,
            List<HashSet<string>> scopes)
        {
            if (!position.TryGetValue(index, out var k) || k + 2 >= significant.Count)
            {
                return;
            }

            var name = tokens[significant[k + 1]];
            var after = tokens[significant[k + 2]];
            if (name.Kind == LuaTokenKind.Identifier && after.Text == "(")
            {
                scopes[^1].Add(name.Text);
            }
        }

        private static void AddLocalNames(
            List<LuaToken> tokens,
            List<int> significant,
            Dictionary<int, int> position,
            int index,
            List<HashSet<string>> scopes)
        {
            if (!position.TryGetValue(index, out var k))
            {
                return;
            }

            var j = k + 1;
            if (j < significant.Count && tokens[significant[j]].Text == "function")
            {
                j++;
                if (j < significant.Count && tokens[significant[j]].Kind == LuaTokenKind.Identifier)
                {
                    scopes[^1].Add(tokens[significant[j]].Text);
                }

                return;
            }

            while (j < significant.Count)
            {
                var name = tokens[significant[j]];
                if (name.Kind != LuaTokenKind.Identifier)
                {
                    break;
                }

                scopes[^1].Add(name.Text);
                j++;

                if (j < significant.Count && tokens[significant[j]].Text == "<")
                {
                    var depth = 0;
                    do
                    {
                        var attributeToken = tokens[significant[j]];
                        if (attributeToken.Text == "<") depth++;
                        else if (attributeToken.Text == ">") depth--;
                        j++;
                    }
                    while (j < significant.Count && depth > 0);
                }

                if (j < significant.Count && tokens[significant[j]].Text == ",")
                {
                    j++;
                    continue;
                }

                break;
            }
        }
    }
}
