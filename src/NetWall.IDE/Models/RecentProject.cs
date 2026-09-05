using System;

namespace NetWall.IDE.Models;

public class RecentProject
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public DateTime OpenedAt { get; set; }

    public string FormattedDate => OpenedAt.ToString("d MMM yyyy");
}
