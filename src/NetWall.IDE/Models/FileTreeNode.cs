using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public partial class FileTreeNode : ObservableObject
{
    public required string FullPath { get; set; }
    public bool IsFolder { get; init; }
    public int Level { get; init; }
    public FileTreeNode? Parent { get; init; }
    public ObservableCollection<FileTreeNode> Children { get; init; } = [];

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial bool IsMain { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string EditingName { get; set; } = string.Empty;
}
