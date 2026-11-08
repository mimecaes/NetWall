using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public sealed partial class ClassViewModel(Func<string?> cliPath) : ViewModelBase
{
    private const int RevealDelay = 70;
    private const int PredictDelay = 500;
    private const int CurrentVersion = 1;

    private int _revealVersion;
    private bool _loading;
    private Action? _pendingDiscard;

    public ObservableCollection<ExerciseEditor> Exercises { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoClass))]
    public partial bool HasClass { get; set; }

    public bool HasNoClass => !HasClass;

    public string ClassId { get; private set; } = string.Empty;

    public string? FilePath { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingDiscard { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ExerciseEditor? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    public string ExerciseCountText => Exercises.Count == 1 ? "1 ejercicio" : $"{Exercises.Count} ejercicios";

    public void RunOrConfirm(Action action)
    {
        if (!IsDirty)
        {
            action();
            return;
        }

        _pendingDiscard = action;
        IsConfirmingDiscard = true;
    }

    [RelayCommand]
    private void ConfirmDiscard()
    {
        IsConfirmingDiscard = false;
        var action = _pendingDiscard;
        _pendingDiscard = null;
        action?.Invoke();
    }

    [RelayCommand]
    private void CancelDiscard()
    {
        IsConfirmingDiscard = false;
        _pendingDiscard = null;
    }

    public void NewClass()
    {
        ++_revealVersion;
        _loading = true;
        Exercises.Clear();
        ClassId = $"clase-{NewId()}";
        FilePath = null;
        Name = "Nueva clase";
        Description = string.Empty;
        _loading = false;
        HasClass = true;
        AddExercise(null);
        IsDirty = false;
    }

    public async Task LoadAsync(string path)
    {
        var file = ClassFileLoader.Load(path);
        var version = ++_revealVersion;
        _loading = true;
        Exercises.Clear();
        Selected = null;
        ClassId = file.Id;
        FilePath = path;
        Name = file.Name;
        Description = file.Description ?? string.Empty;
        HasClass = true;
        IsDirty = false;
        _loading = false;
        OnPropertyChanged(nameof(ExerciseCountText));
        foreach (var exercise in file.Exercises)
        {
            if (version != _revealVersion)
            {
                return;
            }

            Insert(Exercises.Count, ExerciseEditor.FromModel(exercise, exercise.Id, OnExerciseAction));
            if (Exercises.Count == 1)
            {
                Select(Exercises[0]);
            }

            await Task.Delay(RevealDelay);
        }
    }

    public void Save(string path)
    {
        var file = new ClassFile(CurrentVersion, ClassId, Name.Trim(),
            Description.Trim().Length > 0 ? Description.Trim() : null, [.. Exercises.Select(exercise => exercise.ToModel())]);
        ClassFileLoader.Save(file, path);
        FilePath = path;
        IsDirty = false;
    }

    [RelayCommand]
    private void AddExercise(string? type)
    {
        var exercise = new ExerciseEditor(NewId(), type == "free" ? null : type, OnExerciseAction);
        if (exercise.IsPredict)
        {
            exercise.AddTestCommand.Execute("packet");
        }

        Insert(Exercises.Count, exercise);
        Select(exercise);
        MarkDirty();
    }

    public void Move(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Exercises.Count || to >= Exercises.Count)
        {
            return;
        }

        Exercises.Move(from, to);
        Renumber();
        MarkDirty();
    }

    private void Insert(int index, ExerciseEditor exercise)
    {
        exercise.Changed += OnExerciseChanged;
        Exercises.Insert(index, exercise);
        Renumber();
        if (exercise.IsPredict)
        {
            _ = PredictAsync(exercise);
        }
    }

    private void OnExerciseAction(ExerciseEditor exercise, string action)
    {
        switch (action)
        {
            case "select":
                Select(exercise);
                break;
            case "duplicate":
                var copy = ExerciseEditor.FromModel(exercise.ToModel(), NewId(), OnExerciseAction);
                copy.Title = $"{exercise.Title} (copia)";
                Insert(Exercises.IndexOf(exercise) + 1, copy);
                Select(copy);
                MarkDirty();
                break;
            case "delete":
                var index = Exercises.IndexOf(exercise);
                exercise.Changed -= OnExerciseChanged;
                Exercises.Remove(exercise);
                Renumber();
                if (Selected == exercise)
                {
                    Select(Exercises.Count == 0 ? null : Exercises[Math.Min(index, Exercises.Count - 1)]);
                }

                MarkDirty();
                break;
        }
    }

    private void Select(ExerciseEditor? exercise)
    {
        foreach (var item in Exercises)
        {
            item.IsSelected = item == exercise;
            item.IsConfirmingDelete = false;
        }

        Selected = exercise;
    }

    private void Renumber()
    {
        for (var i = 0; i < Exercises.Count; i++)
        {
            Exercises[i].Number = i + 1;
        }

        OnPropertyChanged(nameof(ExerciseCountText));
    }

    private void OnExerciseChanged(ExerciseEditor exercise, bool predictInput)
    {
        MarkDirty();
        if (predictInput)
        {
            _ = PredictAsync(exercise);
        }
    }

    private async Task PredictAsync(ExerciseEditor exercise)
    {
        var version = ++exercise.PredictVersion;
        exercise.PredictText = "NetWall está calculando la respuesta…";
        await Task.Delay(PredictDelay);
        if (version != exercise.PredictVersion)
        {
            return;
        }

        if (exercise.Tests.FirstOrDefault() is not { } test || !Ipv4.IsHost(test.Source.Trim())
            || !Ipv4.IsHost(test.Destination.Trim()) || test.Port is not (>= 0 and <= 65535))
        {
            exercise.PredictText = "Completa el paquete para calcular la respuesta.";
            return;
        }

        if (exercise.Starter.Trim().Length == 0)
        {
            exercise.PredictText = "Escribe el firewall para calcular la respuesta.";
            return;
        }

        if (cliPath() is not { } cli)
        {
            exercise.PredictText = "No se encontró el compilador de NetWall.";
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "NetWall", "class-preview");
        var file = Path.Combine(folder, $"{exercise.Id}.nw");
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(file, exercise.Starter);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            exercise.PredictText = "No se pudo preparar el firewall para calcular la respuesta.";
            return;
        }

        var result = await SimulationRunner.RunChallengeAsync(cli, file, [test.ToModel()]);
        if (version != exercise.PredictVersion)
        {
            return;
        }

        if (result is null)
        {
            exercise.PredictText = "No se pudo ejecutar NetWall. Revisa que Python esté instalado.";
        }
        else if (result.Errors || result.Packets is not [var packet])
        {
            var error = result.Diagnostics.FirstOrDefault();
            exercise.PredictText = error is null
                ? "El firewall tiene un error."
                : $"El firewall tiene un error en la línea {error.Line}: {error.Message}.";
        }
        else
        {
            var loading = _loading;
            _loading = true;
            test.Expect = packet.Action;
            _loading = loading;
            var reason = packet.Rule is { } rule ? $"la regla {rule}" : "la política por defecto";
            exercise.PredictText = $"Respuesta correcta: {(packet.IsAllowed ? "pasa" : "se bloquea")}, por {reason}.";
        }
    }

    private void MarkDirty()
    {
        if (!_loading)
        {
            IsDirty = true;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Name) or nameof(Description))
        {
            MarkDirty();
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
