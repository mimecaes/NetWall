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

public enum ClassStage
{
    Landing,
    Editor,
    Hosting,
    Joining,
    Student,
}

public sealed partial class ClassViewModel(Func<string?> cliPath) : ViewModelBase
{
    private const int RevealDelay = 70;
    private const int CheckDelay = 140;
    private const int CurrentVersion = 1;

    private int _revealVersion;
    private bool _loading;
    private Action? _pendingDiscard;

    public ObservableCollection<ExerciseEditor> Exercises { get; } = [];

    public RoomHostViewModel Room { get; } = new();

    public RoomJoinViewModel Join { get; } = new(cliPath);

    [ObservableProperty]
    public partial bool HasClass { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLanding), nameof(IsEditing), nameof(IsHosting), nameof(IsJoining), nameof(IsStudent))]
    public partial ClassStage Stage { get; set; }

    public bool IsLanding => Stage == ClassStage.Landing;

    public bool IsEditing => Stage == ClassStage.Editor;

    public bool IsHosting => Stage == ClassStage.Hosting;

    public bool IsJoining => Stage == ClassStage.Joining;

    public bool IsStudent => Stage == ClassStage.Student;

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
        Stage = ClassStage.Editor;
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
        Stage = ClassStage.Editor;
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

    public ClassFile BuildFile() => new(CurrentVersion, ClassId, Name.Trim(),
        Description.Trim().Length > 0 ? Description.Trim() : null, [.. Exercises.Select(exercise => exercise.ToModel())]);

    public void OpenRoom()
    {
        var file = BuildFile();
        ClassFileLoader.Validate(file);
        Room.Start(file, cliPath);
        Stage = ClassStage.Hosting;
    }

    public void CloseRoom()
    {
        Room.Stop();
        Stage = ClassStage.Editor;
    }

    public void StartJoining()
    {
        Join.Leave();
        Join.StartSearching();
        Stage = ClassStage.Joining;
    }

    public void ShowStudent() => Stage = ClassStage.Student;

    public void BackToStart()
    {
        Join.StopSearching();
        Join.Leave();
        Stage = ClassStage.Landing;
    }

    public void CloseClass()
    {
        ++_revealVersion;
        _loading = true;
        Exercises.Clear();
        Selected = null;
        FilePath = null;
        _loading = false;
        HasClass = false;
        IsDirty = false;
        Stage = ClassStage.Landing;
    }

    public void LeaveAll()
    {
        Room.Stop();
        Join.StopSearching();
        Join.Leave();
        CancelDiscard();
        if (HasClass && IsDirty)
        {
            Stage = ClassStage.Editor;
        }
        else
        {
            CloseClass();
        }
    }

    public void Save(string path)
    {
        var file = BuildFile();
        ClassFileLoader.Save(file, path);
        FilePath = path;
        IsDirty = false;
    }

    [RelayCommand]
    private void AddExercise(string? type)
    {
        var exercise = new ExerciseEditor(NewId(), type == "free" ? null : type, OnExerciseAction);
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
            case "test":
                _ = TestSolutionAsync(exercise);
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

    private void OnExerciseChanged(ExerciseEditor exercise)
    {
        exercise.ClearChecks();
        MarkDirty();
    }

    private async Task TestSolutionAsync(ExerciseEditor exercise)
    {
        if (exercise.IsTesting)
        {
            return;
        }

        exercise.ClearChecks();
        var version = exercise.SolutionVersion;
        if (exercise.Tests.Count == 0)
        {
            ShowSolution(exercise, "Agrega al menos una prueba para probar tu solución.", false);
            return;
        }

        if (exercise.Solution.Trim().Length == 0)
        {
            ShowSolution(exercise, "Escribe tu solución para probarla.", false);
            return;
        }

        if (cliPath() is not { } cli)
        {
            ShowSolution(exercise, "No se encontró el compilador de NetWall.", false);
            return;
        }

        var tests = exercise.Tests.Select(test => test.ToModel()).ToList();
        exercise.IsTesting = true;
        exercise.SolutionText = "Probando tu solución…";
        var file = await WritePreviewAsync($"{exercise.Id}-solucion", exercise.Solution);
        var result = file is null ? null
            : await SimulationRunner.RunChallengeAsync(cli, file, tests.Where(test => test.Kind == "packet"));
        if (version != exercise.SolutionVersion)
        {
            return;
        }

        if (result is null)
        {
            ShowSolution(exercise, "No se pudo ejecutar NetWall. Revisa que Python esté instalado.", false);
            return;
        }

        if (result.Errors)
        {
            var error = result.Diagnostics.FirstOrDefault();
            ShowSolution(exercise, error is null
                ? "Tu solución tiene un error."
                : $"Tu solución tiene un error en la línea {error.Line}: {error.Message}.", false);
            return;
        }

        var checks = ClassChecker.Check(tests, result);
        for (var i = 0; i < checks.Count && i < exercise.Tests.Count; i++)
        {
            await Task.Delay(CheckDelay);
            if (version != exercise.SolutionVersion)
            {
                return;
            }

            exercise.Tests[i].Check = checks[i] ? TestCheck.Passed : TestCheck.Failed;
        }

        var passed = checks.Count(check => check);
        ShowSolution(exercise, passed == checks.Count
            ? "Tu solución pasa todas las pruebas. El ejercicio está listo."
            : $"Tu solución pasa {passed} de {checks.Count} pruebas. Revisa las marcadas en rojo o tu solución.",
            passed == checks.Count);
    }

    private static void ShowSolution(ExerciseEditor exercise, string text, bool passed)
    {
        exercise.IsTesting = false;
        exercise.SolutionPassed = passed;
        exercise.SolutionText = text;
    }

    private static async Task<string?> WritePreviewAsync(string name, string code)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NetWall", "class-preview");
        var file = Path.Combine(folder, $"{name}.nw");
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(file, code);
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
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
