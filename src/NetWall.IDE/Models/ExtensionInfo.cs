using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public partial class ExtensionInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IconPath { get; init; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}
