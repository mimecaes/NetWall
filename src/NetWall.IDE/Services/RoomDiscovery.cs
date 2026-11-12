using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetWall.IDE.Services;

public sealed class RoomDiscovery
{
    private CancellationTokenSource? _stop;

    public event Action<ClassMessage>? RoomFound;

    public void Start()
    {
        Stop();
        _stop = new CancellationTokenSource();
        _ = ListenAsync(_stop.Token);
    }

    public void Stop()
    {
        _stop?.Cancel();
        _stop = null;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, ClassProtocol.DiscoveryPort));
        }
        catch (SocketException)
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await udp.ReceiveAsync(token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            if (packet.Buffer.Length < 4096
                && ClassProtocol.Parse(Encoding.UTF8.GetString(packet.Buffer)) is { App: ClassProtocol.AppName, Type: "room" } room
                && ClassProtocol.DecodeCode(room.Code) is not null)
            {
                RoomFound?.Invoke(room);
            }
        }
    }
}
