using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.AssetManager.Deserialisers;

namespace MIR;

/// <summary>
/// Represents a character "team"
/// </summary>
public class Faction
{
    public string Name;
    public readonly string Id;
    public readonly uint CollisionLayer;

    /// <summary>
    /// The enemies this faction declares in its data file. <see cref="RegisterDeclaredRelationships"/> puts them into
    /// <see cref="FactionRelationships"/>.
    /// </summary>
    public readonly IReadOnlyList<string> DeclaredEnemies;

    /// <summary>
    /// The allies this faction declares in its data file. <see cref="RegisterDeclaredRelationships"/> puts them into
    /// <see cref="FactionRelationships"/>.
    /// </summary>
    public readonly IReadOnlyList<string> DeclaredAllies;

    public uint AttackHitLayerComposite
    {
        get
        {
            uint enemy = CollisionLayers.AllCharacters;

            foreach (var item in FactionRelationships.GetAllies(Id))
                enemy &= ~Registries.Factions.Get(item).CollisionLayer;

            if (!IsEnemiesWith(this)) // if we are not our own enemy, make sure we can't hit ourselves
                enemy &= ~CollisionLayer;
            return enemy;
        }
    }

    private static uint lastAssignedLayer = CollisionLayers.CharacterStart;

    public Faction(string id, string name, IReadOnlyList<string>? declaredEnemies = null, IReadOnlyList<string>? declaredAllies = null)
    {
        Id = id;
        Name = name;
        DeclaredEnemies = declaredEnemies ?? [];
        DeclaredAllies = declaredAllies ?? [];

        lastAssignedLayer = CollisionLayer = lastAssignedLayer << 1;
        Logger.Debug($"Assigned layer {CollisionLayer:b} to faction '{name}'");
    }

    /// <summary>
    /// Adds <see cref="DeclaredEnemies"/> and <see cref="DeclaredAllies"/> to <see cref="FactionRelationships"/>.
    /// <see cref="Registries.LoadFactions"/> calls this for every faction after clearing the relationships, because the asset
    /// cache hands back the same <see cref="Faction"/> objects on a second load and the deserialiser does not run again.
    /// </summary>
    public void RegisterDeclaredRelationships()
    {
        foreach (var other in DeclaredEnemies)
            FactionRelationships.Enemies.Ensure(Id).Add(other);

        foreach (var other in DeclaredAllies)
            FactionRelationships.Allies.Ensure(Id).Add(other);
    }

    public void AddAlly(Faction f)
    {
        if (f == this)
            throw new System.Exception("Faction can't ally itself");

        if (IsEnemiesWith(f))
            throw new System.Exception("Faction can't ally an enemy");

        FactionRelationships.Allies.Ensure(Id).Add(f.Id);
    }

    public void AddEnemy(Faction f)
    {
        if (f == this)
            throw new System.Exception("Faction can't be an enemy with itself");

        if (IsAlliedTo(f))
            throw new System.Exception("Faction can't be an enemy with an ally");

        FactionRelationships.Enemies.Ensure(Id).Add(f.Id);
    }

    public void ClearAllies()
    {
        if (FactionRelationships.Allies.TryGetValue(Id, out var set))
            set.Clear();
    }

    public void ClearEnemies()
    {
        if (FactionRelationships.Enemies.TryGetValue(Id, out var set))
            set.Clear();
    }

    public void RemoveAlly(Faction f)
    {
        FactionRelationships.Allies.Ensure(Id).Remove(f.Id);
    }

    public void RemoveEnemy(Faction f)
    {
        FactionRelationships.Enemies.Ensure(Id).Remove(f.Id);
    }

    public bool IsAlliedTo(Faction other)
    {
        if (ImprobabilityDisks.IsEnabled("everyone_evil"))
            return false;

        return FactionRelationships.AreAllies(Id, other.Id);
    }

    public bool IsEnemiesWith(Faction other)
    {
        if (ImprobabilityDisks.IsEnabled("everyone_evil"))
            return true;

        return FactionRelationships.AreEnemies(Id, other.Id);
    }

    public class FactionDeserialiser : IAssetDeserialiser<Faction>
    {
        public Faction Deserialise(Func<Stream> stream, in AssetMetadata assetMetadata)
        {
            using var input = stream();
            using var json = new StreamReader(input, leaveOpen: false);
            var v = JsonConvert.DeserializeObject<IntermediateFaction>(json.ReadToEnd());
            // The relationships are kept on the faction and registered by Registries.LoadFactions, not here: a cached
            // faction is never deserialised again, so relationships filled here would be lost after the next clear.
            return new Faction(Path.GetFileNameWithoutExtension(assetMetadata.Path), v.Name, v.Enemies, v.Allies);
        }

        public bool IsCandidate(in AssetMetadata assetMetadata)
        {
            return assetMetadata.Path.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase);
        }

        private struct IntermediateFaction
        {
            public string Name;
            public string[] Enemies, Allies;
        }
    }
}

public static class FactionRelationships
{
    public static Dictionary<string, HashSet<string>> Allies = [];
    public static Dictionary<string, HashSet<string>> Enemies = [];

    public static void Clear()
    {
        Allies.Clear();
        Enemies.Clear();
    }

    public static IEnumerable<string> GetEnemies(string a)
    {
        if (Enemies.TryGetValue(a, out var set))
            foreach (var other in set)
                if (other != a)
                    yield return a;
    }

    public static IEnumerable<string> GetAllies(string a)
    {
        if (Allies.TryGetValue(a, out var set))
            foreach (var other in set)
                if (other != a)
                    yield return a;
    }

    public static IEnumerable<Faction> GetEnemies(Faction a)
    {
        if (Enemies.TryGetValue(a.Id, out var set))
            foreach (var other in set)
                if (other != a.Id)
                    yield return a;
    }

    public static IEnumerable<Faction> GetAllies(Faction a)
    {
        if (Allies.TryGetValue(a.Id, out var set))
            foreach (var other in set)
                if (other != a.Id)
                    yield return a;
    }

    public static bool AreAllies(Faction a, Faction b) => AreAllies(a.Id, b.Id);

    public static bool AreAllies(string a, string b)
    {
        if (ImprobabilityDisks.IsEnabled("everyone_evil"))
            return false;

        if (Allies.TryGetValue(a, out var set))
            if (set.Contains(b))
                return true;

        return false;
    }

    public static bool AreEnemies(Faction a, Faction b) => AreEnemies(a.Id, b.Id);

    public static bool AreEnemies(string a, string b)
    {
        if (ImprobabilityDisks.IsEnabled("everyone_evil"))
            return true;

        if (Enemies.TryGetValue(a, out var set))
            if (set.Contains(b))
                return true;

        return false;
    }
}
