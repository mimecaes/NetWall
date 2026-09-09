using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public partial class GlobalSearchResult : ObservableObject
{
    public required FileTreeNode Node { get; init; }
    public required string RelativeFolder { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
