using System.Collections.Generic;

namespace NetWall.IDE.Services;

public static class ProjectDiagnostics
{
    private const string Red = "\u001b[31m";
    private const string Dim = "\u001b[90m";
    private const string Reset = "\u001b[0m";

    public static string NoMainFile() => Format(
        "ningún archivo del proyecto contiene un bloque 'principal'",
        "Falta escribir principal { } en el archivo que inicia el programa.");

    public static string SeveralMainFiles(IEnumerable<string> files) => Format(
        $"más de un archivo contiene un bloque 'principal': {string.Join(", ", files)}",
        "Solo puede haber un archivo principal por proyecto.");

    private static string Format(string message, string cause) =>
        $"{Red}✖ error_semantico{Reset}: {message}\n" +
        $"  {Dim}={Reset} posible causa: {cause}\n" +
        "\n" +
        $"{Red}✖ 1 error{Reset}\n";
}
