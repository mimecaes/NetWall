using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public class RecentProjectsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetWall.IDE"
    );

    private static readonly string RecentProjectsPath = Path.Combine(ConfigDir, "recent-projects.json");
    private const int MaxRecentProjects = 15;

    public RecentProjectsService()
    {
        Directory.CreateDirectory(ConfigDir);
    }

    public List<RecentProject> LoadRecent()
    {
        try
        {
            if (!File.Exists(RecentProjectsPath))
            {
                return [];
            }

            var json = File.ReadAllText(RecentProjectsPath);
            var projects = JsonSerializer.Deserialize<List<RecentProject>>(json, JsonOptions) ?? [];
            return projects.OrderByDescending(p => p.OpenedAt).ToList();
        }
        catch
        {
            return [];
        }
    }

    public void AddOrUpdateRecent(string folderPath)
    {
        try
        {
            var projects = LoadRecent();
            var existing = projects.FirstOrDefault(p => p.Path == folderPath);

            if (existing is not null)
            {
                projects.Remove(existing);
            }

            projects.Insert(0, new RecentProject
            {
                Name = Path.GetFileName(folderPath) ?? folderPath,
                Path = folderPath,
                OpenedAt = DateTime.Now
            });

            projects = projects.Take(MaxRecentProjects).ToList();

            var json = JsonSerializer.Serialize(projects, JsonOptions);
            File.WriteAllText(RecentProjectsPath, json);
        }
        catch
        {
        }
    }

    public void RemoveRecent(string folderPath)
    {
        try
        {
            var projects = LoadRecent().Where(p => p.Path != folderPath).ToList();
            File.WriteAllText(RecentProjectsPath, JsonSerializer.Serialize(projects, JsonOptions));
        }
        catch
        {
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
