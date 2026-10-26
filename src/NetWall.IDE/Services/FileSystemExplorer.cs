using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public static class FileSystemExplorer
{
    private static readonly string[] ExcludedNames = [".git", "bin", "obj", "node_modules"];

    public static List<FileTreeNode> BuildChildren(string directoryPath, FileTreeNode? parent = null, int level = 0)
    {
        var result = new List<FileTreeNode>();

        IEnumerable<string> directories;
        IEnumerable<string> files;
        try
        {
            directories = Directory.EnumerateDirectories(directoryPath)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            files = Directory.EnumerateFiles(directoryPath)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return result;
        }

        foreach (var directory in directories)
        {
            var name = Path.GetFileName(directory);
            if (ExcludedNames.Contains(name))
            {
                continue;
            }

            var node = new FileTreeNode
            {
                Name = name,
                FullPath = directory,
                IsFolder = true,
                Level = level,
                Parent = parent,
            };
            foreach (var child in BuildChildren(directory, node, level + 1))
            {
                node.Children.Add(child);
            }

            result.Add(node);
        }

        foreach (var file in files)
        {
            result.Add(new FileTreeNode
            {
                Name = Path.GetFileName(file),
                FullPath = file,
                Level = level,
                Parent = parent,
            });
        }

        return result;
    }
}
