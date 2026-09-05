using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NetWall.IDE.Services;

public sealed class TerminalSession
{
    private readonly string _rootPath;

    public TerminalSession(string rootPath)
    {
        _rootPath = rootPath;
        CurrentDirectory = rootPath;
    }

    public string CurrentDirectory { get; private set; }

    public async Task<string> RunAsync(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed == "cd" || trimmed.StartsWith("cd ", StringComparison.Ordinal))
        {
            var target = trimmed.Length > 2 ? trimmed[2..].Trim() : _rootPath;
            var resolved = Path.GetFullPath(Path.Combine(CurrentDirectory, target));
            if (Directory.Exists(resolved))
            {
                CurrentDirectory = resolved;
                return string.Empty;
            }

            return $"cd: no such file or directory: {target}";
        }

        var isWindows = OperatingSystem.IsWindows();
        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            WorkingDirectory = CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(isWindows ? "/c" : "-lc");
        startInfo.ArgumentList.Add(commandLine);

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return $"Failed to start command: {ex.Message}";
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return output.ToString().TrimEnd('\n', '\r');
    }
}
