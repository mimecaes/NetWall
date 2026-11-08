using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public sealed record ClassMessage(string Type, string? Name = null, string? Token = null, int? Seat = null,
    string? ClassName = null, string? Description = null, List<ClassExercise>? Exercises = null, string? Reason = null,
    string? Code = null, int? Students = null, string? App = null);

public static class ClassProtocol
{
    public const string AppName = "netwall-class";
    public const int DiscoveryPort = 47830;
    public const int FirstRoomPort = 47831;
    public const int RoomPorts = 4;
    public const int MaxNameLength = 24;
    public const int MaxMessageChars = 1024 * 1024;

    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const int CodeLength = 7;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(ClassMessage message) => JsonSerializer.Serialize(message, Options);

    public static ClassMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClassMessage>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task SendAsync(StreamWriter writer, ClassMessage message, CancellationToken token)
    {
        await writer.WriteLineAsync(Serialize(message).AsMemory(), token);
        await writer.FlushAsync(token);
    }

    public static async Task<ClassMessage?> ReceiveAsync(StreamReader reader, CancellationToken token)
    {
        var line = new StringBuilder();
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), token) == 1)
        {
            if (buffer[0] == '\n')
            {
                return Parse(line.ToString()) ?? new ClassMessage("invalid");
            }

            if (line.Length >= MaxMessageChars)
            {
                throw new InvalidDataException("El mensaje es demasiado grande.");
            }

            line.Append(buffer[0]);
        }

        return null;
    }

    public static string CleanName(string? name)
    {
        var clean = new string((name ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;
    }

    public static string EncodeCode(IPAddress address, int port)
    {
        var bytes = address.GetAddressBytes();
        var value = ((long)bytes[0] << 24 | (long)bytes[1] << 16 | (long)bytes[2] << 8 | bytes[3]) << 2
                    | (long)(port - FirstRoomPort);
        var chars = new char[CodeLength];
        for (var i = CodeLength - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        var code = new string(chars);
        return $"{code[..4]}-{code[4..]}";
    }

    public static IPEndPoint? DecodeCode(string? code)
    {
        var clean = (code ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (clean.Length != CodeLength)
        {
            return null;
        }

        long value = 0;
        foreach (var c in clean)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0)
            {
                return null;
            }

            value = value << 5 | (long)index;
        }

        var port = FirstRoomPort + (int)(value & 3);
        var ip = value >> 2;
        if (ip > uint.MaxValue)
        {
            return null;
        }

        return new IPEndPoint(new IPAddress([(byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip]), port);
    }

    public static (IPAddress Address, IPAddress Broadcast)? FindLocalNetwork()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                          && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(nic => (Properties: nic.GetIPProperties(), Nic: nic))
            .SelectMany(item => item.Properties.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => (address, HasGateway: item.Properties.GatewayAddresses
                    .Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork))))
            .OrderByDescending(item => item.HasGateway)
            .ToList();
        if (candidates.FirstOrDefault() is not { address: { } best })
        {
            return null;
        }

        var ip = best.Address.GetAddressBytes();
        var mask = best.IPv4Mask.GetAddressBytes();
        var broadcast = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            broadcast[i] = (byte)(ip[i] | ~mask[i]);
        }

        return (best.Address, new IPAddress(broadcast));
    }
}
