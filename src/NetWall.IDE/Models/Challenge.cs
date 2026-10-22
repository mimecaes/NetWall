using System.Collections.Generic;
using System.Text.Json;

namespace NetWall.IDE.Models;

public sealed record ChallengeCatalogData(List<ChallengeModule> Modules);

public sealed record ChallengeModule(string Id, string Name, string Brush, List<Challenge> Challenges);

public sealed record Challenge(string Id, string Title, string Statement, string Hint, string Starter, int? MaxRules,
    List<string> Concepts, List<ChallengeTest> Tests);

public sealed record ChallengeTest(string Kind, string Label, bool Visible, string? Name, string? Type, string? Value,
    string? Protocol, int Port, string? Source, string? Destination, string? Expect, string? Failure);

public sealed record ChallengeDiagnostic(string Type, int Line, string Message, string File);

public sealed record ChallengeDeclaration(string Name, string Type, JsonElement Value)
{
    public string Text => Value.ValueKind == JsonValueKind.String ? Value.GetString() ?? string.Empty : Value.GetRawText();
}

public sealed record ChallengeResult(bool Errors, List<ChallengeDiagnostic> Diagnostics,
    List<ChallengeDeclaration>? Declarations, string? DefaultPolicy, bool PolicyDefined, List<SimulationRule>? Rules,
    List<SimulatedPacket>? Packets);
