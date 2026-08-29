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

    public static List<FileTreeNode> BuildChildren(string directoryPath)
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
        catch (UnauthorizedAccessException)
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

            result.Add(new FileTreeNode
            {
                Name = name,
                FullPath = directory,
                IsFolder = true,
                Children = new ObservableCollection<FileTreeNode>(BuildChildren(directory)),
            });
        }

        foreach (var file in files)
        {
            result.Add(new FileTreeNode { Name = Path.GetFileName(file), FullPath = file });
        }

        return result;
    }
}
