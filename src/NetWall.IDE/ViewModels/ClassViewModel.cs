using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lucide.Avalonia;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public sealed record ClassExerciseCard(ClassExercise Exercise, int Number)
{
    public string TypeText => Exercise.Type switch
    {
        "predict" => "¿Pasa o se bloquea?",
        "fix" => "Arregla el firewall",
        _ => "Reto libre",
    };

    public LucideIconKind Icon => Exercise.Type switch
    {
        "predict" => LucideIconKind.Vote,
        "fix" => LucideIconKind.Wrench,
        _ => LucideIconKind.Pencil,
    };

    public string TestsText => Exercise.Tests.Count == 1 ? "1 prueba" : $"{Exercise.Tests.Count} pruebas";
}

public sealed partial class ClassViewModel : ViewModelBase
{
    private const int RevealDelay = 70;

    private int _revealVersion;

    public ObservableCollection<ClassExerciseCard> Exercises { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClass), nameof(HasNoClass), nameof(ClassName), nameof(Description),
        nameof(ExerciseCountText))]
    public partial ClassFile? File { get; set; }

    public bool HasClass => File is not null;

    public bool HasNoClass => File is null;

    public string ClassName => File?.Name ?? string.Empty;

    public string Description => File?.Description ?? string.Empty;

    public string ExerciseCountText => File is { Exercises.Count: var count }
        ? count == 1 ? "1 ejercicio" : $"{count} ejercicios"
        : string.Empty;

    public async Task LoadAsync(string path)
    {
        var file = ClassFileLoader.Load(path);
        var version = ++_revealVersion;
        Exercises.Clear();
        File = file;
        for (var i = 0; i < file.Exercises.Count && version == _revealVersion; i++)
        {
            Exercises.Add(new ClassExerciseCard(file.Exercises[i], i + 1));
            await Task.Delay(RevealDelay);
        }
    }
}
