using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public sealed class RoomStudent(string token, string name, int seat)
{
    public string Token { get; } = token;

    public string Name { get; set; } = name;

    public PetColor Color { get; set; }

    public int Accessory { get; set; }

    public string Address { get; set; } = string.Empty;

    public HashSet<string> Solved { get; } = [];

    public int Attempts { get; set; }

    public bool IsReviewing { get; set; }

    public SemaphoreSlim WriteLock { get; } = new(1, 1);

    public int Seat { get; } = seat;

    public bool IsConnected { get; set; }

    public StreamWriter? Writer { get; set; }

    public TcpClient? Connection { get; set; }
}

public sealed partial class ClassHost
{
    public const int MaxStudents = 60;

    private const int KittenAvatarAccessories = 5;

    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReviewTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AnnounceInterval = TimeSpan.FromSeconds(1.5);

    private readonly ClassFile _file;
    private readonly Func<string?> _cliPath;
    private readonly SemaphoreSlim _reviews = new(2, 2);
    private readonly Dictionary<string, RoomStudent> _students = [];
    private readonly CancellationTokenSource _stop = new();
    private TcpListener? _listener;
    private IPAddress _broadcast = IPAddress.Broadcast;

    public ClassHost(ClassFile file, Func<string?> cliPath)
    {
        _cliPath = cliPath;
        _file = file with { Exercises = [.. file.Exercises.Select(exercise => exercise with { Solution = null })] };
    }

    public string Code { get; private set; } = string.Empty;

    public event Action<RoomStudent>? StudentChanged;

    public int ExerciseCount => _file.Exercises.Count(exercise => exercise.Type != "predict");

    public void Start()
    {
        if (ClassProtocol.FindLocalNetwork() is not { } network)
        {
            throw new ClassFileException("No estás conectado a una red. Conéctate al Wi-Fi del aula para abrir la sala.");
        }

        for (var port = ClassProtocol.FirstRoomPort; port < ClassProtocol.FirstRoomPort + ClassProtocol.RoomPorts; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                _listener = listener;
                Code = ClassProtocol.EncodeCode(network.Address, port);
                _broadcast = network.Broadcast;
                break;
            }
            catch (SocketException)
            {
            }
        }

        if (_listener is null)
        {
            throw new ClassFileException("No se pudo abrir la sala porque los puertos de NetWall están ocupados.");
        }

        _ = AcceptAsync(_listener, _stop.Token);
        _ = AnnounceAsync(_stop.Token);
    }

    public void Stop()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        _stop.Cancel();
        _listener?.Stop();
        List<RoomStudent> students;
        lock (_students)
        {
            students = [.. _students.Values];
        }

        foreach (var student in students)
        {
            try
            {
                student.Writer?.WriteLine(ClassProtocol.Serialize(new ClassMessage("closed")));
                student.Writer?.Flush();
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
            }

            student.Connection?.Dispose();
        }
    }

    private async Task AcceptAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient connection;
            try
            {
                connection = await listener.AcceptTcpClientAsync(token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = ServeAsync(connection, token);
        }
    }

    private async Task ServeAsync(TcpClient connection, CancellationToken token)
    {
        RoomStudent? student = null;
        try
        {
            using (connection)
            {
                var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                using var joinTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                joinTimeout.CancelAfter(JoinTimeout);
                var join = await ClassProtocol.ReceiveAsync(reader, joinTimeout.Token);
                var name = ClassProtocol.CleanName(join?.Name);
                if (join is not { Type: "join" } || name.Length == 0 || string.IsNullOrWhiteSpace(join.Token))
                {
                    await ClassProtocol.SendAsync(writer, new ClassMessage("rejected", Reason: "Escribe tu nombre para entrar."), token);
                    return;
                }

                student = Seat(join.Token, name);
                if (student is null)
                {
                    await ClassProtocol.SendAsync(writer, new ClassMessage("rejected", Reason: "La sala está llena."), token);
                    return;
                }

                student.Color = Enum.TryParse<PetColor>(join.Color, out var color) ? color : PetColor.Gray;
                student.Accessory = join.Accessory is >= 0 and < KittenAvatarAccessories ? join.Accessory.Value : 0;
                student.Address = (connection.Client.RemoteEndPoint as IPEndPoint)?.Address.MapToIPv4().ToString() ?? string.Empty;
                student.Connection?.Dispose();
                student.Connection = connection;
                student.Writer = writer;
                student.IsConnected = true;
                await SendAsync(student, new ClassMessage("welcome", Seat: student.Seat, ClassName: _file.Name,
                    Description: _file.Description, Exercises: _file.Exercises), token);
                StudentChanged?.Invoke(student);
                while (await ClassProtocol.ReceiveAsync(reader, token) is { } message)
                {
                    if (message.Type == "submit")
                    {
                        _ = ReviewAsync(student, message, token);
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or SocketException
                                       or ObjectDisposedException or InvalidDataException)
        {
        }
        finally
        {
            if (student is not null && student.Connection == connection)
            {
                student.IsConnected = false;
                student.Writer = null;
                student.Connection = null;
                if (!token.IsCancellationRequested)
                {
                    StudentChanged?.Invoke(student);
                }
            }
        }
    }

    private async Task ReviewAsync(RoomStudent student, ClassMessage message, CancellationToken token)
    {
        if (student.IsReviewing)
        {
            await SendResultAsync(student, message.Exercise, false, null, "Espera el resultado de tu envío anterior.", token);
            return;
        }

        var exercise = _file.Exercises.FirstOrDefault(item => item.Id == message.Exercise && item.Type != "predict");
        var source = message.Source ?? string.Empty;
        var problem = exercise is null ? "Ese ejercicio no existe en esta clase."
            : source.Length > ClassProtocol.MaxSourceChars ? "Tu código es demasiado largo."
            : ImportPattern().IsMatch(source) ? "En la sala no se puede usar importar."
            : _cliPath() is null ? "La computadora del profesor no encontró el compilador de NetWall."
            : null;
        if (exercise is null || problem is not null)
        {
            await SendResultAsync(student, message.Exercise, false, null, problem, token);
            return;
        }

        student.IsReviewing = true;
        try
        {
            await _reviews.WaitAsync(token);
            ChallengeResult? result;
            var timedOut = false;
            try
            {
                var folder = Path.Combine(Path.GetTempPath(), "NetWall", "class-room");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, $"{student.Token}-{exercise.Id}.nw");
                await File.WriteAllTextAsync(file, source, token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(ReviewTimeout);
                result = await SimulationRunner.RunChallengeAsync(_cliPath()!, file,
                    exercise.Tests.Where(test => test.Kind == "packet"), timeout.Token);
                timedOut = timeout.IsCancellationRequested && !token.IsCancellationRequested;
            }
            finally
            {
                _reviews.Release();
            }

            student.Attempts++;
            if (result is null || result.Errors)
            {
                var error = result?.Diagnostics.FirstOrDefault();
                var reason = timedOut ? "Tu código tardó demasiado en revisarse."
                    : error is null ? "No se pudo revisar tu código."
                    : $"Tu código tiene un error en la línea {error.Line}: {error.Message}.";
                await SendResultAsync(student, exercise.Id, false, null, reason, token);
            }
            else
            {
                var checks = ClassChecker.Check(exercise.Tests, result);
                var passed = checks.All(check => check);
                if (passed)
                {
                    student.Solved.Add(exercise.Id);
                }

                await SendResultAsync(student, exercise.Id, passed, checks, null, token);
            }

            StudentChanged?.Invoke(student);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
        }
        finally
        {
            student.IsReviewing = false;
        }
    }

    private Task SendResultAsync(RoomStudent student, string? exercise, bool passed, List<bool>? checks, string? reason,
        CancellationToken token) =>
        SendAsync(student, new ClassMessage("result", Exercise: exercise, Passed: passed, Checks: checks, Reason: reason), token);

    private static async Task SendAsync(RoomStudent student, ClassMessage message, CancellationToken token)
    {
        if (student.Writer is not { } writer)
        {
            return;
        }

        await student.WriteLock.WaitAsync(token);
        try
        {
            await ClassProtocol.SendAsync(writer, message, token);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
        }
        finally
        {
            student.WriteLock.Release();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bimportar\b")]
    private static partial System.Text.RegularExpressions.Regex ImportPattern();

    private RoomStudent? Seat(string token, string name)
    {
        lock (_students)
        {
            if (_students.TryGetValue(token, out var existing))
            {
                existing.Name = name;
                return existing;
            }

            if (_students.Count >= MaxStudents)
            {
                return null;
            }

            var taken = _students.Values.Select(student => student.Seat).ToHashSet();
            var seat = Enumerable.Range(1, MaxStudents).First(number => !taken.Contains(number));
            var student = new RoomStudent(token, name, seat);
            _students[token] = student;
            return student;
        }
    }

    private async Task AnnounceAsync(CancellationToken token)
    {
        using var udp = new UdpClient { EnableBroadcast = true };
        while (!token.IsCancellationRequested)
        {
            int count;
            lock (_students)
            {
                count = _students.Values.Count(student => student.IsConnected);
            }

            var payload = Encoding.UTF8.GetBytes(ClassProtocol.Serialize(new ClassMessage("room",
                ClassName: _file.Name, Code: Code, Students: count, App: ClassProtocol.AppName)));
            foreach (var target in new[] { IPAddress.Broadcast, _broadcast }.Distinct())
            {
                try
                {
                    await udp.SendAsync(payload, new IPEndPoint(target, ClassProtocol.DiscoveryPort), token);
                }
                catch (Exception e) when (e is SocketException or ObjectDisposedException)
                {
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                await Task.Delay(AnnounceInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
