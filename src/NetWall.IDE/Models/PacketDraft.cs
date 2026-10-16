using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetWall.IDE.Models;

public sealed record DeviceChoice(BuilderDevice Device)
{
    public override string ToString() =>
        $"{Device.Network?.Network?.Name ?? Device.KindLabel.ToLowerInvariant()} · {Device.Address}";
}

public sealed partial class PacketDraft(BuilderDevice source, IReadOnlyList<DeviceChoice> destinations) : ObservableObject
{
    public BuilderDevice Source { get; } = source;

    public string Title => $"Enviar paquete desde {Source.Address}";

    public IReadOnlyList<DeviceChoice> Destinations { get; } = destinations;

    public IReadOnlyList<string> Protocols { get; } = ["tcp", "udp"];

    [ObservableProperty]
    public partial DeviceChoice? Destination { get; set; }

    [ObservableProperty]
    public partial string Protocol { get; set; } = "tcp";

    [ObservableProperty]
    public partial string Port { get; set; } = "80";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; set; } = string.Empty;

    public bool HasError => Error.Length > 0;
}
