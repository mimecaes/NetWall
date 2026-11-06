using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NetWall.IDE.Services;

public sealed record TerminalResult(string Output, int ExitCode);

public sealed class TerminalSession
{
    private readonly string _rootPath;

    public TerminalSession(string rootPath)
    {
        _rootPath = rootPath;
        CurrentDirectory = rootPath;
    }

    public string CurrentDirectory { get; private set; }

    public async Task<TerminalResult> RunAsync(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (trimmed.Length == 0)
        {
            return new TerminalResult(string.Empty, 0);
        }

        if (trimmed == "cd" || trimmed.StartsWith("cd ", StringComparison.Ordinal))
        {
            var target = trimmed.Length > 2 ? trimmed[2..].Trim() : _rootPath;
            var resolved = Path.GetFullPath(Path.Combine(CurrentDirectory, target));
            if (Directory.Exists(resolved))
            {
                CurrentDirectory = resolved;
                return new TerminalResult(string.Empty, 0);
            }

            return new TerminalResult($"cd: no such file or directory: {target}", 1);
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // The output panel renders ANSI colors, so ask tools for them, and use UTF-8 end to end.
        startInfo.Environment.Remove("NO_COLOR");
        startInfo.Environment["FORCE_COLOR"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        if (isWindows)
        {
            startInfo.Arguments = $"/c \"chcp 65001 >nul & {commandLine}\"";
        }
        else
        {
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add(commandLine);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        
        void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (output)
            {
                output.AppendLine(line);
            }
        }

        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new TerminalResult($"Failed to start command: {ex.Message}", -1);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return new TerminalResult(output.ToString().TrimEnd('\n', '\r'), process.ExitCode);
    }
}
