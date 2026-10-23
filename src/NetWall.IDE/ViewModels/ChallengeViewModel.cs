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
    public partial int Seal { get; set; }

    [ObservableProperty]
    public partial bool IsUnlocked { get; set; }
}

public sealed partial class ChallengeTestCard(ChallengeTest test) : ObservableObject
{
    public ChallengeTest Test { get; } = test;

    public string Title => Test.Label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassed), nameof(IsFailed), nameof(StateText))]
    public partial ChallengeTestState State { get; set; }

    public bool IsPassed => State == ChallengeTestState.Passed;

    public bool IsFailed => State == ChallengeTestState.Failed;

    public string StateText => State switch
    {
        ChallengeTestState.Passed => "✓",
        ChallengeTestState.Failed => "✕",
        _ => "·",
    };
}

public sealed record ChallengeOutcome(ChallengeTest Test, bool Passed, bool? Allowed, string? Rule);

public enum NotebookPageKind
{
    Index,
    Cover,
    Level,
}

public sealed record NotebookPage(NotebookPageKind Kind, ChallengeChapter Chapter, ChallengeLevel? Level);

public sealed partial class ChallengeChapter(ChallengeModule module, int number, IBrush? brush,
    IReadOnlyList<ChallengeLevel> levels) : ObservableObject
{
    public ChallengeModule Module { get; } = module;

    public int Number { get; } = number;

    public IBrush? Brush { get; } = brush;

    public IReadOnlyList<ChallengeLevel> Levels { get; } = levels;

    public string Title => Levels.Count == 1 ? "Examen final" : $"Capítulo {Number}";

    public string Name => Module.Name;

    public string Description => Module.Description ?? string.Empty;

    public string ProgressText => $"{Levels.Count(level => level.Seal > 0)} de {Levels.Count}";

    public bool IsUnlocked => Levels[0].IsUnlocked;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsUnlocked));
    }
}

public partial class ChallengeViewModel : ViewModelBase
{
    private readonly Func<string?> _cliPath;
    private readonly Dictionary<string, int> _seals;
    private CancellationTokenSource? _live;
    private int _attempts;
    private bool _usedHint;

    public ChallengeViewModel(Func<string?> cliPath)
    {
        _cliPath = cliPath;
        _seals = ChallengeProgress.LoadSeals();
        var number = 0;
        foreach (var module in ChallengeCatalog.Load())
        {
            var levels = module.Challenges.Select(challenge => new ChallengeLevel(challenge, module, ++number,
                Resource(module.Brush))
            {
                Seal = _seals.GetValueOrDefault(challenge.Id),
            }).ToList();
            foreach (var level in levels)
            {
                Levels.Add(level);
            }

            Chapters.Add(new ChallengeChapter(module, Chapters.Count + 1, Resource(module.Brush), levels));
        }

        UpdateUnlocks();
        Pages.Add(new NotebookPage(NotebookPageKind.Index, Chapters[0], null));
        foreach (var chapter in Chapters)
        {
            Pages.Add(new NotebookPage(NotebookPageKind.Cover, chapter, null));
            Pages.AddRange(chapter.Levels.Select(level => new NotebookPage(NotebookPageKind.Level, chapter, level)));
        }
    }

    public ObservableCollection<ChallengeChapter> Chapters { get; } = [];

    public List<NotebookPage> Pages { get; } = [];

    public IEnumerable<ChallengeChapter> IndexLeft => Chapters.Take(2);

    public IEnumerable<ChallengeChapter> IndexRight => Chapters.Skip(2);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndexPage), nameof(IsCoverPage), nameof(IsLevelPage), nameof(Chapter),
        nameof(HeaderText), nameof(CanGoBack), nameof(CanGoForward), nameof(PagesBefore), nameof(PagesAfter),
        nameof(CoverDoodle))]
    public partial NotebookPage? Page { get; set; }

    public bool IsIndexPage => Page?.Kind == NotebookPageKind.Index;

    public bool IsCoverPage => Page?.Kind == NotebookPageKind.Cover;

    public bool IsLevelPage => Page?.Kind == NotebookPageKind.Level;

    public ChallengeChapter? Chapter => Page?.Kind == NotebookPageKind.Index ? null : Page?.Chapter;

    public string HeaderText => Page?.Kind switch
    {
        NotebookPageKind.Index => "Índice",
        NotebookPageKind.Cover => $"{Page.Chapter.Title} · {Page.Chapter.Name}",
        _ => Current?.Heading ?? string.Empty,
    };

    public DoodleKind CoverDoodle => (DoodleKind)((Page?.Chapter.Number ?? 0) % 5);

    private int PageIndex => Page is null ? 0 : Pages.IndexOf(Page);

    public int PagesBefore => Math.Min(5, (PageIndex + 1) / 2);

    public int PagesAfter => Math.Min(5, (Pages.Count - PageIndex) / 2);


    public ObservableCollection<ChallengeLevel> Levels { get; } = [];

    public ObservableCollection<ChallengeTestCard> Tests { get; } = [];

    public event Action<string>? CodeReplaced;

    public event Action<string, BubbleTone>? HelpySpoke;

    public event Action<int>? SealAwarded;

    public Func<IReadOnlyList<ChallengeOutcome>, Task>? Presenter { get; set; }

    public string Code { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChapterText),
        nameof(IsPredict), nameof(IsWriting), nameof(Question), nameof(TypeLabel),
        nameof(TypeText), nameof(TypeBrush), nameof(Learn), nameof(HasLesson), nameof(DoodleTop),
        nameof(DoodleBottom))]
    public partial ChallengeLevel? Current { get; set; }

    public bool IsPredict => Current?.Challenge.IsPredict == true;

    public bool IsWriting => !IsPredict;

    public bool HasLesson => Current?.Challenge.Lesson is not null;

    public string Question => IsPredict ? Current!.Challenge.Tests[0].Label : string.Empty;

    public IReadOnlyList<string> Learn => Current?.Challenge.Learn ?? [];

    public DoodleKind DoodleTop => (DoodleKind)((Current?.Number ?? 0) % 5);

    public DoodleKind DoodleBottom => (DoodleKind)(((Current?.Number ?? 0) + 2) % 5);

    public string ChapterText => Current is null ? string.Empty
        : $"Reto {Current.Number} de {Levels.Count} · {Current.Module.Name}";

    public string TypeLabel => Current?.Challenge.Type switch
    {
        "predict" => "Predice",
        "fix" => "Arregla",
        _ => "Escribe",
    };

    public string TypeText => Current?.Challenge.Type switch
    {
        "predict" => "No escribes código: lees un firewall y decides qué le pasa a un paquete.",
        "fix" => "Este código tiene un problema. Encuéntralo y corrígelo sin empezar de cero.",
        _ => "Escribe el código que pide el reto y presiona Comprobar para probarlo.",
    };

    public IBrush? TypeBrush => Resource(Current?.Challenge.Type switch
    {
        "predict" => "PastelPeachBrush",
        "fix" => "PastelLavenderBrush",
        _ => "PastelMintBrush",
    });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string HintText { get; set; } = string.Empty;

    public bool HasHint => HintText.Length > 0;

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
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(AnswerCommand))]
    public partial bool IsChecking { get; set; }

    public bool CanGoBack => PageIndex > 0;

    public bool CanGoForward => PageIndex < Pages.Count - 1 && IsOpen(Pages[PageIndex + 1]);

    public bool IsOpen(NotebookPage page) => page.Kind switch
    {
        NotebookPageKind.Index => true,
        NotebookPageKind.Cover => page.Chapter.IsUnlocked,
        _ => page.Level!.IsUnlocked,
    };

    public NotebookPage PageOf(ChallengeLevel level) => Pages.First(page => page.Level == level);

    public NotebookPage CoverOf(ChallengeChapter chapter) =>
        Pages.First(page => page.Kind == NotebookPageKind.Cover && page.Chapter == chapter);

    public bool IsAfterCurrent(NotebookPage page) => Pages.IndexOf(page) > PageIndex;

    public void OpenPage(NotebookPage page)
    {
        if (!IsOpen(page) || page == Page)
        {
            return;
        }

        if (page.Level is { } level)
        {
            Select(level);
            return;
        }

        _live?.Cancel();
        SaveCode();
        Current = null;
        Tests.Clear();
        HintText = string.Empty;
        DiagnosticsText = string.Empty;
        DiagnosticsDetail = string.Empty;
        SetPage(page);
    }

    public ChallengeLevel StartOf(ChallengeChapter chapter) =>
        chapter.Levels.FirstOrDefault(level => level is { IsUnlocked: true, Seal: 0 }) ?? chapter.Levels[0];

    private void SetPage(NotebookPage page)
    {
        Page = page;
        foreach (var chapter in Chapters)
        {
            chapter.IsActive = page.Kind != NotebookPageKind.Index && chapter == page.Chapter;
        }
    }

    public int SealCount => Levels.Count(level => level.Seal > 0);

    public string ProgressText => $"{SealCount} de {Levels.Count} retos";

    public void Open() =>
        Select(Levels.FirstOrDefault(level => level is { IsUnlocked: true, Seal: 0 })
               ?? Levels.LastOrDefault(level => level.IsUnlocked));

    public void Close()
    {
        _live?.Cancel();
        SaveCode();
    }

    public void OpenLevel(ChallengeLevel level)
    {
        if (level.IsUnlocked)
        {
            Select(level);
        }
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
            OpenPage(Pages[PageIndex - 1]);
        }
    }

    public void GoForward()
    {
        if (CanGoForward)
        {
            OpenPage(Pages[PageIndex + 1]);
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
        SetPage(PageOf(level));
        Tests.Clear();
        foreach (var test in level.Challenge.Tests)
        {
            Tests.Add(new ChallengeTestCard(test));
        }

        _attempts = 0;
        _usedHint = false;
        HintText = string.Empty;

        var saved = level.Challenge.IsPredict ? null : ChallengeProgress.LoadCode(level.Challenge.Id);
        Code = saved ?? level.Challenge.Starter;
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
            _usedHint = true;
            HintText = Current.Challenge.Hint;
            HelpySpoke?.Invoke("Te dejé una pista en una nota. ¡Tú puedes!", BubbleTone.Neutral);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task AnswerAsync(string? choice)
    {
        if (Current is not { Challenge.IsPredict: true } level)
        {
            return;
        }

        IsChecking = true;
        var result = await RunAsync(level);
        IsChecking = false;
        if (Current != level)
        {
            return;
        }

        ShowDiagnostics(result);
        if (result is not { Errors: false, Packets: [var packet] })
        {
            HelpySpoke?.Invoke("No pude ejecutar NetWall. Revisa que Python esté instalado.", BubbleTone.Blocked);
            return;
        }

        _attempts++;
        var correct = packet.Action == choice;
        var test = level.Challenge.Tests[0] with
        {
            Label = choice == "permitir" ? "Tu respuesta: pasa" : "Tu respuesta: se bloquea",
        };
        if (Presenter is not null)
        {
            IsChecking = true;
            await Presenter([new ChallengeOutcome(test, correct, packet.IsAllowed, packet.Rule)]);
            IsChecking = false;
            if (Current != level)
            {
                return;
            }
        }

        Tests[0].State = correct ? ChallengeTestState.Passed : ChallengeTestState.Failed;
        if (!correct)
        {
            HelpySpoke?.Invoke($"No exactamente. {Explain(packet)} Inténtalo otra vez.", BubbleTone.Blocked);
            return;
        }

        var perfect = _attempts == 1 && !_usedHint;
        Award(level, perfect ? 2 : 1);
        HelpySpoke?.Invoke($"¡Correcto! {Explain(packet)} "
                           + (perfect ? "Ganaste el sello dorado." : "Ganaste tu sello."), BubbleTone.Allowed);
    }

    private static string Explain(SimulatedPacket packet)
    {
        var verdict = packet.IsAllowed ? "pasa" : "se bloquea";
        if (packet.Rule is not null)
        {
            var before = packet.Skipped.FirstOrDefault() is { } skipped
                ? $" Antes, {skipped.Rule} no coincidió ({skipped.Reason})."
                : string.Empty;
            return $"La primera regla que coincide es {packet.Rule}, así que el paquete {verdict}.{before}";
        }

        var reason = packet.Skipped.FirstOrDefault() is { } first ? $" ({first.Reason})" : string.Empty;
        return $"Ninguna regla coincide{reason}, así que se aplica la política por defecto y el paquete {verdict}.";
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
        if (Current is not { Challenge.IsPredict: false } level)
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
        var outcomes = new List<ChallengeOutcome>();
        foreach (var card in Tests)
        {
            var packet = card.Test.Kind == "packet" && next < packets.Count ? packets[next] : null;
            outcomes.Add(new ChallengeOutcome(card.Test, Passes(card.Test, result, packets, ref next), packet?.IsAllowed,
                packet?.Rule));
        }

        if (Presenter is not null)
        {
            IsChecking = true;
            await Presenter(outcomes);
            IsChecking = false;
            if (Current != level)
            {
                return;
            }
        }

        for (var i = 0; i < Tests.Count; i++)
        {
            Tests[i].State = outcomes[i].Passed ? ChallengeTestState.Passed : ChallengeTestState.Failed;
        }

        if (Tests.FirstOrDefault(card => card.IsFailed) is { } failed)
        {
            HelpySpoke?.Invoke(failed.Test.Failure ?? $"Todavía no se cumple: {failed.Test.Label}.",
                BubbleTone.Blocked);
            return;
        }

        var missing = new List<string>();
        if (_usedHint)
        {
            missing.Add("no pidas pista");
        }

        if (level.Challenge.MaxRules is { } max && (result.Rules?.Count ?? 0) > max)
        {
            missing.Add(max == 1 ? "usa una sola regla" : $"usa {max} reglas o menos");
        }

        if (result.Diagnostics.Count > 0)
        {
            missing.Add("corrige las advertencias del analizador");
        }

        Award(level, missing.Count == 0 ? 2 : 1);
        var after = level.Number < Levels.Count ? " Ya puedes pasar la página." : " ¡Terminaste el cuaderno!";
        HelpySpoke?.Invoke(missing.Count == 0
            ? $"¡Perfecto! Ganaste el sello dorado.{after}"
            : $"¡Lo lograste! Ganaste tu sello. Para el dorado: {string.Join(", ", missing)}.", BubbleTone.Allowed);
    }

    private void Award(ChallengeLevel level, int seal)
    {
        if (seal <= level.Seal)
        {
            return;
        }

        level.Seal = seal;
        _seals[level.Challenge.Id] = seal;
        ChallengeProgress.SaveSeals(_seals);
        UpdateUnlocks();
        foreach (var chapter in Chapters)
        {
            chapter.Refresh();
        }

        OnPropertyChanged(nameof(SealCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CanGoForward));
        SealAwarded?.Invoke(seal);
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
        if (Current is { Challenge.IsPredict: false })
        {
            ChallengeProgress.SaveCode(Current.Challenge.Id, Code);
        }
    }

    private void UpdateUnlocks()
    {
        for (var i = 0; i < Levels.Count; i++)
        {
            Levels[i].IsUnlocked = i == 0 || Levels[i - 1].Seal > 0;
        }
    }

    private static IBrush? Resource(string key) =>
        Application.Current?.TryFindResource(key, out var resource) == true ? resource as IBrush : null;
}
