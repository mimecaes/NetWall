using System.Net;

namespace NetWall.IDE.Services;

public static class Ipv4
{
    public static (uint Base, int Mask)? Parse(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var address) || address.GetAddressBytes().Length != 4
            || parts[0].Split('.').Length != 4)
        {
            return null;
        }

        var mask = 32;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out mask) || mask is < 0 or > 32))
        {
            return null;
        }

        var bytes = address.GetAddressBytes();
        return ((uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]), mask);
    }

    public static bool Contains((uint Base, int Mask) network, uint address)
    {
        var bits = 32 - network.Mask;
        return bits == 32 || network.Base >> bits == address >> bits;
    }

    public static string Format(uint address) =>
        $"{address >> 24}.{(address >> 16) & 255}.{(address >> 8) & 255}.{address & 255}";

    public static bool IsHost(string cidr) => Parse(cidr) is { Mask: 32 };
}
