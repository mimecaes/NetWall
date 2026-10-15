using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NetWall.IDE.Models;

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

public partial class SimulationViewModel : ViewModelBase
{
    private const string NoFields = "—";

    public SimulationResult? Result { get; private set; }

    public IReadOnlyList<SimulatedPacket> Packets => Result?.Packets ?? [];

    public ObservableCollection<SimulationEntry> Entries { get; } = [];

    public ObservableCollection<RuleStep> Steps { get; } = [];

    public event Action? Loaded;

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

    public string HelpyGreeting =>
        $"¡Hola! Voy a vigilar {Packets.Count} {(Packets.Count == 1 ? "paquete" : "paquetes")}.";

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

    public void Load(SimulationResult? result, string fileName)
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
        Result = null;
        Message = string.Empty;
        Reset();
        Loaded?.Invoke();
    }

    public void Reset()
    {
        NextIndex = 0;
        Entries.Clear();
        OnCountsChanged();
        SelectedEntry = null;
        PacketFields = NoFields;
        Steps.Clear();
        foreach (var rule in Result?.Rules ?? [])
        {
            Steps.Add(new RuleStep(rule.Name));
        }

        if (Result is not null)
        {
            Steps.Add(new RuleStep("politica_defecto"));
        }
    }

    public void ShowPacket(SimulatedPacket packet)
    {
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
