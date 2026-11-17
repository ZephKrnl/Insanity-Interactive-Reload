using MIR.Serialisation;
using Newtonsoft.Json;
using System;
using System.Buffers.Text;
using System.Globalization;
using System.IO;
using System.Text;
using Walgelijk.AssetManager.Deserialisers;
using Walgelijk.AssetManager;

namespace MIR;

/// <summary>
/// Load character presets from a file.
/// <see cref="CharacterLook"/>
/// <seealso cref="CharacterStats"/>
/// </summary>
public static class CharacterPresetDeserialiser
{
    private const string dataPrefix = "data:";
    private static readonly KeyValueDeserialiser<ExperimentCharacterPreset> deserialiser;

    static CharacterPresetDeserialiser()
    {
        deserialiser = new KeyValueDeserialiser<ExperimentCharacterPreset>(nameof(CharacterPresetDeserialiser));
        deserialiser.RegisterString("name", (i, v) => i.Name = v);

        // Embedded looks and stats are whole .look and .stats files in base64. They are read from memory; this used to go
        // through a temporary file per preset, which was never deleted.
        deserialiser.RegisterString("look", (i, v) =>
        {
            if (v.StartsWith(dataPrefix))
            {
                using var data = new MemoryStream(Convert.FromBase64String(v[dataPrefix.Length..]));
                i.Look = CharacterLookDeserialiser.Load(data, "embedded look");
            }
            else
                i.Look = Registries.Looks.Get(v);
        });

        deserialiser.RegisterString("stats", (i, v) =>
        {
            if (v.StartsWith(dataPrefix))
            {
                using var data = new MemoryStream(Convert.FromBase64String(v[dataPrefix.Length..]));
                i.Stats = CharacterStatsDeserialiser.Load(data, "embedded stats");
            }
            else
                i.Stats = Registries.Stats.Get(v);
        });
    }

    public static ExperimentCharacterPreset Load(string path) => deserialiser.Deserialise(path);

    /// <summary>
    /// Save a preset, atomically, keeping the previous file as <c>.bak</c> (see <see cref="SaveFile"/>). A look or stats that
    /// are not registered are embedded.
    /// </summary>
    public static void Save(string name, CharacterLook look, CharacterStats stats, string path)
    {
        var writer = new StringBuilder();
        writer.AppendLine(GameVersion.Version.ToString());
        writer.AppendLineFormat("name {0}", name);
        if (Registries.Looks.TryGetKeyFor(look, out var k))
            writer.AppendLineFormat("look {0}", k);
        else
            writer.AppendLineFormat("look {0}", dataPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(CharacterLookDeserialiser.Serialise(look))));

        if (Registries.Stats.TryGetKeyFor(stats, out k))
            writer.AppendLineFormat("stats {0}", k);
        else
            writer.AppendLineFormat("stats {0}", dataPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(CharacterStatsDeserialiser.Serialise(stats))));

        SaveFile.WriteAllText(path, writer.ToString());
    }

    public class AssetDeserialiser : IAssetDeserialiser<ExperimentCharacterPreset>
    {
        public ExperimentCharacterPreset Deserialise(Func<Stream> stream, in AssetMetadata assetMetadata)
        {
            using var s = stream();
            return deserialiser.Deserialise(s, assetMetadata.Path);
        }

        public bool IsCandidate(in AssetMetadata assetMetadata)
        {
            return assetMetadata.Path.EndsWith(".preset");
        }
    }
}