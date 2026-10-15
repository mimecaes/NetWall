using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public static class SimulationRunner
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static async Task<SimulationResult?> RunAsync(string cliPath, string filePath)
    {
        var startInfo = new ProcessStartInfo("python")
        {
            ArgumentList = { cliPath, filePath, "--simulacion" },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return JsonSerializer.Deserialize<SimulationResult>(output, Options);
        }
        catch (Exception e) when (e is Win32Exception or JsonException)
        {
            return null;
        }
    }
}
