using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetWall.IDE.Services;

public sealed class ClassClient(IPEndPoint room, string name)
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private const int MaxRetries = 15;

    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();

    public event Action<ClassMessage>? Welcomed;

    public event Action? Reconnecting;

    public event Action<string>? Ended;

    public async Task<ClassMessage> JoinAsync()
    {
        var result = await TryJoinAsync(_stop.Token);
        if (result is not { } reply)
        {
            throw new ClassFileException("No se pudo conectar con la sala. Revisa el código y que estés en la misma red.");
        }

        if (reply.Type != "welcome")
        {
            throw new ClassFileException(reply.Reason ?? "El profesor no permitió la entrada.");
        }

        return reply;
    }

    public void Leave() => _stop.Cancel();

    private async Task<ClassMessage?> TryJoinAsync(CancellationToken token)
    {
        var connection = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(ConnectTimeout);
            await connection.ConnectAsync(room, timeout.Token);
            var stream = connection.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await ClassProtocol.SendAsync(writer, new ClassMessage("join", Name: name, Token: _token), timeout.Token);
            var reply = await ClassProtocol.ReceiveAsync(reader, timeout.Token);
            if (reply is { Type: "welcome" })
            {
                _ = ListenAsync(connection, reader, token);
            }
            else
            {
                connection.Dispose();
            }

            return reply;
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or SocketException
                                       or ObjectDisposedException or InvalidDataException)
        {
            connection.Dispose();
            return null;
        }
    }

    private async Task ListenAsync(TcpClient connection, StreamReader reader, CancellationToken token)
    {
        using (connection)
        {
            try
            {
                while (await ClassProtocol.ReceiveAsync(reader, token) is { } message)
                {
                    if (message.Type == "closed")
                    {
                        Ended?.Invoke("El profesor cerró la sala.");
                        return;
                    }
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException
                                           or ObjectDisposedException or InvalidDataException)
            {
            }
        }

        if (!token.IsCancellationRequested)
        {
            await ReconnectAsync(token);
        }
    }

    private async Task ReconnectAsync(CancellationToken token)
    {
        Reconnecting?.Invoke();
        for (var attempt = 0; attempt < MaxRetries && !token.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(RetryDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (await TryJoinAsync(token) is { Type: "welcome" } reply)
            {
                Welcomed?.Invoke(reply);
                return;
            }
        }

        if (!token.IsCancellationRequested)
        {
            Ended?.Invoke("Se perdió la conexión con la sala.");
        }
    }
}
