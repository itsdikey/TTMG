using System.Text;

namespace TTMG.Scripting
{
    internal enum LuaTokenKind
    {
        Whitespace,
        Comment,
        String,
        Identifier,
        Keyword,
        Number,
        Symbol
    }

    internal readonly struct LuaToken
    {
        public LuaTokenKind Kind { get; }
        public string Text { get; }

        public LuaToken(LuaTokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    internal static class LuaLexer
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto",
            "if", "in", "local", "nil", "not", "or", "repeat", "return", "then", "true",
            "until", "while"
        };

        private static readonly string[] MultiCharSymbols =
        {
            "...", "..", "==", "~=", "<=", ">=", "//", "<<", ">>", "::"
        };

        public static List<LuaToken> Tokenize(string source)
        {
            var tokens = new List<LuaToken>();
            var i = 0;
            var length = source.Length;

            while (i < length)
            {
                var c = source[i];

                if (IsWhitespace(c))
                {
                    var start = i;
                    while (i < length && IsWhitespace(source[i])) i++;
                    tokens.Add(new LuaToken(LuaTokenKind.Whitespace, source.Substring(start, i - start)));
                    continue;
                }

                if (c == '-' && i + 1 < length && source[i + 1] == '-')
                {
                    var start = i;
                    i += 2;
                    if (TryReadLongBracketOpen(source, i, out var level, out var contentStart))
                    {
                        i = FindLongBracketEnd(source, contentStart, level);
                    }
                    else
                    {
                        while (i < length && source[i] != '\n') i++;
                    }

                    tokens.Add(new LuaToken(LuaTokenKind.Comment, source.Substring(start, i - start)));
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    var start = i;
                    i = ReadShortString(source, i);
                    tokens.Add(new LuaToken(LuaTokenKind.String, source.Substring(start, i - start)));
                    continue;
                }

                if (c == '[' && TryReadLongBracketOpen(source, i, out var longLevel, out var longContentStart))
                {
                    var start = i;
                    i = FindLongBracketEnd(source, longContentStart, longLevel);
                    tokens.Add(new LuaToken(LuaTokenKind.String, source.Substring(start, i - start)));
                    continue;
                }

                if (IsIdentifierStart(c))
                {
                    var start = i;
                    i++;
                    while (i < length && IsIdentifierPart(source[i])) i++;
                    var text = source.Substring(start, i - start);
                    var kind = Keywords.Contains(text) ? LuaTokenKind.Keyword : LuaTokenKind.Identifier;
                    tokens.Add(new LuaToken(kind, text));
                    continue;
                }

                if (char.IsDigit(c))
                {
                    var start = i;
                    i = ReadNumber(source, i);
                    tokens.Add(new LuaToken(LuaTokenKind.Number, source.Substring(start, i - start)));
                    continue;
                }

                var symbol = MatchSymbol(source, i);
                tokens.Add(new LuaToken(LuaTokenKind.Symbol, symbol));
                i += symbol.Length;
            }

            return tokens;
        }

        private static string MatchSymbol(string source, int index)
        {
            foreach (var candidate in MultiCharSymbols)
            {
                if (index + candidate.Length <= source.Length &&
                    string.CompareOrdinal(source, index, candidate, 0, candidate.Length) == 0)
                {
                    return candidate;
                }
            }

            return source[index].ToString();
        }

        private static int ReadShortString(string source, int start)
        {
            var quote = source[start];
            var i = start + 1;
            while (i < source.Length)
            {
                var c = source[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                if (c == quote)
                {
                    i++;
                    break;
                }

                if (c == '\n')
                {
                    break;
                }

                i++;
            }

            return i > source.Length ? source.Length : i;
        }

        private static int ReadNumber(string source, int start)
        {
            var i = start;

            if (source[i] == '0' && i + 1 < source.Length && (source[i + 1] == 'x' || source[i + 1] == 'X'))
            {
                i += 2;
                while (i < source.Length && IsHexDigit(source[i])) i++;
                if (i < source.Length && source[i] == '.')
                {
                    i++;
                    while (i < source.Length && IsHexDigit(source[i])) i++;
                }

                if (i < source.Length && (source[i] == 'p' || source[i] == 'P'))
                {
                    i++;
                    if (i < source.Length && (source[i] == '+' || source[i] == '-')) i++;
                    while (i < source.Length && char.IsDigit(source[i])) i++;
                }

                return i;
            }

            while (i < source.Length && char.IsDigit(source[i])) i++;
            if (i + 1 < source.Length && source[i] == '.' && char.IsDigit(source[i + 1]))
            {
                i++;
                while (i < source.Length && char.IsDigit(source[i])) i++;
            }

            if (i < source.Length && (source[i] == 'e' || source[i] == 'E'))
            {
                var save = i;
                i++;
                if (i < source.Length && (source[i] == '+' || source[i] == '-')) i++;
                if (i < source.Length && char.IsDigit(source[i]))
                {
                    while (i < source.Length && char.IsDigit(source[i])) i++;
                }
                else
                {
                    i = save;
                }
            }

            return i;
        }

        private static bool TryReadLongBracketOpen(string source, int index, out int level, out int contentStart)
        {
            level = 0;
            contentStart = index;

            if (index >= source.Length || source[index] != '[')
            {
                return false;
            }

            var i = index + 1;
            while (i < source.Length && source[i] == '=')
            {
                level++;
                i++;
            }

            if (i < source.Length && source[i] == '[')
            {
                contentStart = i + 1;
                return true;
            }

            level = 0;
            return false;
        }

        private static int FindLongBracketEnd(string source, int contentStart, int level)
        {
            var i = contentStart;
            while (i < source.Length)
            {
                if (source[i] == ']')
                {
                    var j = i + 1;
                    var equals = 0;
                    while (j < source.Length && source[j] == '=')
                    {
                        equals++;
                        j++;
                    }

                    if (equals == level && j < source.Length && source[j] == ']')
                    {
                        return j + 1;
                    }
                }

                i++;
            }

            return source.Length;
        }

        private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f' or '\v';

        private static bool IsIdentifierStart(char c) => c == '_' || char.IsLetter(c);

        private static bool IsIdentifierPart(char c) => c == '_' || char.IsLetterOrDigit(c);

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
