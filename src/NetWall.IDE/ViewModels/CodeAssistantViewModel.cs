using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetWall.IDE.Services;
using NetWall.IDE.Themes;

namespace NetWall.IDE.ViewModels;

public enum AssistantKind
{
    None,
    Rule,
    Variable,
    Packet,
    Import,
    Main,
}

/// <summary>
/// Backs the code assistant dialog: the user picks what to create, fills a form, and the
/// assistant validates it with the same rules as the compiler and generates NetWall code.
/// </summary>
public partial class CodeAssistantViewModel : ViewModelBase
{
    public const string AnyProtocol = "(cualquiera)";

    public const string NoPolicy = "(sin política)";

    public static IReadOnlyList<string> Actions { get; } = ["permitir", "bloquear"];

    public static IReadOnlyList<string> Protocols { get; } = [AnyProtocol, "tcp", "udp"];

    public static IReadOnlyList<string> PacketProtocols { get; } = ["tcp", "udp"];

    public static IReadOnlyList<string> Types { get; } = ["Int", "Float", "String", "Red"];

    public static IReadOnlyList<string> Policies { get; } = ["bloquear", "permitir", NoPolicy];

    private readonly HashSet<string> _importedPaths = [];
    private readonly bool _hasMainBlock;

    public CodeAssistantViewModel() : this(null, null, string.Empty)
    {
    }

    /// <param name="currentFile">The file open in the editor, used to compute import paths.</param>
    /// <param name="projectRoot">The project folder, where importable files are searched.</param>
    /// <param name="documentText">The editor text, to know what is already imported and declared.</param>
    public CodeAssistantViewModel(string? currentFile, string? projectRoot, string documentText)
    {
        ScanDocument(documentText);
        _hasMainBlock = _keywords.Contains("principal");
        ImportFiles = FindImportFiles(currentFile, projectRoot);

        var networks = new Dictionary<string, string>();
        CollectNetworks(documentText, networks);
        foreach (var imported in _importedPaths)
        {
            var path = Path.Combine(Path.GetDirectoryName(currentFile) ?? string.Empty, imported);
            if (currentFile is not null && File.Exists(path))
            {
                CollectNetworks(File.ReadAllText(path), networks);
            }
        }

        NetworkVariables = networks.Keys.Order().ToList();
        HostVariables = NetworkVariables.Where(name => IsHostVariable(name, networks)).ToList();
        Validate();
    }

    /// <summary>Red variables declared in the open file and the files it imports.</summary>
    public IReadOnlyList<string> NetworkVariables { get; }

    /// <summary>The Red variables that hold a single address, valid as a packet's origin or destination.</summary>
    public IReadOnlyList<string> HostVariables { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosing), nameof(IsRuleForm), nameof(IsVariableForm), nameof(IsPacketForm),
        nameof(IsImportForm), nameof(IsMainForm))]
    public partial AssistantKind Kind { get; set; }

    public bool IsChoosing => Kind == AssistantKind.None;

    public bool IsRuleForm => Kind == AssistantKind.Rule;

    public bool IsVariableForm => Kind == AssistantKind.Variable;

    public bool IsPacketForm => Kind == AssistantKind.Packet;

    public bool IsImportForm => Kind == AssistantKind.Import;

    public bool IsMainForm => Kind == AssistantKind.Main;

    public IReadOnlyList<string> ImportFiles { get; }

    [ObservableProperty]
    public partial string? SelectedImport { get; set; }

    [ObservableProperty]
    public partial string MainPolicy { get; set; } = "bloquear";

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
    private void ChooseImport() => Kind = AssistantKind.Import;

    [RelayCommand]
    private void ChooseMain() => Kind = AssistantKind.Main;

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
            case AssistantKind.Import:
                ValidateImport(errors);
                break;
            case AssistantKind.Main when _hasMainBlock:
                errors.Add("el archivo ya tiene un bloque principal");
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

    private void ValidateImport(List<string> errors)
    {
        if (ImportFiles.Count == 0)
        {
            errors.Add("no hay otros archivos .nw en el proyecto");
        }
        else if (SelectedImport is null)
        {
            errors.Add("elige el archivo que quieres importar");
        }
        else if (_importedPaths.Contains(SelectedImport))
        {
            errors.Add("ese archivo ya está importado");
        }
    }

    public string GenerateCode() => Kind switch
    {
        AssistantKind.Rule => GenerateRule(),
        AssistantKind.Variable => GenerateVariable(),
        AssistantKind.Packet => GeneratePacket(),
        AssistantKind.Import => $"importar \"{SelectedImport}\";\n",
        AssistantKind.Main => GenerateMain(),
        _ => string.Empty,
    };

    private string GenerateMain() => MainPolicy == NoPolicy
        ? "principal {\n}\n"
        : $"principal {{\n    politica_defecto = {MainPolicy};\n}}\n";

    private readonly HashSet<string> _keywords = [];

    /// <summary>Collects the keywords and imported paths of the open file, skipping comments and strings.</summary>
    private void ScanDocument(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var spans = NetWallSyntaxScanner.ScanLine(line);
            for (var i = 0; i < spans.Count; i++)
            {
                if (spans[i].Token != SyntaxToken.Keyword)
                {
                    continue;
                }

                var word = line.Substring(spans[i].Start, spans[i].Length);
                _keywords.Add(word);
                if (word == "importar" && i + 1 < spans.Count && spans[i + 1].Token == SyntaxToken.String)
                {
                    _importedPaths.Add(line.Substring(spans[i + 1].Start, spans[i + 1].Length).Trim('"'));
                }
            }
        }
    }

    /// <summary>Finds "declarar name: Red = value;" and stores each name with its value.</summary>
    private static void CollectNetworks(string text, Dictionary<string, string> networks)
    {
        foreach (var line in text.Split('\n'))
        {
            var spans = NetWallSyntaxScanner.ScanLine(line);
            string Word(int i) => line.Substring(spans[i].Start, spans[i].Length);

            for (var i = 0; i + 5 < spans.Count; i++)
            {
                if (Word(i) == "declarar" && spans[i + 1].Token == SyntaxToken.Identifier
                    && Word(i + 2) == ":" && Word(i + 3) == "Red" && Word(i + 4) == "="
                    && spans[i + 5].Token is SyntaxToken.String or SyntaxToken.Identifier)
                {
                    networks[Word(i + 1)] = Word(i + 5).Trim('"');
                }
            }
        }
    }

    private static bool IsHostVariable(string name, Dictionary<string, string> networks)
    {
        var visited = new HashSet<string>();
        while (networks.TryGetValue(name, out var value) && visited.Add(name))
        {
            if (!IsIdentifier(value))
            {
                return IsHost(value);
            }

            name = value;
        }

        return false;
    }

    private static IReadOnlyList<string> FindImportFiles(string? currentFile, string? projectRoot)
    {
        var folder = Path.GetDirectoryName(currentFile);
        if (currentFile is null || folder is null)
        {
            return [];
        }

        var root = projectRoot is not null && Directory.Exists(projectRoot) ? projectRoot : folder;
        return Directory.EnumerateFiles(root, "*.nw", SearchOption.AllDirectories)
            .Where(file => !string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentFile), StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
            .Order()
            .ToList();
    }

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
