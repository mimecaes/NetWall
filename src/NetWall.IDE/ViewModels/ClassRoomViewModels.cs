using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public sealed partial class LabSeat(int number) : ObservableObject
{
    public int Number { get; } = number;

    public string? Token { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAway), nameof(Label), nameof(StatusText))]
    public partial bool IsOccupied { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAway), nameof(StatusText))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PetColor Color { get; set; }

    [ObservableProperty]
    public partial int Accessory { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    public bool IsAway => IsOccupied && !IsConnected;

    public string Label => IsOccupied ? Name : $"Puesto {Number}";

    public string StatusText => IsConnected ? "Conectado" : "Desconectado, esperando que vuelva";
}

public sealed partial class AvatarChoice(PetColor color, int accessory, string label) : ObservableObject
{
    public PetColor Color { get; } = color;

    public int Accessory { get; } = accessory;

    public string Label { get; } = label;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed partial class FoundRoom(string code) : ObservableObject
{
    public string Code { get; } = code;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    [ObservableProperty]
    public partial string ClassName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Students { get; set; }

    public string StudentsText => Students == 1 ? "1 estudiante" : $"{Students} estudiantes";

    partial void OnStudentsChanged(int value) => OnPropertyChanged(nameof(StudentsText));
}

public sealed partial class RoomHostViewModel : ObservableObject
{
    private const int Columns = 6;
    private const int StartingSeats = 30;

    private ClassHost? _host;

    public ObservableCollection<LabSeat> Seats { get; } = [];

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ClassName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ConnectedCount { get; set; }

    public string ConnectedText => ConnectedCount == 1 ? "1 estudiante conectado" : $"{ConnectedCount} estudiantes conectados";

    partial void OnConnectedCountChanged(int value) => OnPropertyChanged(nameof(ConnectedText));

    public void Start(ClassFile file)
    {
        Stop();
        var host = new ClassHost(file);
        host.StudentChanged += student => Dispatcher.UIThread.Post(() => OnStudentChanged(student));
        host.Start();
        _host = host;
        Seats.Clear();
        for (var number = 1; number <= StartingSeats; number++)
        {
            Seats.Add(new LabSeat(number));
        }

        ConnectedCount = 0;
        ClassName = file.Name;
        Code = host.Code;
    }

    public void Stop()
    {
        _host?.Stop();
        _host = null;
    }

    private void OnStudentChanged(RoomStudent student)
    {
        if (_host is null)
        {
            return;
        }

        while (Seats.Count < student.Seat)
        {
            for (var column = 0; column < Columns; column++)
            {
                Seats.Add(new LabSeat(Seats.Count + 1));
            }
        }

        var seat = Seats[student.Seat - 1];
        seat.Token = student.Token;
        seat.Name = student.Name;
        seat.Color = student.Color;
        seat.Accessory = student.Accessory;
        seat.Address = student.Address;
        seat.IsConnected = student.IsConnected;
        seat.IsOccupied = true;
        ConnectedCount = Seats.Count(item => item.IsConnected);
    }
}

public sealed partial class RoomJoinViewModel : ObservableObject
{
    private static readonly TimeSpan RoomLifetime = TimeSpan.FromSeconds(5);

    private readonly RoomDiscovery _discovery = new();
    private readonly DispatcherTimer _prune = new() { Interval = TimeSpan.FromSeconds(1) };
    private ClassClient? _client;

    public RoomJoinViewModel()
    {
        _discovery.RoomFound += room => Dispatcher.UIThread.Post(() => OnRoomFound(room));
        _prune.Tick += (_, _) => PruneRooms();
    }

    public ObservableCollection<FoundRoom> Rooms { get; } = [];

    public ObservableCollection<string> Exercises { get; } = [];

    public ObservableCollection<AvatarChoice> Colors { get; } =
    [
        new(PetColor.Gray, 0, "Gris") { IsSelected = true },
        new(PetColor.Orange, 0, "Naranja"),
        new(PetColor.White, 0, "Blanco"),
    ];

    public ObservableCollection<AvatarChoice> Accessories { get; } =
    [
        new(PetColor.Gray, 0, "Ninguno") { IsSelected = true },
        new(PetColor.Gray, 1, "Moño"),
        new(PetColor.Gray, 2, "Lentes"),
        new(PetColor.Gray, 3, "Bufanda"),
        new(PetColor.Gray, 4, "Gorro"),
    ];

    [ObservableProperty]
    public partial PetColor Color { get; set; }

    [ObservableProperty]
    public partial int Accessory { get; set; }

    [ObservableProperty]
    public partial int Seat { get; set; }

    [RelayCommand]
    private void PickColor(AvatarChoice choice)
    {
        foreach (var item in Colors)
        {
            item.IsSelected = item == choice;
        }

        Color = choice.Color;
    }

    [RelayCommand]
    private void PickAccessory(AvatarChoice choice)
    {
        foreach (var item in Accessories)
        {
            item.IsSelected = item == choice;
        }

        Accessory = choice.Accessory;
    }

    public event Action? Joined;

    public event Action<string>? Failed;

    public event Action<string>? Ended;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanJoin))]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial bool IsReconnecting { get; set; }

    [ObservableProperty]
    public partial string ClassName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SeatText { get; set; } = string.Empty;

    public bool CanJoin => !IsConnecting;

    public bool HasRooms => Rooms.Count > 0;

    public void StartSearching()
    {
        Rooms.Clear();
        OnPropertyChanged(nameof(HasRooms));
        _discovery.Start();
        _prune.Start();
    }

    public void StopSearching()
    {
        _discovery.Stop();
        _prune.Stop();
    }

    [RelayCommand]
    private Task JoinRoom(FoundRoom room) => JoinAsync(room.Code);

    [RelayCommand]
    private Task JoinCode() => JoinAsync(Code);

    public async Task JoinAsync(string code)
    {
        var name = ClassProtocol.CleanName(Name);
        if (name.Length == 0)
        {
            Failed?.Invoke("Escribe tu nombre para entrar a la clase.");
            return;
        }

        if (ClassProtocol.DecodeCode(code) is not { } room)
        {
            Failed?.Invoke("El código no es válido. Revisa que tenga 7 letras o números, como K7M2-QX4.");
            return;
        }

        IsConnecting = true;
        Leave();
        var client = new ClassClient(room, name, Color, Accessory);
        client.Welcomed += message => Dispatcher.UIThread.Post(() => Welcome(message));
        client.Reconnecting += () => Dispatcher.UIThread.Post(() => IsReconnecting = true);
        client.Ended += reason => Dispatcher.UIThread.Post(() => End(client, reason));
        try
        {
            var welcome = await client.JoinAsync();
            _client = client;
            StopSearching();
            Welcome(welcome);
            Joined?.Invoke();
        }
        catch (ClassFileException error)
        {
            Failed?.Invoke(error.Message);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Leave()
    {
        _client?.Leave();
        _client = null;
        IsReconnecting = false;
    }

    private void Welcome(ClassMessage message)
    {
        IsReconnecting = false;
        ClassName = message.ClassName ?? string.Empty;
        Seat = message.Seat ?? 0;
        SeatText = $"Estás en el puesto {message.Seat}";
        Exercises.Clear();
        foreach (var exercise in message.Exercises ?? [])
        {
            Exercises.Add(exercise.Title);
        }
    }

    private void End(ClassClient client, string reason)
    {
        if (client != _client)
        {
            return;
        }

        Leave();
        Ended?.Invoke(reason);
    }

    private void OnRoomFound(ClassMessage message)
    {
        if (message.Code is not { } code)
        {
            return;
        }

        var room = Rooms.FirstOrDefault(item => item.Code == code);
        if (room is null)
        {
            room = new FoundRoom(code);
            Rooms.Add(room);
            OnPropertyChanged(nameof(HasRooms));
        }

        room.ClassName = message.ClassName ?? string.Empty;
        room.Students = message.Students ?? 0;
        room.LastSeen = DateTime.UtcNow;
    }

    private void PruneRooms()
    {
        foreach (var room in Rooms.Where(room => DateTime.UtcNow - room.LastSeen > RoomLifetime).ToList())
        {
            Rooms.Remove(room);
        }

        OnPropertyChanged(nameof(HasRooms));
    }
}
