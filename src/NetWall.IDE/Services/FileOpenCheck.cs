using System;
using System.IO;

namespace NetWall.IDE.Services;

public enum FileOpenRisk
{
    None,
    TooLarge,
    Binary,
}

public static class FileOpenCheck
{
    public const long MaxComfortableSize = 2 * 1024 * 1024;
    private const int SampleSize = 8 * 1024;

    public static FileOpenRisk Inspect(string path, out long size)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        size = stream.Length;

        var sample = new byte[(int)Math.Min(SampleSize, size)];
        var read = stream.ReadAtLeast(sample, sample.Length, throwOnEndOfStream: false);
        if (LooksBinary(sample.AsSpan(0, read)))
        {
            return FileOpenRisk.Binary;
        }

        return size > MaxComfortableSize ? FileOpenRisk.TooLarge : FileOpenRisk.None;
    }

    private static bool LooksBinary(ReadOnlySpan<byte> sample)
    {
        if (sample.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || sample.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return false;
        }

        return sample.IndexOf((byte)0) >= 0;
    }

    public static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):0.#} MB" : $"{bytes / 1024.0:0.#} KB";
}
