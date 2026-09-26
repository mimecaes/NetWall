using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NetWall.IDE.Themes;

/// <summary>
/// Token categories the editor colors. They mirror the token types produced by
/// NetWall.Lang/src/lexer.py, with KEYWORD and IDENT split by their role in the grammar.
/// </summary>
public enum SyntaxToken
{
    Keyword,
    Field,
    Type,
    Constant,
    Function,
    Identifier,
    String,
    Number,
    Comment,
    Operator,
    Punctuation,
    Error,
}

/// <summary>
/// Syntax colors for one theme, read from the "{Theme}.Syntax.{Token}" colors in that
/// theme's ResourceDictionary.
/// </summary>
public sealed class SyntaxPalette
{
    private readonly Dictionary<SyntaxToken, IBrush> _brushes;

    private SyntaxPalette(Dictionary<SyntaxToken, IBrush> brushes) => _brushes = brushes;

    public static SyntaxPalette Empty { get; } = new([]);

    public IBrush? this[SyntaxToken token] => _brushes.GetValueOrDefault(token);

    public static SyntaxPalette FromResources(ResourceDictionary themeDictionary, string prefix)
    {
        var brushes = new Dictionary<SyntaxToken, IBrush>();
        foreach (var token in Enum.GetValues<SyntaxToken>())
        {
            var key = $"{prefix}.Syntax.{token}";
            if (themeDictionary.TryGetValue(key, out var value) && value is Color color)
            {
                brushes[token] = new ImmutableSolidColorBrush(color);
            }
        }

        return new SyntaxPalette(brushes);
    }
}
