using System.Collections.Generic;

namespace NetWall.IDE.Models;

public sealed record ClassFile(int Version, string Id, string Name, string? Description, List<ClassExercise> Exercises);

public sealed record ClassExercise(string Id, string Title, string Statement, string? Starter, string? Type,
    List<ChallengeTest> Tests, string? Solution = null);
