using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private TerminalSession? _terminal;
    private int _untitledCounter;

    [ObservableProperty]
    public partial string RootPath { get; set; } = Environment.CurrentDirectory;

    [ObservableProperty]
    public partial string RootName { get; set; } = "NO FOLDER OPENED";

    [ObservableProperty]
    public partial bool IsFolderOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsTerminalVisible { get; set; } = true;

    [ObservableProperty]
    public partial string TerminalOutput { get; set; } = "NetWall.IDE terminal ready.";

    [ObservableProperty]
    public partial string TerminalInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial EditorTabViewModel? SelectedTab { get; set; }

    public ObservableCollection<FileTreeNode> RootNodes { get; } = [];

    public ObservableCollection<EditorTabViewModel> OpenTabs { get; } = [];

    public event Action<EditorTabViewModel>? RequestSaveAs;

    public event Action? RequestOpenFolder;

    [RelayCommand]
    private void NewFile()
    {
        var suffix = _untitledCounter == 0 ? string.Empty : $"-{_untitledCounter}";
        _untitledCounter++;

        var tab = new EditorTabViewModel { Title = $"untitled{suffix}.nw" };
        OpenTabs.Add(tab);
        SelectTab(tab);
    }

    [RelayCommand]
    private void OpenFolder() => RequestOpenFolder?.Invoke();

    public void LoadFolder(string path)
    {
        RootPath = path;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        RootName = string.IsNullOrEmpty(name) ? path : name;

        RootNodes.Clear();
        foreach (var node in FileSystemExplorer.BuildChildren(path))
        {
            RootNodes.Add(node);
        }

        IsFolderOpen = true;
        _terminal = new TerminalSession(path);
    }

    public void OpenNode(FileTreeNode? node)
    {
        if (node is null || node.IsFolder)
        {
            return;
        }

        var existing = OpenTabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectTab(existing);
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(node.FullPath);
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to open '{node.FullPath}': {ex.Message}");
            return;
        }

        var tab = new EditorTabViewModel { Title = node.Name, FilePath = node.FullPath };
        tab.LoadContent(text);
        OpenTabs.Add(tab);
        SelectTab(tab);
    }

    [RelayCommand]
    private void SelectTab(EditorTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        foreach (var t in OpenTabs)
        {
            t.IsSelected = false;
        }

        tab.IsSelected = true;
        SelectedTab = tab;
    }

    [RelayCommand]
    private void CloseTab(EditorTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var index = OpenTabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        var wasSelected = SelectedTab == tab;
        OpenTabs.RemoveAt(index);

        if (!wasSelected)
        {
            return;
        }

        SelectedTab = null;
        if (OpenTabs.Count > 0)
        {
            SelectTab(OpenTabs[Math.Min(index, OpenTabs.Count - 1)]);
        }
    }

    public void SaveTab(EditorTabViewModel tab, string? pathOverride = null)
    {
        var path = pathOverride ?? tab.FilePath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, tab.Content);
            tab.FilePath = path;
            tab.Title = Path.GetFileName(path);
            tab.IsDirty = false;
            AppendTerminalLine($"Saved '{path}'.");
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to save '{path}': {ex.Message}");
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedTab is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(SelectedTab.FilePath))
        {
            RequestSaveAs?.Invoke(SelectedTab);
            return;
        }

        SaveTab(SelectedTab);
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (SelectedTab is null)
        {
            return;
        }

        RequestSaveAs?.Invoke(SelectedTab);
    }

    [RelayCommand]
    private void SaveAll()
    {
        foreach (var tab in OpenTabs.Where(t => t.IsDirty))
        {
            if (string.IsNullOrEmpty(tab.FilePath))
            {
                RequestSaveAs?.Invoke(tab);
            }
            else
            {
                SaveTab(tab);
            }
        }
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    private void ToggleTerminal() => IsTerminalVisible = !IsTerminalVisible;

    [RelayCommand]
    private async Task RunActiveFileAsync()
    {
        if (SelectedTab is null)
        {
            AppendTerminalLine("No active file to run.");
            return;
        }

        if (string.IsNullOrEmpty(SelectedTab.FilePath))
        {
            AppendTerminalLine("Save the file before running it.");
            return;
        }

        if (SelectedTab.IsDirty)
        {
            SaveTab(SelectedTab);
        }

        IsTerminalVisible = true;
        _terminal ??= new TerminalSession(Path.GetDirectoryName(SelectedTab.FilePath) ?? RootPath);

        AppendTerminalLine($"$ {SelectedTab.FilePath}");
        var output = await _terminal.RunAsync($"\"{SelectedTab.FilePath}\"");
        if (!string.IsNullOrEmpty(output))
        {
            AppendTerminalLine(output);
        }
    }

    [RelayCommand]
    private async Task RunTerminalLineAsync()
    {
        var command = TerminalInput.Trim();
        TerminalInput = string.Empty;
        if (command.Length == 0)
        {
            return;
        }

        _terminal ??= new TerminalSession(RootPath);
        AppendTerminalLine($"$ {command}");
        var output = await _terminal.RunAsync(command);
        if (!string.IsNullOrEmpty(output))
        {
            AppendTerminalLine(output);
        }
    }

    [RelayCommand]
    private void ClearTerminal() => TerminalOutput = string.Empty;

    private void AppendTerminalLine(string line)
    {
        TerminalOutput = TerminalOutput.Length > 0 ? $"{TerminalOutput}\n{line}" : line;
    }
}
