using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public enum TerminalLineKind
{
    /// <summary>Output of a process; may contain ANSI colors and links.</summary>
    Output,

    /// <summary>A command the user typed ("$ ...").</summary>
    Command,

    /// <summary>A message from the IDE itself.</summary>
    Info,

    /// <summary>An error reported by the IDE itself.</summary>
    Error,
}

public abstract class TerminalEntry : ObservableObject;

public sealed class TerminalLine(TerminalLineKind kind, string text) : TerminalEntry
{
    public TerminalLineKind Kind { get; } = kind;

    public string Text { get; } = text;
}

public enum RunStatus
{
    Running,
    Succeeded,
    Failed,
}

/// <summary>
/// One run of the active file: a short header plus the program output, marked with a thin
/// bar whose color follows the result.
/// </summary>
public sealed partial class TerminalRunBlock(string fileName, string command) : TerminalEntry
{
    public string FileName { get; } = fileName;

    public string Command { get; } = command;

    public DateTime StartedAt { get; } = DateTime.Now;

    public ObservableCollection<TerminalLine> Lines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsSucceeded), nameof(IsFailed), nameof(Header))]
    public partial RunStatus Status { get; set; } = RunStatus.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    public partial TimeSpan? Duration { get; set; }

    public bool IsRunning => Status == RunStatus.Running;

    public bool IsSucceeded => Status == RunStatus.Succeeded;

    public bool IsFailed => Status == RunStatus.Failed;

    public string Header => Duration is { } d
        ? $"{FileName} · {StartedAt:HH:mm:ss} · {d.TotalSeconds:0.00} s"
        : $"{FileName} · {StartedAt:HH:mm:ss} · ejecutando…";
}
