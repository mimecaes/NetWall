using System;
using Avalonia.Data;

namespace NetWall.IDE.Localization;

public sealed class LocExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public object ProvideValue(IServiceProvider serviceProvider) => new Binding
    {
        Path = nameof(LocalizationService.Language),
        Source = LocalizationService.Instance,
        Converter = LocConverter.Instance,
        ConverterParameter = Key,
    };
}
