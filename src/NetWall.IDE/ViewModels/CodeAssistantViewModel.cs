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
    Variable,
    Packet,
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

    public static IReadOnlyList<string> PacketProtocols { get; } = ["tcp", "udp"];

    public static IReadOnlyList<string> Types { get; } = ["Int", "Float", "String", "Red"];

    public CodeAssistantViewModel() => Validate();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosing), nameof(IsRuleForm), nameof(IsVariableForm), nameof(IsPacketForm))]
    public partial AssistantKind Kind { get; set; }

    public bool IsChoosing => Kind == AssistantKind.None;

    public bool IsRuleForm => Kind == AssistantKind.Rule;

    public bool IsVariableForm => Kind == AssistantKind.Variable;

    public bool IsPacketForm => Kind == AssistantKind.Packet;

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
    public partial string VariableName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VariableType { get; set; } = "Int";

    [ObservableProperty]
    public partial string VariableValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PacketProtocol { get; set; } = "tcp";

    [ObservableProperty]
    public partial string PacketPort { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PacketSource { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PacketDestination { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string Errors { get; set; } = string.Empty;

    public bool HasErrors => Errors.Length > 0;

    [ObservableProperty]
    public partial bool CanInsert { get; set; }

    [RelayCommand]
    private void ChooseRule() => Kind = AssistantKind.Rule;

    [RelayCommand]
    private void ChooseVariable() => Kind = AssistantKind.Variable;

    [RelayCommand]
    private void ChoosePacket() => Kind = AssistantKind.Packet;

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
        switch (Kind)
        {
            case AssistantKind.Rule:
                ValidateRule(errors);
                break;
            case AssistantKind.Variable:
                ValidateVariable(errors);
                break;
            case AssistantKind.Packet:
                ValidatePacket(errors);
                break;
        }

        Errors = string.Join("\n", errors.Select(error => $"⚠ {error}"));
        CanInsert = Kind != AssistantKind.None && errors.Count == 0;
    }

    private void ValidateRule(List<string> errors)
    {
        AddNameError(errors, RuleName);

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

    private void ValidateVariable(List<string> errors)
    {
        AddNameError(errors, VariableName);

        var value = VariableValue.Trim();
        if (value.Length == 0)
        {
            errors.Add("el valor es obligatorio");
            return;
        }

        switch (VariableType)
        {
            case "Int" when !IsInt(value) && !IsVariable(value):
                errors.Add("el valor de un Int debe ser un número entero o una variable");
                break;
            case "Float" when !IsFloat(value) && !IsVariable(value):
                errors.Add("el valor de un Float debe ser un número (ej. 2.5) o una variable");
                break;
            case "String" when value.Trim('"').Contains('"'):
                errors.Add("el texto no puede contener comillas");
                break;
            case "Red":
                AddAddressError(errors, value, "valor", allowAny: false);
                break;
        }
    }

    private void ValidatePacket(List<string> errors)
    {
        if (ParsePort(PacketPort) is null)
        {
            errors.Add("el puerto debe ser un número entre 0 y 65535");
        }

        AddHostError(errors, PacketSource, "origen");
        AddHostError(errors, PacketDestination, "destino");
    }

    public string GenerateCode() => Kind switch
    {
        AssistantKind.Rule => GenerateRule(),
        AssistantKind.Variable => GenerateVariable(),
        AssistantKind.Packet => GeneratePacket(),
        _ => string.Empty,
    };

    private string GenerateRule()
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

        return $"regla {RuleName.Trim()} {Block(fields)};\n";
    }

    private string GenerateVariable()
    {
        var value = VariableValue.Trim();
        var formatted = VariableType switch
        {
            "String" => $"\"{value.Trim('"')}\"",
            "Red" => FormatAddress(value),
            _ => value,
        };
        return $"declarar {VariableName.Trim()}: {VariableType} = {formatted};\n";
    }

    private string GeneratePacket()
    {
        var fields = new List<string>
        {
            $"protocolo = {PacketProtocol}",
            $"puerto = {ParsePort(PacketPort)}",
            $"origen = {FormatAddress(PacketSource)}",
            $"destino = {FormatAddress(PacketDestination)}",
        };
        return $"simular Paquete {Block(fields)};\n";
    }

    private static string Block(List<string> fields) =>
        $"{{\n{string.Join(",\n", fields.Select(field => $"    {field}"))}\n}}";

    private static void AddNameError(List<string> errors, string text)
    {
        var name = text.Trim();
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
    }

    private static void AddAddressError(List<string> errors, string text, string fieldName, bool allowAny = true)
    {
        var value = Unquote(text);
        if (value.Length == 0 || (allowAny && value == "cualquiera") || IsNetwork(value) || IsVariable(value))
        {
            return;
        }

        errors.Add(allowAny
            ? $"el {fieldName} debe ser una red (ej. 10.0.0.0/8), una variable o 'cualquiera'"
            : $"el {fieldName} debe ser una red (ej. 10.0.0.0/8) o una variable");
    }

    private static void AddHostError(List<string> errors, string text, string fieldName)
    {
        var value = Unquote(text);
        if (value.Length == 0)
        {
            errors.Add($"el {fieldName} es obligatorio");
        }
        else if (!IsVariable(value) && !IsHost(value))
        {
            errors.Add($"el {fieldName} debe ser una sola dirección (ej. 10.0.0.5) o una variable");
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

    private static bool IsInt(string text) => text.Length > 0 && text.All(char.IsDigit);

    private static bool IsFloat(string text)
    {
        var parts = text.Split('.');
        return IsInt(parts[0]) && (parts.Length == 1 || (parts.Length == 2 && IsInt(parts[1])));
    }

    private static bool IsIdentifier(string text) =>
        text.Length > 0 && char.IsLetter(text[0]) && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static bool IsVariable(string text) => IsIdentifier(text) && !NetWallSyntaxScanner.IsReserved(text);

    private static bool IsHost(string text) => IsNetwork(text) && (!text.Contains('/') || text.EndsWith("/32"));

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
