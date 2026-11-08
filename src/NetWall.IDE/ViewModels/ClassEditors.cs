using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lucide.Avalonia;
using NetWall.IDE.Models;

namespace NetWall.IDE.ViewModels;

public enum TestCheck
{
    None,
    Passed,
    Failed,
}

public sealed partial class TestEditor : ObservableObject
{
    private const string AnyType = "Cualquiera";

    private static readonly HashSet<string> PacketFields =
        [nameof(Protocol), nameof(Port), nameof(Source), nameof(Destination)];

    private readonly Action<TestEditor> _remove;

    public TestEditor(string kind, Action<TestEditor> remove)
    {
        Kind = kind;
        _remove = remove;
    }

    public static IReadOnlyList<string> Protocols { get; } = ["tcp", "udp"];

    public static IReadOnlyList<string> VariableTypes { get; } = [AnyType, "Int", "Float", "String", "Boolean", "Red", "Lista"];

    public string Kind { get; }

    public bool IsPacket => Kind == "packet";

    public bool IsPolicy => Kind == "policy";

    public bool IsVariable => Kind == "variable";

    public bool HasExpect => (IsPacket && !IsComputed) || IsPolicy;

    public string KindText => Kind switch
    {
        "packet" => "Paquete",
        "policy" => "Política por defecto",
        "variable" => "Variable",
        _ => "Sin advertencias",
    };

    public LucideIconKind Icon => Kind switch
    {
        "packet" => LucideIconKind.Send,
        "policy" => LucideIconKind.Shield,
        "variable" => LucideIconKind.Variable,
        _ => LucideIconKind.Sparkles,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExpect))]
    public partial bool IsComputed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassed), nameof(IsFailed))]
    public partial TestCheck Check { get; set; }

    public bool IsPassed => Check == TestCheck.Passed;

    public bool IsFailed => Check == TestCheck.Failed;

    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoLabel))]
    public partial string Protocol { get; set; } = "tcp";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoLabel))]
    public partial decimal? Port { get; set; } = 80;

    [ObservableProperty]
    public partial string Source { get; set; } = "192.168.1.20";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoLabel))]
    public partial string Destination { get; set; } = "10.0.0.5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoLabel), nameof(ExpectsAllow), nameof(ExpectsBlock))]
    public partial string Expect { get; set; } = "permitir";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoLabel))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VariableType { get; set; } = AnyType;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public bool ExpectsAllow
    {
        get => Expect == "permitir";
        set
        {
            if (value)
            {
                Expect = "permitir";
            }
        }
    }

    public bool ExpectsBlock
    {
        get => Expect == "bloquear";
        set
        {
            if (value)
            {
                Expect = "bloquear";
            }
        }
    }

    public string AutoLabel => Kind switch
    {
        "packet" => $"{(Expect == "permitir" ? "Pasa" : "Se bloquea")} {Protocol} {Port:0} hacia {Destination}",
        "policy" => Expect == "permitir" ? "La política por defecto permite" : "La política por defecto bloquea",
        "variable" => Name.Trim().Length == 0 ? "Existe la variable" : $"Existe la variable {Name.Trim()}",
        _ => "El código no tiene advertencias",
    };

    public event Action<TestEditor, bool>? Changed;

    [RelayCommand]
    private void Remove() => _remove(this);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(AutoLabel) or nameof(ExpectsAllow) or nameof(ExpectsBlock) or nameof(HasExpect)
            or nameof(IsComputed) or nameof(Check) or nameof(IsPassed) or nameof(IsFailed))
        {
            return;
        }

        Changed?.Invoke(this, IsPacket && PacketFields.Contains(e.PropertyName ?? string.Empty));
    }

    public ChallengeTest ToModel() => new(
        Kind,
        string.IsNullOrWhiteSpace(Label) ? AutoLabel : Label.Trim(),
        IsVariable ? Name.Trim() : null,
        IsVariable && VariableType != AnyType ? VariableType : null,
        IsVariable && Value.Trim().Length > 0 ? Value.Trim() : null,
        IsPacket ? Protocol : null,
        IsPacket ? (int)(Port ?? -1) : 0,
        IsPacket ? Source.Trim() : null,
        IsPacket ? Destination.Trim() : null,
        IsPacket || IsPolicy ? Expect : null,
        null);

    public static TestEditor FromModel(ChallengeTest test, Action<TestEditor> remove)
    {
        var editor = new TestEditor(test.Kind, remove)
        {
            Protocol = test.Protocol ?? "tcp",
            Port = test.Port,
            Source = test.Source ?? string.Empty,
            Destination = test.Destination ?? string.Empty,
            Expect = test.Expect ?? "permitir",
            Name = test.Name ?? string.Empty,
            VariableType = test.Type ?? AnyType,
            Value = test.Value ?? string.Empty,
        };
        editor.Label = test.Label == editor.AutoLabel ? string.Empty : test.Label;
        return editor;
    }
}

public sealed partial class ExerciseEditor : ObservableObject
{
    private readonly Action<ExerciseEditor, string> _action;

    public ExerciseEditor(string id, string? type, Action<ExerciseEditor, string> action)
    {
        Id = id;
        Type = type;
        _action = action;
        Tests.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TestsText));
            Changed?.Invoke(this, IsPredict);
        };
    }

    public string Id { get; }

    public string? Type { get; }

    public ObservableCollection<TestEditor> Tests { get; } = [];

    public bool IsPredict => Type == "predict";

    public bool CanAddTests => !IsPredict;

    public bool CanTestSolution => !IsPredict;

    public string TypeText => Type switch
    {
        "predict" => "¿Pasa o se bloquea?",
        _ => "Reto libre",
    };

    public LucideIconKind Icon => Type switch
    {
        "predict" => LucideIconKind.Vote,
        _ => LucideIconKind.Pencil,
    };

    public string StarterText => Type switch
    {
        "predict" => "Firewall que leerán los estudiantes",
        _ => "Código inicial (opcional)",
    };

    public string TestsText => Tests.Count == 1 ? "1 prueba" : $"{Tests.Count} pruebas";

    public string DisplayTitle => Title.Trim().Length > 0 ? Title.Trim() : "Sin título";

    public int PredictVersion { get; set; }

    public int SolutionVersion { get; set; }

    [ObservableProperty]
    public partial string Solution { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTesting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSolutionResult))]
    public partial string SolutionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SolutionPassed { get; set; }

    public bool HasSolutionResult => SolutionText.Length > 0;

    [ObservableProperty]
    public partial int Number { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Statement { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Starter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial bool IsDragging { get; set; }

    [ObservableProperty]
    public partial string PredictText { get; set; } = string.Empty;

    public event Action<ExerciseEditor, bool>? Changed;

    [RelayCommand]
    private void AddTest(string kind) => Tests.Add(Watch(new TestEditor(kind, RemoveTest)));

    [RelayCommand]
    private void Select() => _action(this, "select");

    [RelayCommand]
    private void Duplicate() => _action(this, "duplicate");

    [RelayCommand]
    private void AskDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private void Delete() => _action(this, "delete");

    [RelayCommand]
    private void TestSolution() => _action(this, "test");

    public void ClearChecks()
    {
        SolutionVersion++;
        IsTesting = false;
        SolutionText = string.Empty;
        foreach (var test in Tests)
        {
            test.Check = TestCheck.None;
        }
    }

    private void RemoveTest(TestEditor test) => Tests.Remove(test);

    private TestEditor Watch(TestEditor test)
    {
        test.IsComputed = IsPredict;
        test.Changed += (_, packetChanged) => Changed?.Invoke(this, IsPredict && packetChanged);
        return test;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Title) or nameof(Statement) or nameof(Starter) or nameof(Solution))
        {
            Changed?.Invoke(this, IsPredict && e.PropertyName == nameof(Starter));
        }
    }

    public ClassExercise ToModel() => new(Id, Title.Trim(), Statement.Trim(),
        Starter.Trim().Length > 0 ? Starter : null, Type, [.. Tests.Select(test => test.ToModel())],
        !IsPredict && Solution.Trim().Length > 0 ? Solution : null);

    public static ExerciseEditor FromModel(ClassExercise exercise, string id, Action<ExerciseEditor, string> action)
    {
        var editor = new ExerciseEditor(id, exercise.Type, action)
        {
            Title = exercise.Title,
            Statement = exercise.Statement,
            Starter = exercise.Starter ?? string.Empty,
            Solution = exercise.Solution ?? string.Empty,
        };
        foreach (var test in exercise.Tests)
        {
            editor.Tests.Add(editor.Watch(TestEditor.FromModel(test, editor.RemoveTest)));
        }

        return editor;
    }
}
