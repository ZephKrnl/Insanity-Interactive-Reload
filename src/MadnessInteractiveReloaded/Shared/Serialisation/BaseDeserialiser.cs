using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Walgelijk;

namespace MIR.Serialisation;

/// <summary>
/// All custom deserialisers should inherit the behaviour of this deserialiser.
/// </summary>
public static class BaseDeserialiser
{
    public const string CommentPrefix = "#";
    public record Line(string String, Version Version, int LineNumber, string Untrimmed);

    /// <exception cref="Exceptions.SerialisationException"></exception>
    public static IEnumerable<Line> Read(StreamReader input) => Read(input, null);

    /// <summary>
    /// Reads the lines after the version line. The version is compared with <see cref="SaveFormat.TextFormatVersion"/>: an
    /// older one (any MIR version up to 0.51.1) reads as it is, a newer one is read anyway with a warning, because this build
    /// may not understand all of it. Each <see cref="Line"/> carries the version for format-dependent parsing.
    /// </summary>
    /// <param name="input">The text, starting with the version line.</param>
    /// <param name="location">The file, for the warning.</param>
    /// <exception cref="Exceptions.SerialisationException"></exception>
    public static IEnumerable<Line> Read(StreamReader input, string? location)
    {
        var expectedVersion = input.ReadLine();
        int ln = 2;

        if (!Version.TryParse(expectedVersion, out var version))
            throw new Exceptions.SerialisationException($"Expected version at the first line, got '{expectedVersion}' instead");

        if (version > SaveFormat.TextFormatVersion)
            Logger.Warn($"\"{location ?? "A text file"}\" has format version {version}, newer than this build's {SaveFormat.TextFormatVersion}. Reading what this build understands.");

        while (true)
        {
            var line = input.ReadLine();
            if (line == null)
                yield break;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith(CommentPrefix))
                continue;

            yield return new(line.Trim(), version, ln, line);

            ln++;
        }
    }

    /// <summary>
    /// Save something to disk, atomically (see <see cref="SaveFile"/>), starting with <see cref="SaveFormat.TextFormatVersion"/>.
    /// No backup is kept, because this writes editor files into folders that are packed or listed as assets.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="lines">The lines after the version line; blank ones are skipped.</param>
    /// <returns></returns>
    public static bool Write(string path, IEnumerable<string> lines)
    {
        var writer = new StringBuilder();
        writer.AppendLine(SaveFormat.TextFormatVersion.ToString());
        writer.AppendLine();

        foreach (var l in lines)
        {
            if (l == null || string.IsNullOrWhiteSpace(l))
                continue;

            writer.AppendLineFormat(l.Trim());
        }

        SaveFile.WriteAllText(path, writer.ToString(), keepBackup: false);

        return true;
    }
}
