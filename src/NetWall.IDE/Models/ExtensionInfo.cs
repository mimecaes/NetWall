using CommunityToolkit.Mvvm.ComponentModel;
using NetWall.IDE.Localization;

namespace NetWall.IDE.Models;

public partial class ExtensionInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string DescriptionKey { get; init; }
    public required string IconPath { get; init; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public ExtensionInfo() =>
        LocalizationService.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Description));

    public string Description => LocalizationService.T(DescriptionKey);
}
