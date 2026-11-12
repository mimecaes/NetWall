using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public enum DeviceKind
{
    Computer,
    Server,
    Internet,
}

public sealed record NetworkOption(string Label, SimulationNetwork? Network, IBrush? Brush)
{
    public string Name => Network?.Name ?? "internet";

    public string Detail => Network?.Cidr ?? "fuera de las redes";

    public override string ToString() => Label;
}

public sealed partial class BuilderDevice(int id, DeviceKind kind, IReadOnlyList<NetworkOption> options) : ObservableObject
{
    public int Id { get; } = id;

    public DeviceKind Kind { get; } = kind;

    public IReadOnlyList<NetworkOption> Options { get; } = options;

    public string KindLabel => Kind switch
    {
        DeviceKind.Computer => "Computadora",
        DeviceKind.Server => "Servidor",
        _ => "Internet",
    };

    public string Question => Kind switch
    {
        DeviceKind.Computer => "¿A qué red pertenece esta computadora?",
        DeviceKind.Server => "¿Qué servidor es? Elige su red o una variable del archivo.",
        _ => "Este equipo vive en internet. ¿Qué dirección le damos?",
    };

    public string NetworkTitle => Kind switch
    {
        DeviceKind.Computer => "Red de la computadora",
        DeviceKind.Server => "Red del servidor",
        _ => "Red",
    };

    public string HelpyMessage => HasError ? Error
        : Network?.Network is { } network ? $"Vive en {network.Name} con la dirección {Address}. ¡Todo en orden!"
        : $"Está en internet con la dirección {Address}. ¡Todo en orden!";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HelpyMessage))]
    public partial NetworkOption? Network { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HelpyMessage))]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectText), nameof(ConnectHint))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(HelpyMessage))]
    public partial string Error { get; set; } = string.Empty;

    public bool HasError => Error.Length > 0;

    public string ConnectText => IsConnected ? "Desconectar" : "Conectar a NetWall";

    public string ConnectHint => IsConnected ? "cortar su cable con la pared" : "o arrastra desde su puerto";
}
