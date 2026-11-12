using System.Collections.Generic;

namespace NetWall.IDE.Models;

public sealed record SimulationNetwork(string Name, string Cidr);

public sealed record SimulationRule(string Name, string? Action, string? Protocol, string? Port, string? Source,
    string? Destination);

public sealed record SkippedRule(string Rule, string Reason);

public sealed record SimulatedPacket(string Protocol, int Port, string Source, string Destination, int Line,
    string Action, string? Rule, List<SkippedRule> Skipped)
{
    public bool IsAllowed => Action == "permitir";
}

public sealed record SimulationResult(bool Errors, string DefaultPolicy, bool PolicyDefined,
    List<SimulationNetwork>? Networks, List<SimulationRule>? Rules, List<SimulatedPacket>? Packets);
