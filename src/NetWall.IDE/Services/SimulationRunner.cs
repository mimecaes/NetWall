using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public static class SimulationRunner
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Task<SimulationResult?> RunAsync(string cliPath, string filePath) =>
        RunAsync(cliPath, filePath, request: null);

    public static Task<SimulationResult?> RunCustomAsync(string cliPath, string filePath, IEnumerable<string> rules,
        IEnumerable<SimulatedPacket> packets)
    {
        var request = JsonSerializer.Serialize(new
        {
            rules,
            packets = packets.Select(packet => new
            {
                protocol = packet.Protocol,
                port = packet.Port,
                source = packet.Source,
                destination = packet.Destination,
            }),
        });
        return RunAsync(cliPath, filePath, request);
    }

    private static async Task<SimulationResult?> RunAsync(string cliPath, string filePath, string? request)
    {
        var startInfo = new ProcessStartInfo("python")
        {
            ArgumentList = { cliPath, filePath, request is null ? "--simulacion" : "--simulacion-personalizada" },
            RedirectStandardOutput = true,
            RedirectStandardInput = request is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = request is null ? null : new UTF8Encoding(false),
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (request is not null)
            {
                await process.StandardInput.WriteAsync(request);
                process.StandardInput.Close();
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
