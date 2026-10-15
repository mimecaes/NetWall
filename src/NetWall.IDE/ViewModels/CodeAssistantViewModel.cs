using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Services;

namespace NetWall.IDE.ViewModels;

public enum AssistantKind
{
    None,
    Rule,
}

/// <summary>
/// Backs the code assistant dialog: the user picks what to create, fills a form, and the
/// assistant validates it with the same rules as the compiler and generates NetWall code.
/// </summary>
public partial class CodeAssistantViewModel : ViewModelBase
{
    public const string AnyProtocol = "(cualquiera)";

    public static IReadOnlyList<string> Actions { get; } = ["permitir", "bloquear"];

    public static IReadOnlyList<string> Protocols { get; } = [AnyProtocol, "tcp", "udp"];

    public CodeAssistantViewModel() => Validate();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosing), nameof(IsRuleForm))]
    public partial AssistantKind Kind { get; set; }

    public bool IsChoosing => Kind == AssistantKind.None;

    public bool IsRuleForm => Kind == AssistantKind.Rule;

    [ObservableProperty]
    public partial string RuleName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Action { get; set; } = "permitir";

    [ObservableProperty]
    public partial string Protocol { get; set; } = AnyProtocol;

    [ObservableProperty]
    public partial bool HasNoPort { get; set; } = true;

    [ObservableProperty]
    public partial bool HasSinglePort { get; set; }

    [ObservableProperty]
    public partial bool HasPortRange { get; set; }

    [ObservableProperty]
    public partial string Port { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PortFrom { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PortTo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Source { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Destination { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Comment { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string Errors { get; set; } = string.Empty;

    public bool HasErrors => Errors.Length > 0;

    [ObservableProperty]
    public partial bool CanInsert { get; set; }

    [RelayCommand]
    private void ChooseRule() => Kind = AssistantKind.Rule;

    [RelayCommand]
    private void Back() => Kind = AssistantKind.None;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not (nameof(Errors) or nameof(HasErrors) or nameof(CanInsert)))
        {
            Validate();
        }
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (Kind == AssistantKind.Rule)
        {
            ValidateRule(errors);
        }

        Errors = string.Join("\n", errors.Select(error => $"⚠ {error}"));
        CanInsert = Kind != AssistantKind.None && errors.Count == 0;
    }

    private void ValidateRule(List<string> errors)
    {
        var name = RuleName.Trim();
        if (name.Length == 0)
        {
            errors.Add("el nombre es obligatorio");
        }
        else if (!IsIdentifier(name))
        {
            errors.Add("el nombre solo puede tener letras, números y '_', y debe empezar con una letra");
        }
        else if (NetWallSyntaxScanner.IsReserved(name))
        {
            errors.Add($"'{name}' es una palabra reservada");
        }

        if (HasSinglePort && ParsePort(Port) is null)
        {
            errors.Add("el puerto debe ser un número entre 0 y 65535");
        }

        if (HasPortRange)
        {
            var from = ParsePort(PortFrom);
            var to = ParsePort(PortTo);
            if (from is null || to is null)
            {
                errors.Add("los extremos del rango deben ser números entre 0 y 65535");
            }
            else if (from > to)
            {
                errors.Add("el inicio del rango no puede ser mayor que el final");
            }
        }

        AddAddressError(errors, Source, "origen");
        AddAddressError(errors, Destination, "destino");

        if (Comment.Contains('"'))
        {
            errors.Add("el comentario no puede contener comillas");
        }
    }

    public string GenerateCode()
    {
        var fields = new List<string> { $"accion = {Action}" };
        if (Protocol != AnyProtocol)
        {
            fields.Add($"protocolo = {Protocol}");
        }

        if (HasSinglePort)
        {
            fields.Add($"puerto = {ParsePort(Port)}");
        }
        else if (HasPortRange)
        {
            fields.Add($"puerto = rango({ParsePort(PortFrom)}, {ParsePort(PortTo)})");
        }

        if (FormatAddress(Source) is { } source)
        {
            fields.Add($"origen = {source}");
        }

        if (FormatAddress(Destination) is { } destination)
        {
            fields.Add($"destino = {destination}");
        }

        if (!string.IsNullOrWhiteSpace(Comment))
        {
            fields.Add($"comentario = \"{Comment.Trim()}\"");
        }

        var body = string.Join(",\n", fields.Select(field => $"    {field}"));
        return $"regla {RuleName.Trim()} {{\n{body}\n}};\n";
    }

    private static void AddAddressError(List<string> errors, string text, string fieldName)
    {
        var value = Unquote(text);
        if (value.Length == 0 || value == "cualquiera" || IsNetwork(value))
        {
            return;
        }

        if (!IsIdentifier(value))
        {
            errors.Add($"el {fieldName} debe ser una red (ej. 10.0.0.0/8), una variable o 'cualquiera'");
        }
        else if (NetWallSyntaxScanner.IsReserved(value))
        {
            errors.Add($"el {fieldName} no puede ser la palabra reservada '{value}'");
        }
    }

    private static string? FormatAddress(string text)
    {
        var value = Unquote(text);
        if (value.Length == 0)
        {
            return null;
        }

        return value == "cualquiera" || IsIdentifier(value) ? value : $"\"{value}\"";
    }

    private static string Unquote(string text) => text.Trim().Trim('"').Trim();

    private static int? ParsePort(string text) =>
        int.TryParse(text.Trim(), out var port) && port is >= 0 and <= 65535 ? port : null;

    private static bool IsIdentifier(string text) =>
        text.Length > 0 && char.IsLetter(text[0]) && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static bool IsNetwork(string text)
    {
        var parts = text.Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork || parts[0].Count(c => c == '.') != 3)
        {
            return false;
        }

        return parts.Length == 1 || (int.TryParse(parts[1], out var mask) && mask is >= 0 and <= 32);
    }
}
