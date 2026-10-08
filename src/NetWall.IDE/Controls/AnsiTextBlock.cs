using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.Controls;

public sealed class TerminalLinkEventArgs(RoutedEvent routedEvent, Uri uri) : RoutedEventArgs(routedEvent)
{
    public Uri Uri { get; } = uri;
}

/// <summary>
/// Renders a <see cref="TerminalLine"/>. Output lines are parsed as ANSI and colored with the
/// theme's terminal palette (the Ansi0Brush..Ansi15Brush resources), so they follow theme changes.
/// </summary>
public sealed class AnsiTextBlock : TextBlock
{
    public static readonly StyledProperty<TerminalLine?> LineProperty =
        AvaloniaProperty.Register<AnsiTextBlock, TerminalLine?>(nameof(Line));

    public static readonly RoutedEvent<TerminalLinkEventArgs> LinkClickedEvent =
        RoutedEvent.Register<AnsiTextBlock, TerminalLinkEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public TerminalLine? Line
    {
        get => GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    public event EventHandler<TerminalLinkEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        if (Line is { } line)
        {
            switch (line.Kind)
            {
                case TerminalLineKind.Command:
                    inlines.Add(ThemedRun("$ ", "RunGreenBrush"));
                    inlines.Add(ThemedRun(line.Text, "TextMutedBrush"));
                    break;
                case TerminalLineKind.Info:
                    inlines.Add(ThemedRun(line.Text, "TextMutedBrush"));
                    break;
                case TerminalLineKind.Error:
                    inlines.Add(ThemedRun(line.Text, "StopRedBrush"));
                    break;
                default:
                    foreach (var segment in AnsiParser.Parse(line.Text))
                    {
                        inlines.Add(SegmentInline(segment));
                    }

                    break;
            }
        }

        Inlines = inlines;
    }

    private Inline SegmentInline(AnsiSegment segment)
    {
        var brushKey = segment.Color is { } color ? $"Ansi{color}Brush"
            : segment.Dim ? "TextMutedBrush"
            : null;

        if (segment.Link is null || !Uri.TryCreate(segment.Link, UriKind.Absolute, out var uri))
        {
            return brushKey is null ? new Run(segment.Text) : ThemedRun(segment.Text, brushKey);
        }

        var link = new TextBlock { Text = segment.Text, Cursor = new Cursor(StandardCursorType.Hand) };
        link.Classes.Add("terminal-link");
        if (brushKey is not null)
        {
            link.Bind(ForegroundProperty, this.GetResourceObservable(brushKey));
        }

        link.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(link).Properties.IsLeftButtonPressed)
            {
                RaiseEvent(new TerminalLinkEventArgs(LinkClickedEvent, uri));
                e.Handled = true;
            }
        };

        return new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom };
    }

    private Run ThemedRun(string text, string brushKey)
    {
        var run = new Run(text);
        run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(brushKey));
        return run;
    }
}
