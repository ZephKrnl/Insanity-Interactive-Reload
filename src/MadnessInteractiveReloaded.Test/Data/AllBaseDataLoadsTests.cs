using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using MIR.Test.GameTests;
using Newtonsoft.Json;
using Walgelijk;
using Walgelijk.AssetManager;
using Xunit;

namespace MIR.Test.Data;

/// <summary>
/// Every shipped data file loads through the game's own loaders (<see cref="Registries"/>) with zero logged errors, nothing is
/// silently skipped, and every asset or registry entry that a loaded file names exists.
/// </summary>
[Collection("Registry collection")]
public class AllBaseDataLoadsTests : IDisposable
{
    private readonly RecordingLogger logger = new();
    private readonly Game game;

    public AllBaseDataLoadsTests()
    {
        // Loads every registry in dependency order (armour before looks, weapons/stats/looks before levels, ...)
        game = TestGameUtil.Create(logger);
    }

    public void Dispose() => game.Stop();

    /// <summary>Clears the log, runs <paramref name="load"/> and fails on any error it logged.</summary>
    private void LoadWithoutErrors(Action load, string what)
    {
        logger.Clear();
        load();
        logger.Errors.Should().BeEmpty($"because every shipped {what} should load without errors");
    }

    [Fact]
    public void Every_weapon_loads_and_its_assets_exist()
    {
        LoadWithoutErrors(Registries.LoadWeapons, "weapon");
        Registries.Weapons.Count.Should().Be(ShippedData.Files("data/weapons", ".json").Count(), "because no weapon file may be skipped");

        ShippedData.MissingAssetReferences(Registries.Weapons.GetAllValues().Select(w => (w.Id, (object)w))).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_armour_piece_loads_and_its_assets_exist()
    {
        LoadWithoutErrors(Registries.LoadArmour, "armour piece");

        var loaded = Registries.Armour.Body.Count + Registries.Armour.Head.Count + Registries.Armour.BodyAccessory.Count
                   + Registries.Armour.HeadAccessory.Count + Registries.Armour.HandArmour.Count;
        loaded.Should().Be(ShippedData.Files("data/armour", ".armor", ".hand").Count(), "because no armour file may be skipped");

        var all = new[] { Registries.Armour.Body, Registries.Armour.Head, Registries.Armour.BodyAccessory, Registries.Armour.HeadAccessory }
            .SelectMany(r => r.GetAllKeys().Select(k => (k, (object)r.Get(k))))
            .Concat(Registries.Armour.HandArmour.GetAllKeys().Select(k => (k, (object)Registries.Armour.HandArmour.Get(k))));
        ShippedData.MissingAssetReferences(all).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_look_loads_and_its_assets_exist()
    {
        // The look deserialiser throws (and the registry logs an error) for an armour id that is not registered.
        LoadWithoutErrors(Registries.LoadLooks, "look");
        Registries.Looks.Count.Should().Be(ShippedData.Files("data/looks", ".look").Count(), "because no look file may be skipped");

        ShippedData.MissingAssetReferences(Registries.Looks.GetAllKeys().Select(k => (k, (object)Registries.Looks.Get(k)))).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_stats_file_loads_and_its_references_exist()
    {
        LoadWithoutErrors(Registries.LoadStats, "stats file");
        Registries.Stats.Count.Should().Be(ShippedData.Files("data/stats", ".stats").Count(), "because no stats file may be skipped");

        var problems = new List<string>();
        foreach (var key in Registries.Stats.GetAllKeys())
        {
            var s = Registries.Stats.Get(key);
            if (s.WalkAnimation != null && !Registries.Animations.Has(s.WalkAnimation))
                problems.Add($"{key}: walk_anim \"{s.WalkAnimation}\" is not a registered animation");

            foreach (var seq in s.UnarmedSeq.Concat(s.SwordSeq).Concat(s.TwoHandedSeq).Concat(s.BluntSeq).Concat(s.TwoHandedGunSeq).Concat(s.OneHandedGunSeq))
                if (!Registries.MeleeSequences.Has(seq))
                    problems.Add($"{key}: melee sequence \"{seq}\" is not registered");
        }
        problems.ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_animation_loads()
    {
        LoadWithoutErrors(Registries.LoadAnimations, "animation");
        Registries.Animations.Count.Should().Be(ShippedData.Files("data/animations", ".anim").Count(), "because no animation file may be skipped");
    }

    [Fact]
    public void Every_melee_sequence_loads_and_its_animations_exist()
    {
        // A missing animation in a sequence is only a warning in the deserialiser (the line is dropped), so warnings fail here too.
        logger.Clear();
        Registries.LoadMeleeSequences();
        logger.AtLeast(Microsoft.Extensions.Logging.LogLevel.Warning).Should().BeEmpty("because every shipped melee sequence should load cleanly");
        Registries.MeleeSequences.Count.Should().Be(ShippedData.Files("data/melee_sequences", ".seq").Count(), "because no sequence file may be skipped");
    }

    [Fact]
    public void Every_level_loads_and_its_assets_exist()
    {
        LoadWithoutErrors(Registries.LoadLevels, "level entry");
        var files = ShippedData.Files("data/levels", ".json").ToArray();
        Registries.Levels.Count.Should().Be(files.Length, "because no level file may be skipped");

        var problems = files.Where(p => !KnownBrokenLevels.ContainsKey(p)).SelectMany(LevelProblems).ToList();
        problems.ShouldHaveNoProblems();

        ShippedData.MissingAssetReferences(Registries.Levels.GetAllValues().Select(e => (e.Id, (object)e.Thumbnail))).ShouldHaveNoProblems();

        // A known-broken level must still be broken in the recorded way. Once it is fixed, this fails: remove it from the list.
        foreach (var (path, (expected, _)) in KnownBrokenLevels)
            LevelProblems(path).Should().ContainSingle().Which.Should().Contain(expected, $"because {path} is listed as known broken");
    }

    /// <summary>
    /// Shipped levels that are broken in a way with no obvious fix, each with the problem it has and the tracker note. They are
    /// left out of <see cref="Every_level_loads_and_its_assets_exist"/> and reported as skipped by <see cref="Known_broken_level_loads"/>.
    /// </summary>
    private static readonly Dictionary<string, (string Problem, string Note)> KnownBrokenLevels = new()
    {
        ["data/levels/incident/lvl_incident_end_2.json"] = ("unregistered look: mag", "BUG-111"),
    };

    [Theory]
    [InlineData("data/levels/incident/lvl_incident_end_2.json",
        Skip = "Known broken (BUG-111 in docs/TECHNICAL_AUDIT.md): an NPC uses look \"mag\", which does not exist; mag_agent or mag_rich is meant, and which one needs a decision")]
    public void Known_broken_level_loads(string path)
    {
        KnownBrokenLevels.Should().ContainKey(path);
        LevelProblems(path).ShouldHaveNoProblems();
    }

    /// <summary>
    /// Loads a level in full (the registry only reads its header) and lists what is wrong with it. LevelDeserialiser.AssertValidLevel
    /// checks that every weapon, stats and look the level names is registered; the rest is the asset-reference walk.
    /// </summary>
    private List<string> LevelProblems(string path)
    {
        var problems = new List<string>();
        logger.Clear();
        try
        {
            var level = Assets.LoadNoCache<Level>(path);
            problems.AddRange(ShippedData.MissingAssetReferences([(path, level)]));
        }
        catch (Exception e)
        {
            problems.Add($"{path}: {e.Message}");
        }
        problems.AddRange(logger.Errors.Select(e => $"{path}: {e}"));
        return problems;
    }

    [Fact]
    public void Every_campaign_loads_and_its_references_exist()
    {
        LoadWithoutErrors(Registries.LoadCampaigns, "campaign");
        Registries.Campaigns.Count.Should().Be(ShippedData.Files("data/campaigns", ".json").Count(), "because no campaign file may be skipped");

        var problems = new List<string>();
        foreach (var c in Registries.Campaigns.GetAllValues())
        {
            foreach (var level in c.Levels)
                if (!Registries.Levels.Has(level))
                    problems.Add($"{c.Id}: level \"{level}\" is not registered");
            if (c.Look != null && !Registries.Looks.Has(c.Look))
                problems.Add($"{c.Id}: look \"{c.Look}\" is not registered");
            if (c.Stats != null && !Registries.Stats.Has(c.Stats))
                problems.Add($"{c.Id}: stats \"{c.Stats}\" is not registered");
        }
        problems.ShouldHaveNoProblems();

        ShippedData.MissingAssetReferences(Registries.Campaigns.GetAllValues().Select(c => (c.Id, (object)c))).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_cutscene_loads_and_its_assets_exist()
    {
        LoadWithoutErrors(Registries.LoadCutscenes, "cutscene");
        Registries.Cutscenes.Count.Should().Be(ShippedData.Files("data/cutscenes", ".json").Count(), "because no cutscene file may be skipped");

        ShippedData.MissingAssetReferences(Registries.Cutscenes.GetAllKeys().Select(k => (k, (object)Registries.Cutscenes.Get(k)))).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_dj_entry_loads_and_its_track_exists()
    {
        LoadWithoutErrors(Registries.LoadDjTracks, "DJ entry");
        var entries = JsonConvert.DeserializeObject<object[]>(ShippedData.ReadText("data/dj.json"))!;
        Registries.Dj.Count.Should().Be(entries.Length, "because no DJ entry may be skipped");

        var problems = Registries.Dj.Where(t => !t.Sound.IsValid).Select(t => $"{t.Name}: no sound").ToList();
        problems.ShouldHaveNoProblems();
        ShippedData.MissingAssetReferences(Registries.Dj.Select(t => (t.Name, (object)t))).ShouldHaveNoProblems();
    }

    [Fact]
    public void Every_incident_music_entry_exists_and_is_used()
    {
        LoadWithoutErrors(Registries.LoadIncidentMusicSet, "incident-music entry");
        logger.Warnings.Should().BeEmpty("because every shipped incident-music entry should point at a shipped .ogg file");

        var entries = JsonConvert.DeserializeObject<string[]>(ShippedData.ReadText("data/incident_music.json"))!;
        var missing = entries.Where(p => !ShippedData.Exists(p)).ToArray();
        missing.Should().BeEmpty("because every incident-music entry must name a file that is shipped");
        Registries.IncidentMusicSet.Should().HaveCount(entries.Length, "because no incident-music entry may be skipped");
    }

    [Fact]
    public void A_missing_incident_music_entry_is_logged_as_a_warning()
    {
        logger.Clear();
        Registries.IncidentMusicSet.Clear();
        Registries.AddIncidentMusic("""["sounds/music/monks.ogg", "sounds/music/this_file_does_not_exist.ogg"]""", "test");

        Registries.IncidentMusicSet.Should().HaveCount(1);
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("sounds/music/this_file_does_not_exist.ogg");
    }

    [Fact]
    public void Every_faction_loads()
    {
        LoadWithoutErrors(Registries.LoadFactions, "faction");
        Registries.Factions.Count.Should().Be(ShippedData.Files("data/factions", ".json").Count(), "because no faction file may be skipped");
    }

    [Fact]
    public void The_reference_check_finds_a_missing_asset()
    {
        // Guards the guard: a broken reference must be reported, or every check above passes vacuously.
        var broken = new DjTrack("broken", "nobody", 120, 0, new AssetRef<StreamAudioData>("sounds/music/this_file_does_not_exist.ogg"));
        var fine = new DjTrack("fine", "nobody", 120, 0, new AssetRef<StreamAudioData>("sounds/music/monks.ogg"));

        ShippedData.MissingAssetReferences([("broken", broken), ("fine", fine)]).Should().ContainSingle().Which.Should().StartWith("broken");
    }
}
