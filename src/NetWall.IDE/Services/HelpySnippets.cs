using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Snippets;

namespace NetWall.IDE.Services;

public sealed class HelpyField : SnippetReplaceableTextElement
{
    public string Tip { get; init; } = string.Empty;

    public InsertionContext? Context { get; private set; }

    public override void Insert(InsertionContext context)
    {
        base.Insert(context);
        Context = context;
    }

    public bool Contains(int offset) =>
        Context?.GetActiveElement(this) is { Segment: { } segment }
        && offset >= segment.Offset && offset <= segment.EndOffset;
}

public static partial class HelpySnippets
{
    private static readonly Dictionary<string, (string Hint, string Template)> Templates = new()
    {
        ["regla"] = ("Presiona Tab y armamos una regla juntos.",
            "regla ${nombre|Ponle un nombre que diga qué hace, como web o ssh_oficina.} {\n" +
            "    accion = ${permitir|¿Dejas pasar el tráfico (permitir) o lo frenas (bloquear)?},\n" +
            "    protocolo = ${tcp|El protocolo: tcp para web, correo o ssh; udp para dns o video.},\n" +
            "    puerto = ${80|El puerto del servicio: 80 web, 443 web segura, 22 ssh, 25 correo.}\n" +
            "};"),
        ["declarar"] = ("Presiona Tab para declarar una variable.",
            "declarar ${nombre|El nombre de la variable, en minúsculas y con _ entre palabras.}: " +
            "${Int|El tipo: Int para enteros, Float para decimales, String para texto.} = " +
            "${0|El valor inicial. Tiene que ser del tipo que elegiste.};"),
        ["red"] = ("Presiona Tab para declarar una red.",
            "declarar ${nombre|El nombre de la red, como oficina o servidores.}: Red = " +
            "${192.168.1.0/24|La red en formato CIDR: dirección/prefijo. Un /24 tiene 256 direcciones.};"),
    };

    public static string? HintFor(string trigger) => Templates.TryGetValue(trigger, out var entry) ? entry.Hint : null;

    public static Snippet? Find(string trigger)
    {
        if (!Templates.TryGetValue(trigger, out var entry))
        {
            return null;
        }

        var snippet = new Snippet();
        var last = 0;
        foreach (Match match in Placeholder().Matches(entry.Template))
        {
            snippet.Elements.Add(new SnippetTextElement { Text = entry.Template[last..match.Index] });
            snippet.Elements.Add(new HelpyField { Text = match.Groups[1].Value, Tip = match.Groups[2].Value });
            last = match.Index + match.Length;
        }

        snippet.Elements.Add(new SnippetTextElement { Text = entry.Template[last..] });
        snippet.Elements.Add(new SnippetCaretElement());
        return snippet;
    }

    [GeneratedRegex(@"\$\{([^|}]*)\|([^}]*)\}")]
    private static partial Regex Placeholder();
}
