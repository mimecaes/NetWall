using System.Collections.ObjectModel;

namespace NetWall.IDE.Models;

public class FileTreeNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsFolder { get; init; }
    public ObservableCollection<FileTreeNode> Children { get; init; } = [];
}
