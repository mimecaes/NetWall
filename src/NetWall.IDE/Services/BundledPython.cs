using System;
using System.IO;

namespace NetWall.IDE.Services;

public static class BundledPython
{
    public static string? Directory { get; } = Find();

    public static string Executable => Directory is { } directory ? Path.Combine(directory, "python.exe") : "python";

    private static string? Find()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "python");
        return File.Exists(Path.Combine(directory, "python.exe")) ? directory : null;
    }
}
