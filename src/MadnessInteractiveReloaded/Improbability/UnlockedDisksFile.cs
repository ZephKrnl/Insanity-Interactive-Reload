using MIR.Serialisation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walgelijk;

namespace MIR;

/// <summary>
/// <c>disks.txt</c>: one unlocked Improbability disk ID per line. Since format 1 the first line is <c>#format 1</c>; a file
/// without it (MIR's) is format 0 and reads the same. Lines starting with <c>#</c> and blank lines are not IDs, so the
/// version line is harmless to MIR, which reads it as an unknown ID.
/// </summary>
public static class UnlockedDisksFile
{
    public const string VersionPrefix = "#format ";

    /// <summary>
    /// The file's text for <paramref name="ids"/>.
    /// </summary>
    public static string Serialise(IEnumerable<string> ids)
    {
        var b = new StringBuilder();
        b.Append(VersionPrefix).Append(SaveFormat.ImprobabilityDisksVersion).Append(Environment.NewLine);
        foreach (var id in ids)
            if (!string.IsNullOrWhiteSpace(id))
                b.Append(id.Trim()).Append(Environment.NewLine);
        return b.ToString();
    }

    /// <summary>
    /// The IDs in <paramref name="text"/>, in file order.
    /// </summary>
    public static string[] Parse(string text)
    {
        var ids = new List<string>();
        var version = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith('#'))
            {
                if (line.StartsWith(VersionPrefix) && int.TryParse(line[VersionPrefix.Length..], out var v))
                    version = v;
                continue;
            }
            ids.Add(line);
        }

        if (version > SaveFormat.ImprobabilityDisksVersion)
            Logger.Warn($"disks.txt has format version {version}, newer than this build's {SaveFormat.ImprobabilityDisksVersion}. Reading the IDs it has.");
        // 0 -> 1 only added the version line: the IDs are the same.

        return [.. ids];
    }

    /// <summary>
    /// Writes <paramref name="ids"/> to <paramref name="path"/>, atomically, keeping a <c>.bak</c>. Never throws.
    /// </summary>
    /// <returns>True when it was written.</returns>
    public static bool Save(string path, IEnumerable<string> ids)
        => SaveFile.TryWriteAllText(path, Serialise(ids), "unlocked Improbability disks");

    /// <summary>
    /// The IDs in <paramref name="path"/>, falling back to its <c>.bak</c>; empty when there is neither.
    /// </summary>
    public static string[] Load(string path)
    {
        SaveFile.TryLoad(path, Parse, out var ids, "unlocked Improbability disks");
        return ids ?? [];
    }
}
