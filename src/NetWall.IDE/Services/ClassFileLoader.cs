using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NetWall.IDE.Models;

namespace NetWall.IDE.Services;

public sealed class ClassFileException(string message) : Exception(message);

public static partial class ClassFileLoader
{
    private const int CurrentVersion = 1;
    private const int MaxExercises = 100;
    private const long MaxBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static ClassFile Load(string path)
    {
        var file = Parse(Read(path));
        Validate(file);
        return file;
    }

    public static void Save(ClassFile file, string path)
    {
        Validate(file);
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(file, WriteOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ClassFileException("No se pudo guardar el archivo.");
        }
    }

    public static void Validate(ClassFile file)
    {
        if (file.Version != CurrentVersion)
        {
            throw new ClassFileException($"Este archivo usa la versión {file.Version} y NetWall solo abre la {CurrentVersion}.");
        }

        if (!IsSafeId(file.Id))
        {
            throw new ClassFileException("El id de la clase solo puede tener letras, números, guiones y guiones bajos.");
        }

        if (string.IsNullOrWhiteSpace(file.Name))
        {
            throw new ClassFileException("La clase necesita un nombre.");
        }

        if (file.Exercises is not { Count: > 0 and <= MaxExercises })
        {
            throw new ClassFileException($"La clase debe tener entre 1 y {MaxExercises} ejercicios.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (exercise, index) in file.Exercises.Select((exercise, index) => (exercise, index)))
        {
            Validate(exercise, index + 1, ids);
        }
    }

    private static string Read(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxBytes)
            {
                throw new ClassFileException("El archivo es demasiado grande para ser una clase.");
            }

            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ClassFileException("No se pudo leer el archivo.");
        }
    }

    private static ClassFile Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<ClassFile>(text, Options)
                   ?? throw new ClassFileException("El archivo está vacío.");
        }
        catch (JsonException)
        {
            throw new ClassFileException("El archivo no tiene el formato de una clase de NetWall.");
        }
    }

    private static void Validate(ClassExercise? exercise, int number, HashSet<string> ids)
    {
        var name = $"Ejercicio {number}";
        if (exercise is null)
        {
            throw new ClassFileException($"{name}: está vacío.");
        }

        if (!IsSafeId(exercise.Id))
        {
            throw new ClassFileException($"{name}: su id solo puede tener letras, números, guiones y guiones bajos.");
        }

        if (!ids.Add(exercise.Id))
        {
            throw new ClassFileException($"{name}: el id {exercise.Id} está repetido.");
        }

        if (string.IsNullOrWhiteSpace(exercise.Title) || string.IsNullOrWhiteSpace(exercise.Statement))
        {
            throw new ClassFileException($"{name}: necesita título y enunciado.");
        }

        if (exercise.Type is not (null or "fix"))
        {
            throw new ClassFileException($"{name}: el tipo {exercise.Type} no existe.");
        }

        if (exercise.Type is not null && string.IsNullOrWhiteSpace(exercise.Starter))
        {
            throw new ClassFileException($"{name}: necesita código inicial.");
        }

        if (exercise.Tests is not { Count: > 0 })
        {
            throw new ClassFileException($"{name}: necesita al menos una prueba.");
        }

        foreach (var test in exercise.Tests)
        {
            ValidateTest(test, name);
        }
    }

    private static void ValidateTest(ChallengeTest? test, string name)
    {
        if (test is null || string.IsNullOrWhiteSpace(test.Label))
        {
            throw new ClassFileException($"{name}: cada prueba necesita una descripción.");
        }

        var valid = test.Kind switch
        {
            "clean" => true,
            "variable" => !string.IsNullOrWhiteSpace(test.Name),
            "policy" => IsAction(test.Expect),
            "packet" => test.Protocol is "tcp" or "udp" && test.Port is >= 0 and <= 65535 && IsAction(test.Expect)
                        && IsHost(test.Source) && IsHost(test.Destination),
            _ => false,
        };
        if (!valid)
        {
            throw new ClassFileException($"{name}: la prueba \"{test.Label}\" está incompleta o no es válida.");
        }
    }

    private static bool IsAction(string? value) => value is "permitir" or "bloquear";

    private static bool IsHost(string? address) => address is not null && Ipv4.IsHost(address);

    private static bool IsSafeId(string? id) => id is not null && SafeId().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex SafeId();
}
