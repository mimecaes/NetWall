using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Platform;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public static class ChallengeCatalog
{
    private static readonly Uri Source = new("avares://NetWall.IDE/Assets/Challenges/challenges.json");

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ChallengeModule> Load()
    {
        using var stream = AssetLoader.Open(Source);
        return JsonSerializer.Deserialize<ChallengeCatalogData>(stream, Options)?.Modules ?? [];
    }
}
