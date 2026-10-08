using System;
using System.Collections.Generic;
using System.Text;

namespace NetWall.IDE.Services;

/// <summary>
/// A run of text with the same ANSI attributes. <see cref="Color"/> is an index into the
/// 16-color terminal palette (0-7 normal, 8-15 bright), or null for the default foreground.
/// </summary>
public sealed record AnsiSegment(string Text, int? Color, bool Dim, string? Link);

/// <summary>
/// Parses the subset of ANSI escape sequences the output panel renders: SGR colors
/// (30-37, 90-97, 39), dim (2/22), reset (0) and OSC 8 hyperlinks. Any other escape
/// sequence (cursor movement, erase, window title...) is dropped.
/// </summary>
public static class AnsiParser
{
    private const char Esc = '\x1b';

    public static List<AnsiSegment> Parse(string text)
    {
        var segments = new List<AnsiSegment>();
        var buffer = new StringBuilder();
        int? color = null;
        var dim = false;
        string? link = null;

        void Flush()
        {
            if (buffer.Length > 0)
            {
                segments.Add(new AnsiSegment(buffer.ToString(), color, dim, link));
                buffer.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\r')
            {
                i++;
                continue;
            }

            if (c != Esc || i + 1 >= text.Length)
            {
                buffer.Append(c);
                i++;
                continue;
            }

            if (text[i + 1] == '[')
            {
                // CSI: parameters, then a final byte in the range '@'..'~'.
                var end = i + 2;
                while (end < text.Length && (text[end] < '@' || text[end] > '~'))
                {
                    end++;
                }

                if (end < text.Length && text[end] == 'm')
                {
                    Flush();
                    ApplySgr(text[(i + 2)..end], ref color, ref dim);
                }

                i = end + 1;
                continue;
            }

            if (text[i + 1] == ']')
            {
                // OSC: terminated by BEL or ESC '\'.
                var start = i + 2;
                var end = start;
                while (end < text.Length && text[end] != '\a' && !(text[end] == Esc && end + 1 < text.Length && text[end + 1] == '\\'))
                {
                    end++;
                }

                var body = text[start..Math.Min(end, text.Length)];
                if (body.StartsWith("8;", StringComparison.Ordinal))
                {
                    Flush();
                    var uriStart = body.IndexOf(';', 2);
                    var uri = uriStart >= 0 ? body[(uriStart + 1)..] : string.Empty;
                    link = uri.Length > 0 ? uri : null;
                }

                i = end < text.Length && text[end] == '\a' ? end + 1 : end + 2;
                continue;
            }

            // Two-character escape (e.g. ESC '(' B charset selection is longer, but rare): skip it.
            i += 2;
        }

        Flush();
        return segments;
    }

    private static void ApplySgr(string parameters, ref int? color, ref bool dim)
    {
        if (parameters.Length == 0)
        {
            color = null;
            dim = false;
            return;
        }

        var parts = parameters.Split(';');
        for (var p = 0; p < parts.Length; p++)
        {
            if (!int.TryParse(parts[p], out var code))
            {
                continue;
            }

            switch (code)
            {
                // 256-color and true-color forms (38;5;n / 38;2;r;g;b and the 48 background
                // variants) are not part of the theme palette; skip their arguments.
                case 38 or 48:
                    p += p + 1 < parts.Length && parts[p + 1] == "2" ? 4 : 2;
                    break;
                case 0:
                    color = null;
                    dim = false;
                    break;
                case 2:
                    dim = true;
                    break;
                case 22:
                    dim = false;
                    break;
                case >= 30 and <= 37:
                    color = code - 30;
                    break;
                case 39:
                    color = null;
                    break;
                case >= 90 and <= 97:
                    color = code - 90 + 8;
                    break;
            }
        }
    }
}
