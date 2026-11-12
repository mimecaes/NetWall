using System;

namespace NetWall.IDE.Models;

public class RecentProject
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public DateTime OpenedAt { get; set; }

    public string FormattedDate => OpenedAt.ToString("d MMM yyyy");

    public string ShortPath =>
        System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) is { Length: > 0 } parent
            ? $"{parent} › {Name}"
            : Path;

    public string RelativeDate => (DateTime.Today - OpenedAt.Date).Days switch
    {
        <= 0 => "hoy",
        1 => "ayer",
        < 7 and var days => $"hace {days} días",
        < 14 => "hace 1 semana",
        < 30 and var days => $"hace {days / 7} semanas",
        _ => FormattedDate,
    };
}
