using System;
using Avalonia;
using Avalonia.Data;
using AvaloniaEdit;

namespace NetWall.IDE.Controls;

public sealed class CodeBox : TextEditor
{
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<CodeBox, string>(nameof(Code), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    private bool _syncing;

    public CodeBox()
    {
        TextArea.TextView.LineTransformers.Add(new NetWallSyntaxColorizer { IsEnabled = true });
        TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetCurrentValue(CodeProperty, Text);
            }
        };
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty && Text != Code)
        {
            _syncing = true;
            Text = Code;
            _syncing = false;
        }
    }
}
