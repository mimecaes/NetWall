using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Snippets;

namespace NetWall.IDE.Services;

public static partial class HelpySnippets
{
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["regla"] = "regla ${nombre} {\n    accion = ${permitir},\n    protocolo = ${tcp},\n    puerto = ${80}\n};",
        ["declarar"] = "declarar ${nombre}: ${Int} = ${0};",
        ["red"] = "declarar ${nombre}: Red = ${192.168.1.0/24};",
    };

    public static Snippet? Find(string trigger)
    {
        if (!Templates.TryGetValue(trigger, out var template))
        {
            return null;
        }

        var snippet = new Snippet();
        var last = 0;
        foreach (Match match in Placeholder().Matches(template))
        {
            snippet.Elements.Add(new SnippetTextElement { Text = template[last..match.Index] });
            snippet.Elements.Add(new SnippetReplaceableTextElement { Text = match.Groups[1].Value });
            last = match.Index + match.Length;
        }

        snippet.Elements.Add(new SnippetTextElement { Text = template[last..] });
        snippet.Elements.Add(new SnippetCaretElement());
        return snippet;
    }

    [GeneratedRegex(@"\$\{([^}]*)\}")]
    private static partial Regex Placeholder();
}
