using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public sealed class ClassClient(IPEndPoint room, string name, PetColor color, int accessory)
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private const int MaxRetries = 15;

    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private StreamWriter? _writer;

    public event Action<ClassMessage>? ResultReceived;

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

    public void Leave() => _ = LeaveAsync();

    private async Task LeaveAsync()
    {
        await SendAsync(new ClassMessage("leave"));
        _stop.Cancel();
    }

    public async Task<bool> SendAsync(ClassMessage message)
    {
        if (_writer is not { } writer)
        {
            return false;
        }

        await _writeLock.WaitAsync();
        try
        {
            await ClassProtocol.SendAsync(writer, message, _stop.Token);
            return true;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

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
            await ClassProtocol.SendAsync(writer, new ClassMessage("join", Name: name, Token: _token, Color: color.ToString(),
                Accessory: accessory), timeout.Token);
            var reply = await ClassProtocol.ReceiveAsync(reader, timeout.Token);
            if (reply is { Type: "welcome" })
            {
                _writer = writer;
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

                    if (message.Type == "result")
                    {
                        ResultReceived?.Invoke(message);
                    }
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException
                                           or ObjectDisposedException or InvalidDataException)
            {
            }
        }

        _writer = null;
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
