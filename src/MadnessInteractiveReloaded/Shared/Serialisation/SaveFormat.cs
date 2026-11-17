using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using Walgelijk;

namespace MIR.Serialisation;

/// <summary>
/// The save types that carry a <see cref="SaveFormat.VersionKey"/>.
/// </summary>
public enum SaveKind
{
    Settings,
    CampaignStats,
    ArenaModeSaves,
    ImprobabilityDisks,
}

/// <summary>
/// Save format versions, and the migration hook that brings an older save up to date on load.
/// <para>
/// These are versions of the file formats, not of the game: <see cref="GameVersion"/> (the assembly version, kept at the MIR
/// base version for mods) and <see cref="Branding.DisplayVersion"/> (IIR's own) can change without touching any save. Bump a
/// format version only when that format changes, and add the step from the previous version to <see cref="Migrate"/>.
/// </para>
/// <para>
/// A save written before versions existed has none and counts as version 0. It is migrated to the current version in memory
/// and written with it on the next save.
/// </para>
/// </summary>
public static class SaveFormat
{
    /// <summary>
    /// The JSON key every JSON save stores its format version under.
    /// </summary>
    public const string VersionKey = "FormatVersion";

    /// <summary><c>settings.json</c>. 1: added the version field.</summary>
    public const int SettingsVersion = 1;

    /// <summary><c>campaigns/&lt;id&gt;.json</c>. 1: added the version field.</summary>
    public const int CampaignStatsVersion = 1;

    /// <summary><c>arena.json</c>. 1: added the version field.</summary>
    public const int ArenaModeSavesVersion = 1;

    /// <summary><c>disks.txt</c>. 1: added a <c>#format 1</c> first line.</summary>
    public const int ImprobabilityDisksVersion = 1;

    /// <summary>
    /// The version line the text formats (<c>.look</c>, <c>.stats</c>, <c>.preset</c>, <c>.armor</c> and the other
    /// key-value files) start with. It used to be <see cref="GameVersion"/>, so files carry MIR game versions up to 0.51.1.
    /// The formats have not changed since, so this stays 0.51.1 and IIR's files stay readable by MIR and the other way
    /// round. When IIR changes a text format, raise it past 0.51.1 (1.0, for example) and compare
    /// <see cref="BaseDeserialiser.Line.Version"/> where the meaning changed. A file with a newer version still loads,
    /// with a warning.
    /// </summary>
    public static readonly Version TextFormatVersion = new(0, 51, 1);

    /// <summary>
    /// The current format version of <paramref name="kind"/>.
    /// </summary>
    public static int CurrentVersion(SaveKind kind) => kind switch
    {
        SaveKind.Settings => SettingsVersion,
        SaveKind.CampaignStats => CampaignStatsVersion,
        SaveKind.ArenaModeSaves => ArenaModeSavesVersion,
        SaveKind.ImprobabilityDisks => ImprobabilityDisksVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// The format version stored in <paramref name="data"/>, or 0 when it has none (saves from before versions existed).
    /// </summary>
    public static int ReadVersion(JObject data)
        => data.TryGetValue(VersionKey, out var token) && token.Type == JTokenType.Integer ? token.Value<int>() : 0;

    /// <summary>
    /// Brings <paramref name="data"/> from <paramref name="fromVersion"/> up to the current version of
    /// <paramref name="kind"/>, step by step, and stamps the current version on it. A save from a newer format is loaded as
    /// it is, with a warning: fields this build does not know are dropped on the next save.
    /// </summary>
    public static JObject Migrate(SaveKind kind, int fromVersion, JObject data)
    {
        var current = CurrentVersion(kind);
        if (fromVersion > current)
            Logger.Warn($"{kind} save has format version {fromVersion}, newer than this build's {current}. Loading what this build understands.");

        // Steps go here, oldest first, e.g.
        //   if (kind == SaveKind.Settings && fromVersion < 2) { ...change data from 1 to 2... }
        // 0 -> 1 only added the version field, so there is nothing to change for any kind.

        data[VersionKey] = current;
        return data;
    }

    /// <summary>
    /// Parses a JSON save, migrates it (<see cref="Migrate"/>) and deserialises it, the way <c>JsonConvert.DeserializeObject</c>
    /// did before. Strings are not turned into dates, so time spans and other strings survive the round trip unchanged.
    /// </summary>
    /// <exception cref="JsonException">The text is not a JSON object.</exception>
    public static T DeserialiseJson<T>(string json, SaveKind kind)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var data = JObject.Load(reader);
        if (reader.Read() && reader.TokenType != JsonToken.Comment)
            throw new JsonReaderException("Unexpected content after the JSON object");

        var migrated = Migrate(kind, ReadVersion(data), data);
        return migrated.ToObject<T>() ?? throw new JsonException("The save deserialised to null");
    }
}
