using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly RecentProjectsService _recentProjectsService;

    public MainViewModel()
    {
        _recentProjectsService = new RecentProjectsService();
        LoadRecentProjects();
        OpenTabs.CollectionChanged += OnOpenTabsChanged;

        Extensions.Add(new ExtensionInfo
        {
            Id = "indent-rainbow",
            Name = "Indent Rainbow",
            Description = "Colorea cada nivel de indentación en el editor",
            IconPath = "avares://NetWall.IDE/Assets/Icons/identrainbow.png",
            IsEnabled = false,
        });
        Extensions.Add(new ExtensionInfo
        {
            Id = "dev-cat",
            Name = "DevCat",
            Description = "Una mascota gatito que vive en la terminal",
            IconPath = "avares://NetWall.IDE/Assets/Icons/devcat.png",
            IsEnabled = false,
        });

        foreach (var extension in Extensions)
        {
            extension.PropertyChanged += OnExtensionPropertyChanged;
        }
    }

    public ObservableCollection<ExtensionInfo> Extensions { get; } = [];

    public ObservableCollection<RecentProject> RecentProjects { get; } = [];

    [ObservableProperty]
    public partial string ExtensionSearchText { get; set; } = string.Empty;

    private void LoadRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var project in _recentProjectsService.LoadRecent().Take(7))
        {
            RecentProjects.Add(project);
        }
    }

    private void AddRecentProject(string folderPath)
    {
        _recentProjectsService.AddOrUpdateRecent(folderPath);
        LoadRecentProjects();
    }

    partial void OnExtensionSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredExtensions));

    public IEnumerable<ExtensionInfo> FilteredExtensions
    {
        get
        {
            var search = ExtensionSearchText.ToLowerInvariant();
            return Extensions.Where(e =>
                e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    public int EnabledExtensionsCount => Extensions.Count(e => e.IsEnabled);

    public int TotalExtensionsCount => Extensions.Count;

    public string ExtensionsCountText => $"{EnabledExtensionsCount} de {TotalExtensionsCount} activas";

    public event Action<ExtensionInfo>? ExtensionToggled;

    private void OnExtensionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExtensionInfo.IsEnabled) || sender is not ExtensionInfo extension)
        {
            return;
        }

        OnPropertyChanged(nameof(IsIndentRainbowEnabled));
        OnPropertyChanged(nameof(IsPetEnabled));
        OnPropertyChanged(nameof(EnabledExtensionsCount));
        OnPropertyChanged(nameof(ExtensionsCountText));
        ExtensionToggled?.Invoke(extension);
    }

    public bool IsIndentRainbowEnabled => Extensions.FirstOrDefault(e => e.Id == "indent-rainbow")?.IsEnabled ?? true;

    public bool IsPetEnabled => Extensions.FirstOrDefault(e => e.Id == "dev-cat")?.IsEnabled ?? true;

    private void OnOpenTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (EditorTabViewModel tab in e.NewItems)
            {
                tab.PropertyChanged += OnTabPropertyChanged;
            }
        }

        RecomputeDirtyState();
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorTabViewModel.IsDirty))
        {
            RecomputeDirtyState();
        }
    }

    private void RecomputeDirtyState()
    {
        DirtyTabCount = OpenTabs.Count(t => t.IsDirty);
        SaveCommand.NotifyCanExecuteChanged();
        SaveAllCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    public partial string RootPath { get; set; } = Environment.CurrentDirectory;

    [ObservableProperty]
    public partial string RootName { get; set; } = "NO FOLDER OPENED";

    partial void OnRootNameChanged(string value) => OnPropertyChanged(nameof(StatusBarPath));

    public string StatusBarPath
    {
        get
        {
            if (SelectedTab?.FilePath is { Length: > 0 } path)
            {
                var relative = Path.GetRelativePath(RootPath, path).Replace('\\', '/');
                return $"{RootName}/{relative}";
            }

            return RootName;
        }
    }

    [ObservableProperty]
    public partial bool IsFolderOpen { get; set; }

    partial void OnIsFolderOpenChanged(bool value)
    {
        NewFileCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowTree));
        OnPropertyChanged(nameof(ShowSearchResults));
        OnPropertyChanged(nameof(ShowNoTabPrompt));
    }

    public bool ShowNoTabPrompt => IsFolderOpen && SelectedTab is null;

    [ObservableProperty]
    public partial bool IsSidebarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsTerminalVisible { get; set; } = true;

    public SimulationViewModel Simulation { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelHeight))]
    public partial bool IsSimulationTabActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelHeight))]
    public partial double AvailableHeight { get; set; } = 800;

    private double? _terminalHeight;
    private double? _simulationHeight;

    private double MaxPanelHeight => Math.Max(120, AvailableHeight - 200);

    public double PanelHeight => Math.Min(MaxPanelHeight, IsSimulationTabActive
        ? _simulationHeight ?? Math.Clamp(AvailableHeight * 0.7, 470, 900)
        : _terminalHeight ?? 220);

    public void ResizePanel(double height)
    {
        var minimum = IsSimulationTabActive ? 470 : 120;
        var clamped = Math.Clamp(height, minimum, Math.Max(minimum, MaxPanelHeight));
        if (IsSimulationTabActive)
        {
            _simulationHeight = clamped;
        }
        else
        {
            _terminalHeight = clamped;
        }

        OnPropertyChanged(nameof(PanelHeight));
    }

    public void ResetPanelHeight()
    {
        if (IsSimulationTabActive)
        {
            _simulationHeight = null;
        }
        else
        {
            _terminalHeight = null;
        }

        OnPropertyChanged(nameof(PanelHeight));
    }

    [RelayCommand]
    private void ShowTerminalTab() => IsSimulationTabActive = false;

    [RelayCommand]
    private void ShowSimulationTab() => IsSimulationTabActive = true;

    partial void OnIsSimulationTabActiveChanged(bool value) => Simulation.IsVisible = value;

    public ObservableCollection<TerminalEntry> TerminalEntries { get; } =
        [new TerminalLine(TerminalLineKind.Info, "NetWall.IDE terminal ready.")];

    /// <summary>Raised whenever terminal content is added, so the view can scroll to the end.</summary>
    public event Action? TerminalUpdated;

    /// <summary>Raised after a file was opened from a terminal link, with the 1-based line to reveal.</summary>
    public event Action<int>? RequestRevealLine;

    [ObservableProperty]
    public partial string TerminalInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial EditorTabViewModel? SelectedTab { get; set; }

    partial void OnSelectedTabChanged(EditorTabViewModel? value)
    {
        OnPropertyChanged(nameof(StatusBarPath));
        OnPropertyChanged(nameof(ShowNoTabPrompt));
        SaveCommand.NotifyCanExecuteChanged();

        if (value?.FilePath is not { } path)
        {
            return;
        }

        var node = FindNode(RootNodes, path);
        if (node is null)
        {
            return;
        }

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.IsExpanded = true;
        }

        SelectedTreeNode = node;
    }

    [ObservableProperty]
    public partial FileTreeNode? SelectedTreeNode { get; set; }

    partial void OnSelectedTreeNodeChanged(FileTreeNode? value)
    {
        if (value is { IsFolder: false })
        {
            OpenNode(value);
        }
    }

    [ObservableProperty]
    public partial string FileFilter { get; set; } = string.Empty;

    partial void OnFileFilterChanged(string value)
    {
        RebuildFilteredNodes();
        OnPropertyChanged(nameof(IsFiltering));
        OnPropertyChanged(nameof(ShowTree));
        OnPropertyChanged(nameof(ShowSearchResults));
    }

    public bool IsFiltering => !string.IsNullOrWhiteSpace(FileFilter);

    public bool ShowTree => IsFolderOpen && !IsFiltering;

    public bool ShowSearchResults => IsFolderOpen && IsFiltering;

    public ObservableCollection<FileTreeNode> RootNodes { get; } = [];

    public ObservableCollection<FileSearchResult> FilteredNodes { get; } = [];

    [ObservableProperty]
    public partial bool HasSearchResults { get; set; }

    public ObservableCollection<EditorTabViewModel> OpenTabs { get; } = [];

    [ObservableProperty]
    public partial int DirtyTabCount { get; set; }

    partial void OnDirtyTabCountChanged(int value) => OnPropertyChanged(nameof(ShowSaveAllBadge));

    public bool ShowSaveAllBadge => DirtyTabCount >= 2;

    public event Action<EditorTabViewModel>? RequestSaveAs;

    public event Action? RequestOpenFolder;

    public event Func<FileTreeNode, Task<bool>>? RequestDeleteConfirmation;

    private bool CanCreateNewFile() => IsFolderOpen;

    [RelayCommand(CanExecute = nameof(CanCreateNewFile))]
    private void NewFile()
    {
        var fileName = UniqueFileName(RootPath, "untitled", ".nw");
        var fullPath = Path.Combine(RootPath, fileName);
        if (!TryWriteEmptyFile(fullPath))
        {
            return;
        }

        InsertNode(RootNodes, new FileTreeNode { Name = fileName, FullPath = fullPath, Level = 0 });

        var newTab = new EditorTabViewModel { Title = fileName, FilePath = fullPath };
        OpenTabs.Add(newTab);
        SelectTab(newTab);
    }

    [RelayCommand]
    private void NewFileInFolder(FileTreeNode? folder)
    {
        if (folder is not { IsFolder: true })
        {
            return;
        }

        var fileName = UniqueFileName(folder.FullPath, "untitled", ".nw");
        var fullPath = Path.Combine(folder.FullPath, fileName);
        if (!TryWriteEmptyFile(fullPath))
        {
            return;
        }

        var node = new FileTreeNode { Name = fileName, FullPath = fullPath, Level = folder.Level + 1, Parent = folder };
        InsertNode(folder.Children, node);
        folder.IsExpanded = true;

        var tab = new EditorTabViewModel { Title = fileName, FilePath = fullPath };
        OpenTabs.Add(tab);
        SelectTab(tab);
    }

    [RelayCommand]
    private void NewFolderInFolder(FileTreeNode? parentFolder)
    {
        if (parentFolder is not { IsFolder: true })
        {
            return;
        }

        var folderName = UniqueFolderName(parentFolder.FullPath, "New Folder");
        var fullPath = Path.Combine(parentFolder.FullPath, folderName);

        try
        {
            Directory.CreateDirectory(fullPath);
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to create folder '{fullPath}': {ex.Message}", TerminalLineKind.Error);
            return;
        }

        var node = new FileTreeNode
        {
            Name = folderName,
            FullPath = fullPath,
            IsFolder = true,
            Level = parentFolder.Level + 1,
            Parent = parentFolder,
        };
        InsertNode(parentFolder.Children, node);
        parentFolder.IsExpanded = true;

        BeginRename(node);
    }

    [RelayCommand]
    private void BeginRename(FileTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.EditingName = node.Name;
        node.IsEditing = true;
    }

    [RelayCommand]
    private void CommitRename(FileTreeNode? node)
    {
        if (node is not { IsEditing: true })
        {
            return;
        }

        node.IsEditing = false;
        var newName = node.EditingName.Trim();
        if (string.IsNullOrEmpty(newName) || newName == node.Name)
        {
            return;
        }

        var parentPath = Path.GetDirectoryName(node.FullPath) ?? RootPath;
        var newPath = Path.Combine(parentPath, newName);

        try
        {
            if (node.IsFolder)
            {
                Directory.Move(node.FullPath, newPath);
            }
            else
            {
                File.Move(node.FullPath, newPath);
            }
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to rename '{node.FullPath}': {ex.Message}", TerminalLineKind.Error);
            return;
        }

        var oldPath = node.FullPath;
        node.FullPath = newPath;
        node.Name = newName;

        if (node.IsFolder)
        {
            node.Children.Clear();
            foreach (var child in FileSystemExplorer.BuildChildren(newPath, node, node.Level + 1))
            {
                node.Children.Add(child);
            }
        }
        else
        {
            var openTab = OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
            if (openTab is not null)
            {
                openTab.FilePath = newPath;
                openTab.Title = newName;
                if (openTab == SelectedTab)
                {
                    OnPropertyChanged(nameof(StatusBarPath));
                }
            }
        }
    }

    [RelayCommand]
    private void CancelRename(FileTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteNodeAsync(FileTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        var confirmed = RequestDeleteConfirmation is null || await RequestDeleteConfirmation(node);
        if (!confirmed)
        {
            return;
        }

        try
        {
            if (node.IsFolder)
            {
                Directory.Delete(node.FullPath, recursive: true);
            }
            else
            {
                File.Delete(node.FullPath);
            }
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to delete '{node.FullPath}': {ex.Message}", TerminalLineKind.Error);
            return;
        }

        (node.Parent?.Children ?? RootNodes).Remove(node);

        var prefix = node.FullPath + Path.DirectorySeparatorChar;
        var tabsToClose = OpenTabs
            .Where(t => t.FilePath is { } p &&
                        (string.Equals(p, node.FullPath, StringComparison.OrdinalIgnoreCase) ||
                         (node.IsFolder && p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        foreach (var tab in tabsToClose)
        {
            CloseTab(tab);
        }
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in RootNodes)
        {
            CollapseRecursive(node);
        }
    }

    private static void CollapseRecursive(FileTreeNode node)
    {
        if (!node.IsFolder)
        {
            return;
        }

        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseRecursive(child);
        }
    }

    [RelayCommand]
    private void OpenSearchResult(FileTreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            ancestor.IsExpanded = true;
        }

        SelectedTreeNode = node;
        FileFilter = string.Empty;
    }

    private void RebuildFilteredNodes()
    {
        FilteredNodes.Clear();
        if (string.IsNullOrWhiteSpace(FileFilter))
        {
            HasSearchResults = false;
            return;
        }

        foreach (var node in CollectMatchingFiles(RootNodes, FileFilter))
        {
            FilteredNodes.Add(new FileSearchResult(node, RelativeFolderOf(node)));
        }

        HasSearchResults = FilteredNodes.Count > 0;
    }

    private static IEnumerable<FileTreeNode> CollectMatchingFiles(IEnumerable<FileTreeNode> nodes, string filter)
    {
        foreach (var node in nodes)
        {
            if (node.IsFolder)
            {
                foreach (var match in CollectMatchingFiles(node.Children, filter))
                {
                    yield return match;
                }
            }
            else if (node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                yield return node;
            }
        }
    }

    private string RelativeFolderOf(FileTreeNode node)
    {
        var directory = Path.GetDirectoryName(node.FullPath) ?? RootPath;
        var relative = Path.GetRelativePath(RootPath, directory);
        return relative == "." ? RootName : relative.Replace('\\', '/');
    }

    private static FileTreeNode? FindNode(ObservableCollection<FileTreeNode> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder && string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (node.IsFolder && FindNode(node.Children, fullPath) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string UniqueFileName(string directory, string baseName, string extension)
    {
        var fileName = baseName + extension;
        var counter = 1;
        while (File.Exists(Path.Combine(directory, fileName)))
        {
            fileName = $"{baseName}-{counter}{extension}";
            counter++;
        }

        return fileName;
    }

    private static string UniqueFolderName(string parentDirectory, string baseName)
    {
        var folderName = baseName;
        var counter = 1;
        while (Directory.Exists(Path.Combine(parentDirectory, folderName)))
        {
            folderName = $"{baseName} {counter}";
            counter++;
        }

        return folderName;
    }

    private bool TryWriteEmptyFile(string fullPath)
    {
        try
        {
            File.WriteAllText(fullPath, string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            AppendTerminalLine($"Failed to create '{fullPath}': {ex.Message}", TerminalLineKind.Error);
            return false;
        }
    }

    private static void InsertNode(ObservableCollection<FileTreeNode> nodes, FileTreeNode newNode)
    {
        var index = 0;

        if (newNode.IsFolder)
        {
            while (index < nodes.Count && nodes[index].IsFolder &&
                   string.Compare(nodes[index].Name, newNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                index++;
            }
        }
        else
        {
            while (index < nodes.Count && nodes[index].IsFolder)
            {
                index++;
            }

            while (index < nodes.Count && string.Compare(nodes[index].Name, newNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                index++;
            }
        }

        nodes.Insert(index, newNode);
    }

    [RelayCommand]
    private void OpenFolder() => RequestOpenFolder?.Invoke();

    [RelayCommand]
    private void OpenRecentProject(RecentProject? project)
    {
        if (project?.Path is { Length: > 0 })
        {
            LoadFolder(project.Path);
        }
    }

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
        AddRecentProject(path);
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
            AppendTerminalLine($"Failed to open '{node.FullPath}': {ex.Message}", TerminalLineKind.Error);
            return;
        }

        var tab = new EditorTabViewModel { Title = node.Name, FilePath = node.FullPath };
        tab.LoadContent(text);
        WireDirtyTracking(tab, node);
        OpenTabs.Add(tab);
        SelectTab(tab);
    }

    private void WireDirtyTracking(EditorTabViewModel tab, FileTreeNode node)
    {
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorTabViewModel.IsDirty))
            {
                node.IsDirty = tab.IsDirty;
            }
        };
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
            AppendTerminalLine($"Failed to save '{path}': {ex.Message}", TerminalLineKind.Error);
        }
    }

    private bool CanSave() => SelectedTab is { IsDirty: true };

    [RelayCommand(CanExecute = nameof(CanSave))]
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

    private bool CanSaveAll() => DirtyTabCount > 0;

    [RelayCommand(CanExecute = nameof(CanSaveAll))]
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

        string command;
        string? cliPath = null;
        var filePath = SelectedTab.FilePath;
        if (string.Equals(Path.GetExtension(filePath), ".nw", StringComparison.OrdinalIgnoreCase))
        {
            cliPath = FindNetWallCliPath();
            if (cliPath is null)
            {
                AppendTerminalLine("No se encontró cli.py de NetWall.Lang.", TerminalLineKind.Error);
                return;
            }

            command = $"python \"{cliPath}\" \"{SelectedTab.FilePath}\"";
        }
        else
        {
            command = $"\"{SelectedTab.FilePath}\"";
        }

        var block = new TerminalRunBlock(SelectedTab.Title, command);
        TerminalEntries.Add(block);
        TerminalUpdated?.Invoke();

        var stopwatch = Stopwatch.StartNew();
        var result = await _terminal.RunAsync(command);
        if (!string.IsNullOrEmpty(result.Output))
        {
            block.Lines.Add(new TerminalLine(TerminalLineKind.Output, result.Output));
        }

        block.Duration = stopwatch.Elapsed;
        block.Status = result.ExitCode == 0 ? RunStatus.Succeeded : RunStatus.Failed;
        TerminalUpdated?.Invoke();

        if (cliPath is not null)
        {
            var simulation = await SimulationRunner.RunAsync(cliPath, filePath);
            Simulation.Load(simulation, Path.GetFileName(filePath));
            if (Simulation.HasUnseenResults)
            {
                AppendTerminalLine(
                    $"Simulación lista: {Simulation.Packets.Count} paquetes. Ábrela en la pestaña SIMULACIÓN.");
            }
        }
    }

    private static string? FindNetWallCliPath()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "NetWall.Lang", "src", "cli.py");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
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
        AppendTerminalLine(command, TerminalLineKind.Command);
        var result = await _terminal.RunAsync(command);
        if (!string.IsNullOrEmpty(result.Output))
        {
            AppendTerminalLine(result.Output, TerminalLineKind.Output);
        }
    }

    [RelayCommand]
    private void ClearTerminal() => TerminalEntries.Clear();

    private void AppendTerminalLine(string text, TerminalLineKind kind = TerminalLineKind.Info)
    {
        TerminalEntries.Add(new TerminalLine(kind, text));
        TerminalUpdated?.Invoke();
    }

    /// <summary>Opens a file (e.g. from a diagnostic link in the terminal) and reveals a line.</summary>
    public void OpenFileAtLine(string path, int line)
    {
        if (!File.Exists(path))
        {
            AppendTerminalLine($"Failed to open '{path}': file not found", TerminalLineKind.Error);
            return;
        }

        var node = FindNode(RootNodes, path) ?? new FileTreeNode { FullPath = path, Name = Path.GetFileName(path) };
        OpenNode(node);
        if (SelectedTab is { } tab && string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            RequestRevealLine?.Invoke(line);
        }
    }

    private static FileTreeNode? FindNode(IEnumerable<FileTreeNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (node.IsFolder && FindNode(node.Children, path) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
