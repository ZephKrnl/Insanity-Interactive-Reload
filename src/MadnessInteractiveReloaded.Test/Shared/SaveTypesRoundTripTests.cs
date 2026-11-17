using FluentAssertions;
using MIR.Serialisation;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// Looks, presets, Improbability disks and arena saves: each loads with and without its version (including files written
/// by MIR 0.51.1 and older), and is written atomically through <see cref="SaveFile"/>.
/// </summary>
[Collection("Registry collection")]
public class SaveTypesRoundTripTests : IDisposable
{
    private readonly TempUserData userData = new();
    private readonly SaveTestLooks looks = new();

    public void Dispose()
    {
        looks.Dispose();
        userData.Dispose();
    }

    private static string FirstLine(string path) => File.ReadLines(path).First();

    // ---------------------------------------------------------------- looks

    [Theory]
    [InlineData("0.51.1")]
    [InlineData("0.36.0")]
    [InlineData("0.1.18")]
    public void Look_written_by_MIR_loads(string version)
    {
        var path = userData.PathOf("player.look");
        File.WriteAllText(path, SaveTestLooks.LookFile(version));

        var look = CharacterLookDeserialiser.Load(path);

        look.Body.Should().BeSameAs(looks.Body);
        look.Head.Should().BeSameAs(looks.Head);
        look.HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
    }

    [Fact]
    public void Look_save_is_atomic_and_keeps_a_backup()
    {
        var path = UserData.Paths.PlayerLookFile;
        CharacterLookDeserialiser.Save(looks.Look("first"), path);
        CharacterLookDeserialiser.Save(looks.Look("second"), path);

        FirstLine(path).Should().Be("0.51.1", "the version line MIR 0.51.1 wrote, so MIR can still read it");
        File.Exists(path + SaveFile.BackupSuffix).Should().BeTrue();
        File.Exists(path + SaveFile.TempSuffix).Should().BeFalse();
        CharacterLookDeserialiser.Load(path).HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
    }

    [Fact]
    public void Look_export_keeps_no_backup()
    {
        var path = userData.PathOf("export.look");
        CharacterLookDeserialiser.Save(looks.Look(), path, keepBackup: false);
        CharacterLookDeserialiser.Save(looks.Look(), path, keepBackup: false);

        File.Exists(path + SaveFile.BackupSuffix).Should().BeFalse();
    }

    // ---------------------------------------------------------------- presets

    [Fact]
    public void Preset_round_trips_with_an_embedded_look_and_stats()
    {
        var path = UserData.Paths.ExperimentCharacterPresets + "mine.preset";
        var stats = new CharacterStats { Name = "tough", HeadHealth = 3, Abilities = [] };

        CharacterPresetDeserialiser.Save("Mine", looks.Look(), stats, path);
        CharacterPresetDeserialiser.Save("Mine", looks.Look(), stats, path);

        FirstLine(path).Should().Be("0.51.1");
        File.Exists(path + SaveFile.BackupSuffix).Should().BeTrue();
        var read = CharacterPresetDeserialiser.Load(path);
        read.Name.Should().Be("Mine");
        read.Look.HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
        read.Stats.Name.Should().Be("tough");
        read.Stats.HeadHealth.Should().Be(3);
    }

    [Fact]
    public void Preset_written_by_MIR_loads()
    {
        var path = UserData.Paths.ExperimentCharacterPresets + "old.preset";
        var lookData = Convert.ToBase64String(Encoding.UTF8.GetBytes(SaveTestLooks.LookFile("0.51.1")));
        var statsData = Convert.ToBase64String(Encoding.UTF8.GetBytes("0.51.1\n\nname tough\nhead_health 3\n"));
        File.WriteAllText(path, $"0.51.1\nname Old one\nlook data:{lookData}\nstats data:{statsData}\n");

        var read = CharacterPresetDeserialiser.Load(path);

        read.Name.Should().Be("Old one");
        read.Look.HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
        read.Stats.HeadHealth.Should().Be(3);
    }

    // ---------------------------------------------------------------- Improbability disks

    [Fact]
    public void Disks_without_a_version_line_load()
    {
        File.WriteAllLines(UserData.Paths.UnlockedImprobabilityDisk, ["tricky", "god", ""]);

        UnlockedDisksFile.Load(UserData.Paths.UnlockedImprobabilityDisk).Should().Equal("tricky", "god");
    }

    [Fact]
    public void Disks_round_trip_with_a_version_line()
    {
        var path = UserData.Paths.UnlockedImprobabilityDisk;

        UnlockedDisksFile.Save(path, ["tricky", "god"]).Should().BeTrue();

        FirstLine(path).Should().Be(UnlockedDisksFile.VersionPrefix + SaveFormat.ImprobabilityDisksVersion);
        UnlockedDisksFile.Load(path).Should().Equal("tricky", "god");
    }

    [Fact]
    public void Missing_disks_file_loads_none()
    {
        UnlockedDisksFile.Load(UserData.Paths.UnlockedImprobabilityDisk).Should().BeEmpty();
    }

    // ---------------------------------------------------------------- arena

    [Fact]
    public void Arena_file_without_a_version_loads_and_saves_with_one()
    {
        File.WriteAllText(UserData.Paths.ArenaModeSaves, """{"Saves":[]}""");

        var arena = ArenaModeSaves.Load();

        arena.Saves.Should().BeEmpty();
        arena.FormatVersion.Should().Be(SaveFormat.ArenaModeSavesVersion);

        ArenaModeSaves.Save(arena);
        JObject.Parse(File.ReadAllText(UserData.Paths.ArenaModeSaves))[SaveFormat.VersionKey]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void Unreadable_arena_file_loads_empty_and_is_kept()
    {
        File.WriteAllText(UserData.Paths.ArenaModeSaves, "{\"Sav");

        ArenaModeSaves.Load().Saves.Should().BeEmpty();

        Directory.EnumerateFiles(userData.Root, "arena.json" + SaveFile.CorruptInfix + "*").Should().ContainSingle();
    }
}
