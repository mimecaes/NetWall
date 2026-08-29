using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.ViewModels;

public partial class EditorTabViewModel : ViewModelBase
{
    private bool _suppressDirty;

    [ObservableProperty]
    public partial string Title { get; set; } = "untitled";

    [ObservableProperty]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial int CaretIndex { get; set; }

    [ObservableProperty]
    public partial int Line { get; set; } = 1;

    [ObservableProperty]
    public partial int Column { get; set; } = 1;

    public string Language => LanguageFromPath(FilePath ?? Title) ?? "Plain Text";

    public void LoadContent(string content)
    {
        _suppressDirty = true;
        Content = content;
        IsDirty = false;
        _suppressDirty = false;
    }

    partial void OnContentChanged(string value)
    {
        if (!_suppressDirty)
        {
            IsDirty = true;
        }

        UpdateLineColumn();
    }

    partial void OnCaretIndexChanged(int value) => UpdateLineColumn();

    partial void OnFilePathChanged(string? value) => OnPropertyChanged(nameof(Language));

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(Language));

    private void UpdateLineColumn()
    {
        var index = Math.Clamp(CaretIndex, 0, Content.Length);
        var line = 1;
        var column = 1;
        for (var i = 0; i < index; i++)
        {
            if (Content[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        Line = line;
        Column = column;
    }

    private static string? LanguageFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".nw" => "NetWall",
            ".cs" => "C#",
            ".xaml" or ".axaml" => "XAML",
            ".json" => "JSON",
            ".md" => "Markdown",
            ".xml" => "XML",
            _ => "Plain Text",
        };
    }
}
