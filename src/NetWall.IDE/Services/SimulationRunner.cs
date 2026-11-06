using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        RunCliAsync<SimulationResult>(cliPath, filePath, "--simulacion", null);

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
        return RunCliAsync<SimulationResult>(cliPath, filePath, "--simulacion-personalizada", request);
    }

    public static Task<ChallengeResult?> RunChallengeAsync(string cliPath, string filePath,
        IEnumerable<ChallengeTest> packets)
    {
        var request = JsonSerializer.Serialize(new
        {
            packets = packets.Select(packet => new
            {
                protocol = packet.Protocol,
                port = packet.Port,
                source = packet.Source,
                destination = packet.Destination,
            }),
        });
        return RunCliAsync<ChallengeResult>(cliPath, filePath, "--retos", request);
    }

    private static async Task<T?> RunCliAsync<T>(string cliPath, string filePath, string mode, string? request)
        where T : class
    {
        var startInfo = new ProcessStartInfo("python")
        {
            ArgumentList = { cliPath, filePath, mode },
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
                try
                {
                    await process.StandardInput.WriteAsync(request);
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    
                }
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return JsonSerializer.Deserialize<T>(output, Options);
        }
        catch (Exception e) when (e is Win32Exception or JsonException)
        {
            return null;
        }
    }
}
