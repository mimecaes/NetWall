using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public enum RuleStepState
{
    Pending,
    Evaluating,
    Skipped,
    Allowed,
    Blocked,
}

public partial class RuleStep : ObservableObject
{
    public RuleStep(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEvaluating), nameof(IsSkipped), nameof(IsAllowed), nameof(IsBlocked))]
    public partial RuleStepState State { get; set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    public bool IsEvaluating => State == RuleStepState.Evaluating;

    public bool IsSkipped => State == RuleStepState.Skipped;

    public bool IsAllowed => State == RuleStepState.Allowed;

    public bool IsBlocked => State == RuleStepState.Blocked;
}

public sealed partial class SimulationEntry(
    int number, SimulatedPacket packet, string header, string result, string explanation) : ObservableObject
{
    public int Number { get; } = number;

    public SimulatedPacket Packet { get; } = packet;

    public string Header { get; } = header;

    public string Result { get; } = result;

    public string Explanation { get; } = explanation;

    public bool IsAllowed => Packet.IsAllowed;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed partial class RuleToggle(string name, string summary, bool allows, bool isPolicy) : ObservableObject
{
    public string Name { get; } = name;

    public string Summary { get; } = summary;

    public bool Allows { get; } = allows;

    public bool Blocks => !Allows;

    public bool IsPolicy { get; } = isPolicy;

    public bool IsRule => !IsPolicy;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool IsDecisive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHits), nameof(HitsText))]
    public partial int Hits { get; set; }

    public bool HasHits => Hits > 0;

    public string HitsText => $"×{Hits}";
}

public partial class SimulationViewModel : ViewModelBase
{
    private const string NoFields = "—";

    private const string InternetPrefix = "203.0.113.";

    private bool _updatingCatalog;
    private int _nextDeviceId;

    public SimulationResult? Result { get; private set; }

    public IReadOnlyList<SimulatedPacket> Packets => Result?.Packets ?? [];

    public ObservableCollection<SimulationEntry> Entries { get; } = [];

    public ObservableCollection<RuleStep> Steps { get; } = [];

    public ObservableCollection<RuleToggle> Catalog { get; } = [];

    public ObservableCollection<BuilderDevice> Devices { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetTip))]
    public partial bool IsBuilder { get; set; }

    public string ResetTip => IsBuilder ? "Repetir los paquetes enviados con las reglas activas" : "Reiniciar";

    public List<SimulatedPacket> SentPackets { get; } = [];

    public event Action<SimulatedPacket>? PacketRequested;

    public event Action? ReplayRequested;

    public event Action? PacketDelivered;

    public event Action? Replayed;

    private readonly Dictionary<SimulatedPacket, IReadOnlyList<SimulationRule>> _packetRules =
        new(ReferenceEqualityComparer.Instance);

    private string _lastProtocol = "tcp";
    private string _lastPort = "80";

    public PacketDraft NewDraft(BuilderDevice source, BuilderDevice? destination)
    {
        var choices = Devices.Where(device => device != source).Select(device => new DeviceChoice(device)).ToList();
        return new PacketDraft(source, choices)
        {
            Destination = choices.FirstOrDefault(choice => choice.Device == destination),
            Protocol = _lastProtocol,
            Port = _lastPort,
        };
    }

    public bool Send(PacketDraft draft)
    {
        draft.Error = PacketError(draft);
        if (draft.HasError)
        {
            return false;
        }

        _lastProtocol = draft.Protocol;
        _lastPort = draft.Port;
        var request = new SimulatedPacket(draft.Protocol, int.Parse(draft.Port), draft.Source.Address,
            draft.Destination!.Device.Address, 0, string.Empty, null, []);
        SentPackets.Add(request);
        PacketRequested?.Invoke(request);
        return true;
    }

    public void RequestReplay()
    {
        if (SentPackets.Count > 0)
        {
            ReplayRequested?.Invoke();
        }
    }

    public void Deliver(SimulationResult? result)
    {
        if (result is not { Errors: false, Packets: [var packet] } || Result?.Packets is not { } packets)
        {
            return;
        }

        _packetRules[packet] = result.Rules ?? [];
        packets.Add(packet);
        OnCountsChanged();
        PacketDelivered?.Invoke();
    }

    public void Replay(SimulationResult? result)
    {
        if (result is not { Errors: false, Packets: { } replayed } || Result is null)
        {
            return;
        }

        Result.Packets?.Clear();
        Result.Packets?.AddRange(replayed);
        Reset();
        foreach (var packet in replayed)
        {
            _packetRules[packet] = result.Rules ?? [];
        }

        Replayed?.Invoke();
    }

    private string PacketError(PacketDraft draft)
    {
        if (draft.Destination?.Device is not { } destination)
        {
            return "Elige a qué equipo enviarlo.";
        }

        if (!int.TryParse(draft.Port, out var port) || port is < 0 or > 65535)
        {
            return "El puerto debe ser un número entre 0 y 65535.";
        }

        foreach (var device in new[] { draft.Source, destination })
        {
            if (device.HasError)
            {
                return $"Corrige la dirección de {device.Address} antes de enviar.";
            }

            if (!device.IsConnected)
            {
                return $"Conecta {device.Address} a NetWall desde su puerto.";
            }
        }

        return string.Empty;
    }

    private void UseRules(IReadOnlyList<SimulationRule> rules)
    {
        Steps.Clear();
        foreach (var rule in rules)
        {
            Steps.Add(new RuleStep(rule.Name));
        }

        Steps.Add(new RuleStep("politica_defecto"));
    }

    public IReadOnlyList<SimulatedPacket> ProgramPackets { get; private set; } = [];

    public IEnumerable<string> ActiveRules => Catalog.Where(rule => rule is { IsRule: true, IsActive: true })
        .Select(rule => rule.Name);

    public string ActiveRulesText => $"{ActiveRules.Count()} de {Catalog.Count(rule => rule.IsRule)} activas";

    public bool IsRerun { get; private set; }

    public event Action? Loaded;

    public event Action? RulesChanged;

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; } = "Ejecuta un archivo con simular para ver la simulación.";

    public bool HasMessage => Message.Length > 0;

    [ObservableProperty]
    public partial string PacketFields { get; set; } = NoFields;

    [ObservableProperty]
    public partial SimulationEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsAnimating { get; set; }

    [ObservableProperty]
    public partial bool IsHelpyEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string ModeTitle { get; set; } = string.Empty;

    public string HelpyGreeting
    {
        get
        {
            if (IsBuilder)
            {
                return "Arrastra equipos desde la paleta y conéctalos a NetWall desde su puerto.";
            }

            if (!IsRerun)
            {
                return $"¡Hola! Voy a vigilar {Packets.Count} {(Packets.Count == 1 ? "paquete" : "paquetes")}.";
            }

            var active = ActiveRules.Count();
            return $"Probemos otra vez con {active} {(active == 1 ? "regla activa" : "reglas activas")}.";
        }
    }

    public string HelpySummary
    {
        get
        {
            var allowed = Entries.Count(entry => entry.IsAllowed);
            return $"¡Listo! {allowed} permitidos y {Entries.Count - allowed} bloqueados.";
        }
    }

    public string HelpyArrival(SimulatedPacket packet) =>
        $"Llega un {packet.Protocol} al puerto {packet.Port} desde {HostLabel(packet.Source)}. Lo reviso...";

    public string HelpyVerdict(SimulatedPacket packet) => packet.Rule is null
        ? $"Ninguna regla coincide, así que aplico la política: {packet.Action}."
        : packet.IsAllowed
            ? $"La regla {packet.Rule} lo permite. ¡Adelante, {HostLabel(packet.Destination)} te espera!"
            : $"La regla {packet.Rule} lo bloquea. ¡Por aquí no pasas!";

    public int NextIndex { get; private set; }

    public string AllowedText => $"✓ {Entries.Count(entry => entry.IsAllowed)}";

    public string BlockedText => $"✕ {Entries.Count(entry => !entry.IsAllowed)}";

    public string ProgressText => $"{Entries.Count}/{Packets.Count}";

    public SimulatedPacket? NextPacket => NextIndex < Packets.Count ? Packets[NextIndex] : null;

    public void LoadBuilder(SimulationResult? result, string fileName)
    {
        IsRerun = false;
        IsBuilder = true;
        var valid = result is { Errors: false } ? result with { Packets = [] } : null;
        BuildCatalog(valid);
        ProgramPackets = [];
        ClearDevices();
        FileName = fileName;
        Result = valid;
        Message = result is null ? "No se pudo ejecutar la simulación."
            : result.Errors ? "Corrige los errores del archivo para simular."
            : string.Empty;
        Reset();
        Loaded?.Invoke();
    }

    public BuilderDevice AddDevice(DeviceKind kind, double x, double y)
    {
        var device = new BuilderDevice(++_nextDeviceId, kind, NetworkOptions(kind)) { X = x, Y = y };
        device.PropertyChanged += OnDevicePropertyChanged;
        Devices.Add(device);
        device.Network = device.Options[0];
        return device;
    }

    public void RemoveDevice(BuilderDevice device)
    {
        device.PropertyChanged -= OnDevicePropertyChanged;
        Devices.Remove(device);
        ValidateDevices();
    }

    private void ClearDevices()
    {
        SentPackets.Clear();
        _packetRules.Clear();
        foreach (var device in Devices)
        {
            device.PropertyChanged -= OnDevicePropertyChanged;
        }

        Devices.Clear();
    }

    private IReadOnlyList<NetworkOption> NetworkOptions(DeviceKind kind)
    {
        var networks = Result?.Networks ?? [];
        var zones = networks.Where(network => Ipv4.Parse(network.Cidr) is { Mask: < 32 })
            .Select(network => new NetworkOption($"{network.Name} · {network.Cidr}", network));
        var hosts = networks.Where(network => Ipv4.IsHost(network.Cidr))
            .Select(network => new NetworkOption($"{network.Name} · {network.Cidr}", network));
        NetworkOption[] internet = [new("internet · fuera de las redes", null)];
        return kind switch
        {
            DeviceKind.Computer => [.. zones, .. internet],
            DeviceKind.Server => [.. hosts, .. zones, .. internet],
            _ => internet,
        };
    }

    private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not BuilderDevice device)
        {
            return;
        }

        if (e.PropertyName == nameof(BuilderDevice.Network))
        {
            device.Address = FreeAddress(device);
        }
        else if (e.PropertyName == nameof(BuilderDevice.Address))
        {
            ValidateDevices();
        }
    }

    private string FreeAddress(BuilderDevice device)
    {
        var used = Devices.Where(other => other != device).Select(other => other.Address).ToHashSet();
        if (device.Network?.Network is not { } network || Ipv4.Parse(network.Cidr) is not { } parsed)
        {
            return Enumerable.Range(10, 240).Select(i => InternetPrefix + i).FirstOrDefault(address => !used.Contains(address))
                   ?? InternetPrefix + "10";
        }

        if (parsed.Mask == 32)
        {
            return Ipv4.Format(parsed.Base);
        }

        var size = 1L << (32 - parsed.Mask);
        var first = parsed.Base >> (32 - parsed.Mask) << (32 - parsed.Mask);
        var offsets = Enumerable.Range(10, (int)Math.Max(0, Math.Min(size - 11, 500)))
            .Concat(Enumerable.Range(1, (int)Math.Min(9, Math.Max(0, size - 2))));
        return offsets.Select(offset => Ipv4.Format((uint)(first + offset))).FirstOrDefault(address => !used.Contains(address))
               ?? Ipv4.Format(first);
    }

    private void ValidateDevices()
    {
        foreach (var device in Devices)
        {
            var zone = device.Network?.Network;
            var parsedZone = zone is null ? null : Ipv4.Parse(zone.Cidr);
            device.Error = Ipv4.Parse(device.Address) is not { Mask: 32 } host
                ? "Escribe una dirección IPv4, por ejemplo 192.168.1.10."
                : parsedZone is { Mask: < 32 } inside && !Ipv4.Contains(inside, host.Base)
                    ? $"Está fuera de {zone!.Name} ({zone.Cidr})."
                    : Devices.Count(other => other.Address == device.Address) > 1
                        ? "Otro equipo ya usa esta dirección."
                        : string.Empty;
        }
    }

    public void Load(SimulationResult? result, string fileName)
    {
        IsRerun = false;
        IsBuilder = false;
        ClearDevices();
        BuildCatalog(result is { Errors: false } ? result : null);
        ProgramPackets = result?.Packets ?? [];
        Show(result, fileName);
    }

    public void Reload(SimulationResult? result)
    {
        IsRerun = true;
        Show(result, FileName);
    }

    public void SetAllRules(bool active)
    {
        _updatingCatalog = true;
        foreach (var rule in Catalog.Where(rule => rule.IsRule))
        {
            rule.IsActive = active;
        }

        _updatingCatalog = false;
        OnRulesChanged();
    }

    public async void MarkDecisive(SimulatedPacket packet)
    {
        var rule = Catalog.FirstOrDefault(rule => packet.Rule is null ? rule.IsPolicy : rule.Name == packet.Rule);
        if (rule is null)
        {
            return;
        }

        rule.Hits++;
        rule.IsDecisive = true;
        await Task.Delay(1300);
        rule.IsDecisive = false;
    }

    private void BuildCatalog(SimulationResult? result)
    {
        foreach (var rule in Catalog)
        {
            rule.PropertyChanged -= OnRulePropertyChanged;
        }

        Catalog.Clear();
        if (result is null)
        {
            OnPropertyChanged(nameof(ActiveRulesText));
            return;
        }

        Result = result;
        foreach (var rule in result.Rules ?? [])
        {
            var toggle = new RuleToggle(rule.Name, RuleSummary(rule), rule.Action == "permitir", isPolicy: false);
            toggle.PropertyChanged += OnRulePropertyChanged;
            Catalog.Add(toggle);
        }

        Catalog.Add(new RuleToggle("politica_defecto",
            $"{result.DefaultPolicy} · si ninguna regla coincide", result.DefaultPolicy == "permitir", isPolicy: true));
        OnPropertyChanged(nameof(ActiveRulesText));
    }

    private void OnRulePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RuleToggle.IsActive) && !_updatingCatalog)
        {
            OnRulesChanged();
        }
    }

    private void OnRulesChanged()
    {
        OnPropertyChanged(nameof(ActiveRulesText));
        RulesChanged?.Invoke();
    }

    private string RuleSummary(SimulationRule rule)
    {
        var parts = new List<string> { rule.Action ?? string.Empty, rule.Protocol ?? "todos los protocolos" };
        if (rule.Port is not null)
        {
            parts.Add($"puerto {rule.Port}");
        }

        if (rule.Source is not null && rule.Source != "cualquiera")
        {
            parts.Add($"desde {HostLabel(rule.Source)}");
        }

        if (rule.Destination is not null && rule.Destination != "cualquiera")
        {
            parts.Add($"hacia {HostLabel(rule.Destination)}");
        }

        return string.Join(" · ", parts);
    }

    private void Show(SimulationResult? result, string fileName)
    {
        FileName = fileName;
        Result = result is { Errors: false } ? result : null;
        Message = result is null ? "No se pudo ejecutar la simulación."
            : result.Errors ? "Corrige los errores del archivo para simular."
            : Packets.Count == 0 ? "El archivo no tiene paquetes para simular."
            : string.Empty;
        Reset();
        Loaded?.Invoke();
    }

    public void Unload()
    {
        IsBuilder = false;
        ClearDevices();
        BuildCatalog(null);
        ProgramPackets = [];
        Result = null;
        Message = string.Empty;
        Reset();
        Loaded?.Invoke();
    }

    public void Reset()
    {
        foreach (var rule in Catalog)
        {
            rule.Hits = 0;
            rule.IsDecisive = false;
        }

        NextIndex = 0;
        Entries.Clear();
        OnCountsChanged();
        SelectedEntry = null;
        PacketFields = NoFields;
        Steps.Clear();
        if (Result is not null)
        {
            UseRules(Result.Rules ?? []);
        }
    }

    public void ShowPacket(SimulatedPacket packet)
    {
        if (_packetRules.TryGetValue(packet, out var rules))
        {
            UseRules(rules);
        }

        PacketFields = $"{packet.Protocol} {packet.Port} · {packet.Source} → {packet.Destination}";
        foreach (var step in Steps)
        {
            step.State = RuleStepState.Pending;
            step.Reason = string.Empty;
        }
    }

    public async Task EvaluateAsync(SimulatedPacket packet, int millisecondsPerRule, CancellationToken token)
    {
        ShowPacket(packet);
        var decisive = DecisiveIndex(packet);
        for (var i = 0; i <= decisive; i++)
        {
            Steps[i].State = RuleStepState.Evaluating;
            await Task.Delay(millisecondsPerRule, token);
            Resolve(packet, i, decisive);
        }
    }

    public void AddEntry(SimulatedPacket packet)
    {
        var entry = new SimulationEntry(NextIndex + 1, packet,
            $"{packet.Protocol} {packet.Port} · {packet.Source} → {packet.Destination}",
            $"{(packet.IsAllowed ? "✓ Permitido" : "✕ Bloqueado")} {DecidedBy(packet)}",
            Explain(packet));
        NextIndex++;
        Entries.Add(entry);
        OnCountsChanged();
        SelectedEntry = entry;
    }

    partial void OnSelectedEntryChanged(SimulationEntry? oldValue, SimulationEntry? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }
    }

    partial void OnSelectedEntryChanged(SimulationEntry? value)
    {
        if (value is null)
        {
            return;
        }

        ShowPacket(value.Packet);
        var decisive = DecisiveIndex(value.Packet);
        for (var i = 0; i <= decisive; i++)
        {
            Resolve(value.Packet, i, decisive);
        }
    }

    private void OnCountsChanged()
    {
        OnPropertyChanged(nameof(AllowedText));
        OnPropertyChanged(nameof(BlockedText));
        OnPropertyChanged(nameof(ProgressText));
    }

    private int DecisiveIndex(SimulatedPacket packet) =>
        packet.Rule is null ? Steps.Count - 1 : Steps.ToList().FindIndex(step => step.Name == packet.Rule);

    private void Resolve(SimulatedPacket packet, int index, int decisive)
    {
        var step = Steps[index];
        if (index < decisive)
        {
            step.State = RuleStepState.Skipped;
            step.Reason = packet.Skipped.FirstOrDefault(skipped => skipped.Rule == step.Name)?.Reason ?? string.Empty;
            return;
        }

        step.State = packet.IsAllowed ? RuleStepState.Allowed : RuleStepState.Blocked;
        step.Reason = packet.Rule is not null ? $"coincide → {packet.Action}"
            : packet.Skipped.Count == 0 ? $"sin reglas → {packet.Action}"
            : $"ninguna coincide → {packet.Action}";
    }

    private static string DecidedBy(SimulatedPacket packet) =>
        packet.Rule is null ? "por la política por defecto" : $"por la regla {packet.Rule}";

    private string Explain(SimulatedPacket packet)
    {
        if (packet.Rule is null)
        {
            return ExplainPolicy(packet);
        }

        var rule = Result!.Rules!.First(rule => rule.Name == packet.Rule);
        var clauses = new List<string>();
        if (rule.Protocol is not null)
        {
            clauses.Add($"es {packet.Protocol}");
        }

        if (rule.Port is not null)
        {
            clauses.Add(rule.Port.Contains('-')
                ? $"usa el puerto {packet.Port}, dentro del rango {rule.Port}"
                : $"usa el puerto {packet.Port}");
        }

        AddAddressClause(clauses, rule.Source, "viene de");
        AddAddressClause(clauses, rule.Destination, "va hacia");

        var text = clauses.Count == 0
            ? $"La regla {rule.Name} no tiene condiciones, así que acepta cualquier paquete."
            : $"El paquete {Join(clauses)}.";
        if (packet.Skipped.Count > 0)
        {
            var skipped = packet.Skipped[^1];
            text += $" Antes, la regla {skipped.Rule} no coincidió ({skipped.Reason}).";
        }

        return text;
    }

    private string ExplainPolicy(SimulatedPacket packet)
    {
        var policy = Result!.PolicyDefined
            ? $"se aplicó politica_defecto = {packet.Action}"
            : $"se aplicó la política por defecto, que es {packet.Action} porque no está definida";
        if (packet.Skipped.Count == 0)
        {
            return $"No hay reglas, así que {policy}.";
        }

        if (packet.Skipped.All(skipped => skipped.Reason.StartsWith("protocolo", StringComparison.Ordinal)))
        {
            return $"Ninguna regla acepta el protocolo {packet.Protocol}, así que {policy}.";
        }

        return packet.Skipped.Count == 1
            ? $"La única regla, {packet.Skipped[0].Rule}, no coincidió ({packet.Skipped[0].Reason}), así que {policy}."
            : $"Ninguna de las {packet.Skipped.Count} reglas coincidió, así que {policy}.";
    }

    private void AddAddressClause(List<string> clauses, string? cidr, string verb)
    {
        if (cidr is not null && cidr != "cualquiera")
        {
            clauses.Add($"{verb} {NetworkLabel(cidr)}");
        }
    }

    private string HostLabel(string address) =>
        Result?.Networks?.FirstOrDefault(network => network.Cidr == address)?.Name ?? address;

    private string NetworkLabel(string cidr) =>
        Result?.Networks?.FirstOrDefault(network => network.Cidr == cidr) is { } network
            ? $"{network.Name} ({cidr})"
            : cidr;

    private static string Join(List<string> clauses) =>
        clauses.Count == 1 ? clauses[0] : $"{string.Join(", ", clauses[..^1])} y {clauses[^1]}";
}
