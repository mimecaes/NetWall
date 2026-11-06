using System.Collections.Generic;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Services;

public readonly record struct SyntaxSpan(int Start, int Length, SyntaxToken Token);

/// <summary>
/// Line-based port of NetWall.Lang/src/lexer.py for highlighting. The language has no
/// multi-line tokens (strings stop at the end of the line and comments are "//"), so
/// scanning one line at a time gives the same tokens as the real lexer.
/// </summary>
public static class NetWallSyntaxScanner
{
    private static readonly Dictionary<string, SyntaxToken> ReservedWords = new()
    {
        // Declarations and control flow.
        ["regla"] = SyntaxToken.Keyword,
        ["funcion"] = SyntaxToken.Keyword,
        ["importar"] = SyntaxToken.Keyword,
        ["simular"] = SyntaxToken.Keyword,
        ["principal"] = SyntaxToken.Keyword,
        ["politica_defecto"] = SyntaxToken.Keyword,
        ["si"] = SyntaxToken.Keyword,
        ["sino"] = SyntaxToken.Keyword,
        ["para"] = SyntaxToken.Keyword,
        ["cada"] = SyntaxToken.Keyword,
        ["en"] = SyntaxToken.Keyword,

        // Fields of a regla / struct literal.
        ["accion"] = SyntaxToken.Field,
        ["protocolo"] = SyntaxToken.Field,
        ["puerto"] = SyntaxToken.Field,
        ["origen"] = SyntaxToken.Field,
        ["destino"] = SyntaxToken.Field,
        ["comentario"] = SyntaxToken.Field,

        ["Red"] = SyntaxToken.Type,
        ["Int"] = SyntaxToken.Type,
        ["Float"] = SyntaxToken.Type,
        ["String"] = SyntaxToken.Type,
        ["Boolean"] = SyntaxToken.Type,
        ["Lista"] = SyntaxToken.Type,

        ["y"] = SyntaxToken.Operator,
        ["o"] = SyntaxToken.Operator,
        ["no"] = SyntaxToken.Operator,

        ["permitir"] = SyntaxToken.Constant,
        ["bloquear"] = SyntaxToken.Constant,
        ["tcp"] = SyntaxToken.Constant,
        ["udp"] = SyntaxToken.Constant,
        ["cualquiera"] = SyntaxToken.Constant,
        ["verdadero"] = SyntaxToken.Constant,
        ["falso"] = SyntaxToken.Constant,

        // rango(INT, INT) is used like a built-in call.
        ["rango"] = SyntaxToken.Function,
    };

    private static readonly HashSet<string> PredefinedFunctions =
        ["mostrar", "tamano", "contiene", "interseccion", "mascara", "direccion_red", "broadcast", "binario"];

    private const string Punctuation = "{}()[],;";
    private const string SingleOperators = "=<>!";

    public static bool IsReserved(string word) => ReservedWords.ContainsKey(word);

    public static bool IsPredefined(string word) => PredefinedFunctions.Contains(word);

    public static List<SyntaxSpan> ScanLine(string text)
    {
        var spans = new List<SyntaxSpan>();
        var i = 0;
        var end = text.Length;
        string? previousWord = null;

        while (i < end)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < end && text[i + 1] == '/')
            {
                spans.Add(new SyntaxSpan(i, end - i, SyntaxToken.Comment));
                break;
            }

            var start = i;

            if (c == '"')
            {
                // Unclosed strings still read as strings while the user is typing them.
                i++;
                while (i < end && text[i] != '"')
                {
                    i++;
                }

                i = System.Math.Min(i + 1, end);
                spans.Add(new SyntaxSpan(start, i - start, SyntaxToken.String));
                previousWord = null;
                continue;
            }

            if (char.IsDigit(c))
            {
                while (i < end && char.IsDigit(text[i]))
                {
                    i++;
                }

                if (i + 1 < end && text[i] == '.' && char.IsDigit(text[i + 1]))
                {
                    i++;
                    while (i < end && char.IsDigit(text[i]))
                    {
                        i++;
                    }
                }

                spans.Add(new SyntaxSpan(start, i - start, SyntaxToken.Number));
                previousWord = null;
                continue;
            }

            if (char.IsLetter(c))
            {
                while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                var word = text[start..i];
                if (!ReservedWords.TryGetValue(word, out var token))
                {
                    token = previousWord == "funcion" || IsPredefined(word) || NextNonSpace(text, i) == '('
                        ? SyntaxToken.Function
                        : SyntaxToken.Identifier;
                }

                spans.Add(new SyntaxSpan(start, i - start, token));
                previousWord = word;
                continue;
            }

            previousWord = null;

            // Two character operators are checked before the one character symbols.
            if (i + 1 < end && IsDoubleOperator(c, text[i + 1]))
            {
                i += 2;
                spans.Add(new SyntaxSpan(start, 2, SyntaxToken.Operator));
                continue;
            }

            i++;
            var kind = SingleOperators.Contains(c) ? SyntaxToken.Operator
                : Punctuation.Contains(c) ? SyntaxToken.Punctuation
                : SyntaxToken.Error;
            spans.Add(new SyntaxSpan(start, 1, kind));
        }

        return spans;
    }

    private static bool IsDoubleOperator(char first, char second) =>
        (first, second) is ('=', '=') or ('!', '=') or ('<', '=') or ('>', '=') or ('&', '&') or ('|', '|');

    private static char NextNonSpace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index < text.Length ? text[index] : '\0';
    }
}
