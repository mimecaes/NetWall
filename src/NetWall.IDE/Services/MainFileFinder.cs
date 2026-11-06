using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetWall.IDE.Themes;

namespace NetWall.IDE.Services;

public static class MainFileFinder
{
    private const string MainKeyword = "principal";
    private const string BlockStart = "{";

    public static List<string> Find(string directory, bool recursive) =>
        EnumerateNetWallFiles(directory, recursive)
            .Where(HasMainBlock)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool ContainsMainBlock(string source)
    {
        if (!source.Contains(MainKeyword, StringComparison.Ordinal))
        {
            return false;
        }

        var afterMainKeyword = false;
        foreach (var line in source.Split('\n'))
        {
            foreach (var span in NetWallSyntaxScanner.ScanLine(line))
            {
                if (span.Token == SyntaxToken.Comment)
                {
                    continue;
                }

                var text = line.Substring(span.Start, span.Length);
                if (afterMainKeyword && text == BlockStart)
                {
                    return true;
                }

                afterMainKeyword = span.Token == SyntaxToken.Keyword && text == MainKeyword;
            }
        }

        return false;
    }

    public static bool HasMainBlock(string path)
    {
        try
        {
            return ContainsMainBlock(File.ReadAllText(path));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateNetWallFiles(string directory, bool recursive)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                files.AddRange(Directory.EnumerateFiles(current, "*.nw"));
                if (!recursive)
                {
                    continue;
                }

                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    if (!FileSystemExplorer.IsExcluded(Path.GetFileName(child)))
                    {
                        pending.Push(child);
                    }
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                // A folder that cannot be read is skipped, as the file explorer does.
            }
        }

        return files;
    }
}
