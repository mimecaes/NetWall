using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public enum DeviceKind
{
    Computer,
    Server,
    Internet,
}

public sealed record NetworkOption(string Label, SimulationNetwork? Network)
{
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

    [ObservableProperty]
    public partial NetworkOption? Network { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectText))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; set; } = string.Empty;

    public bool HasError => Error.Length > 0;

    public string ConnectText => IsConnected ? "Desconectar" : "Conectar a NetWall";
}
