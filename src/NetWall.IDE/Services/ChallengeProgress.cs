using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NetWall.IDE.Services;

public static class ChallengeProgress
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetWall.IDE", "challenges");

    private static readonly string ProgressPath = Path.Combine(Folder, "seals.json");

    public static Dictionary<string, int> LoadSeals()
    {
        try
        {
            return File.Exists(ProgressPath)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(ProgressPath)) ?? []
                : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void SaveSeals(Dictionary<string, int> seals)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(ProgressPath, JsonSerializer.Serialize(seals));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static string CodePath(string id) => Path.Combine(Folder, id, "reto.nw");

    public static string? LoadCode(string id)
    {
        try
        {
            return File.Exists(CodePath(id)) ? File.ReadAllText(CodePath(id)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool SaveCode(string id, string code)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CodePath(id))!);
            File.WriteAllText(CodePath(id), code);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
