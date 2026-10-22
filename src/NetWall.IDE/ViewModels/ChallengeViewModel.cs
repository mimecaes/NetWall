using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Controls;
using NetWall.IDE.Models;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public enum ChallengeTestState
{
    Pending,
    Passed,
    Failed,
}

public sealed partial class ChallengeLevel(Challenge challenge, ChallengeModule module, int number, IBrush? brush)
    : ObservableObject
{
    public Challenge Challenge { get; } = challenge;

    public ChallengeModule Module { get; } = module;

    public int Number { get; } = number;

    public IBrush? Brush { get; } = brush;

    public string Heading => $"{Challenge.Id} · {Challenge.Title}";

    [ObservableProperty]
    public partial int Fish { get; set; }

    [ObservableProperty]
    public partial bool IsUnlocked { get; set; }
}

public sealed partial class ChallengeTestCard(ChallengeTest test) : ObservableObject
{
    public ChallengeTest Test { get; } = test;

    public string Title => Test.Visible ? Test.Label : "Prueba oculta";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassed), nameof(IsFailed), nameof(StateText))]
    public partial ChallengeTestState State { get; set; }

    public bool IsPassed => State == ChallengeTestState.Passed;

    public bool IsFailed => State == ChallengeTestState.Failed;

    public string StateText => State switch
    {
        ChallengeTestState.Passed => "✓ pasa",
        ChallengeTestState.Failed => "✕ falla",
        _ => Test.Visible ? "por probar" : "?",
    };
}

public sealed partial class ChallengeReward(string text) : ObservableObject
{
    public string Text { get; } = text;

    [ObservableProperty]
    public partial bool IsEarned { get; set; }
}

public partial class ChallengeViewModel : ViewModelBase
{
    private readonly Func<string?> _cliPath;
    private readonly Dictionary<string, int> _fish;
    private CancellationTokenSource? _live;

    public ChallengeViewModel(Func<string?> cliPath)
    {
        _cliPath = cliPath;
        _fish = ChallengeProgress.LoadFish();
        var number = 0;
        foreach (var module in ChallengeCatalog.Load())
        {
            foreach (var challenge in module.Challenges)
            {
                Levels.Add(new ChallengeLevel(challenge, module, ++number, Resource(module.Brush))
                {
                    Fish = _fish.GetValueOrDefault(challenge.Id),
                });
            }
        }

        UpdateUnlocks();
    }

    public ObservableCollection<ChallengeLevel> Levels { get; } = [];

    public ObservableCollection<ChallengeTestCard> Tests { get; } = [];

    public ObservableCollection<ChallengeTestCard> Objectives { get; } = [];

    public ObservableCollection<ChallengeReward> Rewards { get; } = [];

    public event Action<string>? CodeReplaced;

    public event Action<string, BubbleTone>? HelpySpoke;

    public string Code { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(CanGoBack), nameof(CanGoForward), nameof(HiddenText))]
    public partial ChallengeLevel? Current { get; set; }

    [ObservableProperty]
    public partial string DiagnosticsText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiagnosticsDetail))]
    public partial string DiagnosticsDetail { get; set; } = string.Empty;

    public bool HasDiagnosticsDetail => DiagnosticsDetail.Length > 0;

    [ObservableProperty]
    public partial bool HasCodeErrors { get; set; }

    [ObservableProperty]
    public partial bool HasWarnings { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool IsChecking { get; set; }

    public string Subtitle => Current is null ? string.Empty : $"Reto {Current.Number} de {Levels.Count}";

    public bool CanGoBack => Current is not null && Current.Number > 1;

    public bool CanGoForward => Current is not null && Current.Number < Levels.Count && Levels[Current.Number].IsUnlocked;

    public int TotalFish => Levels.Sum(level => level.Fish);

    public string ProgressText => $"{Levels.Count(level => level.Fish > 0)} de {Levels.Count} retos";

    public string HiddenText
    {
        get
        {
            var hidden = Tests.Count(card => !card.Test.Visible);
            return hidden switch
            {
                0 => string.Empty,
                1 => "1 prueba oculta",
                _ => $"{hidden} pruebas ocultas",
            };
        }
    }

    [ObservableProperty]
    public partial bool IsBoardVisible { get; set; } = true;

    public void Open() => IsBoardVisible = true;

    public void Close()
    {
        _live?.Cancel();
        SaveCode();
    }

    public void OpenLevel(ChallengeLevel level)
    {
        if (!level.IsUnlocked)
        {
            return;
        }

        Select(level);
        IsBoardVisible = false;
    }

    public void ShowBoard()
    {
        _live?.Cancel();
        SaveCode();
        IsBoardVisible = true;
    }

    public void UpdateCode(string code)
    {
        Code = code;
        _ = LiveDiagnosticsAsync();
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            Select(Levels[Current!.Number - 2]);
        }
    }

    public void GoForward()
    {
        if (CanGoForward)
        {
            Select(Levels[Current!.Number]);
        }
    }

    private void Select(ChallengeLevel? level)
    {
        if (level is null)
        {
            return;
        }

        SaveCode();
        Current = level;
        Tests.Clear();
        Objectives.Clear();
        foreach (var test in level.Challenge.Tests)
        {
            var card = new ChallengeTestCard(test);
            Tests.Add(card);
            if (test.Visible)
            {
                Objectives.Add(card);
            }
        }

        Rewards.Clear();
        Rewards.Add(new ChallengeReward("Pasa todas las pruebas"));
        Rewards.Add(new ChallengeReward(level.Challenge.MaxRules switch
        {
            null => "Sin reglas de más",
            1 => "Usa una sola regla",
            var max => $"Usa hasta {max} reglas",
        }));
        Rewards.Add(new ChallengeReward("Sin advertencias del analizador"));
        ShowRewards(level.Fish);
        OnPropertyChanged(nameof(HiddenText));

        Code = ChallengeProgress.LoadCode(level.Challenge.Id) ?? level.Challenge.Starter;
        CodeReplaced?.Invoke(Code);
        DiagnosticsText = string.Empty;
        DiagnosticsDetail = string.Empty;
        _ = LiveDiagnosticsAsync();
        HelpySpoke?.Invoke(level.Challenge.Statement, BubbleTone.Neutral);
    }

    [RelayCommand]
    private void Hint()
    {
        if (Current is not null)
        {
            HelpySpoke?.Invoke(Current.Challenge.Hint, BubbleTone.Neutral);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (Current is null)
        {
            return;
        }

        Code = Current.Challenge.Starter;
        CodeReplaced?.Invoke(Code);
        _ = LiveDiagnosticsAsync();
        foreach (var card in Tests)
        {
            card.State = ChallengeTestState.Pending;
        }

        HelpySpoke?.Invoke("Volvimos al código del inicio. ¡Inténtalo de nuevo!", BubbleTone.Neutral);
    }

    private bool CanCheck() => !IsChecking;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        if (Current is not { } level)
        {
            return;
        }

        _live?.Cancel();
        IsChecking = true;
        var result = await RunAsync(level);
        IsChecking = false;
        if (Current != level)
        {
            return;
        }

        ShowDiagnostics(result);
        if (result is null)
        {
            HelpySpoke?.Invoke("No pude ejecutar NetWall. Revisa que Python esté instalado.", BubbleTone.Blocked);
            return;
        }

        if (result.Errors)
        {
            foreach (var card in Tests)
            {
                card.State = ChallengeTestState.Pending;
            }

            var error = result.Diagnostics.FirstOrDefault();
            HelpySpoke?.Invoke(error is null
                ? "Tu código tiene un error. Revísalo antes de probar."
                : $"Antes de probar, corrige la línea {error.Line}: {error.Message}.", BubbleTone.Blocked);
            return;
        }

        var packets = result.Packets ?? [];
        var next = 0;
        foreach (var card in Tests)
        {
            card.State = Passes(card.Test, result, packets, ref next)
                ? ChallengeTestState.Passed
                : ChallengeTestState.Failed;
        }

        if (Tests.FirstOrDefault(card => card.IsFailed) is { } failed)
        {
            ShowRewards(0);
            HelpySpoke?.Invoke(failed.Test.Visible
                ? failed.Test.Failure ?? $"Todavía no se cumple: {failed.Test.Label}."
                : $"Falló una prueba oculta. {failed.Test.Failure ?? "Piensa en los casos que no estás viendo."}",
                BubbleTone.Blocked);
            return;
        }

        var rulesOk = level.Challenge.MaxRules is not { } max || (result.Rules?.Count ?? 0) <= max;
        var clean = result.Diagnostics.Count == 0;
        var fish = rulesOk ? clean ? 3 : 2 : 1;
        ShowRewards(fish);
        if (fish > level.Fish)
        {
            level.Fish = fish;
            _fish[level.Challenge.Id] = fish;
            ChallengeProgress.SaveFish(_fish);
            UpdateUnlocks();
            OnPropertyChanged(nameof(TotalFish));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(CanGoForward));
        }

        HelpySpoke?.Invoke(SuccessMessage(level, fish), BubbleTone.Allowed);
    }

    private string SuccessMessage(ChallengeLevel level, int fish)
    {
        var next = level.Number < Levels.Count ? " Ya puedes pasar al siguiente reto." : " ¡Terminaste el curso!";
        return fish switch
        {
            3 => $"¡Excelente! Ganaste 3 pescaditos.{next}",
            2 => "¡Lo lograste! Ganaste 2 pescaditos. Para el tercero, corrige lo que el analizador marca como advertencia.",
            _ => $"¡Funciona! Ganaste 1 pescadito. Para más, intenta usar "
                 + $"{(level.Challenge.MaxRules == 1 ? "una sola regla" : $"{level.Challenge.MaxRules} reglas o menos")}.",
        };
    }

    private static bool Passes(ChallengeTest test, ChallengeResult result, List<SimulatedPacket> packets, ref int next)
    {
        switch (test.Kind)
        {
            case "packet":
                return next < packets.Count && packets[next++].Action == test.Expect;
            case "policy":
                return result.PolicyDefined && result.DefaultPolicy == test.Expect;
            case "clean":
                return result.Diagnostics.Count == 0;
            default:
                return result.Declarations?.FirstOrDefault(declaration => declaration.Name == test.Name) is { } found
                       && (test.Type is null || found.Type == test.Type)
                       && (test.Value is null || SameValue(found, test.Value));
        }
    }

    private static bool SameValue(ChallengeDeclaration declaration, string expected) =>
        declaration.Type == "Red"
            ? Ipv4.Parse(declaration.Text) is { } actual && Ipv4.Parse(expected) == actual
            : declaration.Text == expected;

    private void ShowRewards(int fish)
    {
        for (var i = 0; i < Rewards.Count; i++)
        {
            Rewards[i].IsEarned = i < fish;
        }
    }

    private async Task LiveDiagnosticsAsync()
    {
        _live?.Cancel();
        var live = new CancellationTokenSource();
        _live = live;
        try
        {
            await Task.Delay(700, live.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (Current is not { } level)
        {
            return;
        }

        var result = await RunAsync(level);
        if (!live.IsCancellationRequested && Current == level)
        {
            ShowDiagnostics(result);
        }
    }

    private async Task<ChallengeResult?> RunAsync(ChallengeLevel level)
    {
        if (!ChallengeProgress.SaveCode(level.Challenge.Id, Code) || _cliPath() is not { } cliPath)
        {
            return null;
        }

        return await SimulationRunner.RunChallengeAsync(cliPath, ChallengeProgress.CodePath(level.Challenge.Id),
            level.Challenge.Tests.Where(test => test.Kind == "packet"));
    }

    private void ShowDiagnostics(ChallengeResult? result)
    {
        HasCodeErrors = result is null or { Errors: true };
        HasWarnings = result is { Errors: false, Diagnostics.Count: > 0 };
        if (result is null)
        {
            DiagnosticsText = "No se pudo ejecutar NetWall";
            DiagnosticsDetail = string.Empty;
            return;
        }

        var first = result.Diagnostics.FirstOrDefault();
        DiagnosticsDetail = first is null ? string.Empty : $"Línea {first.Line}: {first.Message}";
        if (result.Errors)
        {
            DiagnosticsText = "✕ Tu código tiene un error";
            return;
        }

        var rules = result.Rules?.Count ?? 0;
        var warnings = result.Diagnostics.Count;
        DiagnosticsText = $"Sin errores · {rules} {(rules == 1 ? "regla" : "reglas")} · "
                          + $"{warnings} {(warnings == 1 ? "advertencia" : "advertencias")}";
    }

    private void SaveCode()
    {
        if (Current is not null)
        {
            ChallengeProgress.SaveCode(Current.Challenge.Id, Code);
        }
    }

    private void UpdateUnlocks()
    {
        for (var i = 0; i < Levels.Count; i++)
        {
            Levels[i].IsUnlocked = i == 0 || Levels[i - 1].Fish > 0;
        }
    }

    private static IBrush? Resource(string key) =>
        Application.Current?.TryFindResource(key, out var resource) == true ? resource as IBrush : null;
}
