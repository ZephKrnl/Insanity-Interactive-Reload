using FluentAssertions;
using MIR.Serialisation;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

[Collection("Registry collection")]
public class CampaignStatsSaveTests : IDisposable
{
    private const string Id = "employee_of_the_month";

    /// <summary>
    /// A campaign file as MIR 0.51.1 wrote it: <c>JsonConvert.SerializeObject(stats)</c>, no version.
    /// </summary>
    private const string WrittenByMir =
        """{"CampaignId":"employee_of_the_month","LevelIndex":4,"ByLevel":{"lvl_office":{"Kills":31,"Deaths":2,"Attempts":3,"TotalTimeSpent":"00:05:12.5000000","EquippedWeapon":{"Key":"pistol","Ammo":7,"InfiniteAmmo":false}},"lvl_lobby":{"Kills":5,"Deaths":0,"Attempts":1,"TotalTimeSpent":"00:00:41","EquippedWeapon":null}}}""";

    private readonly TempUserData userData = new();

    public CampaignStatsSaveTests()
    {
        Registries.CampaignStats.Clear();
    }

    public void Dispose()
    {
        Registries.CampaignStats.Clear();
        userData.Dispose();
    }

    private static string PathFor(string id) => UserData.Paths.CampaignStatsDir + id + ".json";

    private static string[] Quarantined(string id) => Directory
        .EnumerateFiles(UserData.Paths.CampaignStatsDir, id + ".json" + SaveFile.CorruptInfix + "*")
        .ToArray();

    private static CampaignStats Sample(string id = Id)
    {
        var s = new CampaignStats(id) { LevelIndex = 6 };
        s.ByLevel["lvl_office"] = new LevelStats
        {
            Kills = 12,
            Deaths = 3,
            Attempts = 4,
            TotalTimeSpent = TimeSpan.FromSeconds(83.25),
            EquippedWeapon = new PersistentEquippedWeapon { Key = "pistol", Ammo = 9, InfiniteAmmo = true },
        };
        return s;
    }

    [Fact]
    public void Save_and_load_round_trip_with_the_format_version()
    {
        var written = Sample();

        written.Save();

        JObject.Parse(File.ReadAllText(PathFor(Id)))[SaveFormat.VersionKey]!.Value<int>().Should().Be(SaveFormat.CampaignStatsVersion);
        var read = CampaignStats.Load(PathFor(Id));
        read.Should().NotBeNull();
        read.Should().BeEquivalentTo(written);
    }

    [Fact]
    public void Save_is_atomic_and_keeps_a_backup()
    {
        var s = Sample();
        s.Save();
        s.LevelIndex = 7;
        s.Save();

        CampaignStats.Load(PathFor(Id))!.LevelIndex.Should().Be(7);
        CampaignStats.Load(PathFor(Id) + SaveFile.BackupSuffix)!.LevelIndex.Should().Be(6);
        File.Exists(PathFor(Id) + SaveFile.TempSuffix).Should().BeFalse();
    }

    [Fact]
    public void File_written_by_MIR_without_a_version_loads_and_is_saved_as_version_1()
    {
        File.WriteAllText(PathFor(Id), WrittenByMir);

        var read = CampaignStats.Load(PathFor(Id))!;

        read.CampaignId.Should().Be(Id);
        read.LevelIndex.Should().Be(4);
        read.FormatVersion.Should().Be(1);
        read.ByLevel["lvl_office"].Kills.Should().Be(31);
        read.ByLevel["lvl_office"].TotalTimeSpent.Should().Be(TimeSpan.FromSeconds(312.5));
        read.ByLevel["lvl_office"].EquippedWeapon!.Value.Key.Should().Be("pistol");
        read.ByLevel["lvl_lobby"].EquippedWeapon.Should().BeNull();

        read.Save();
        JObject.Parse(File.ReadAllText(PathFor(Id)))[SaveFormat.VersionKey]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void CreateFor_does_not_throw_after_a_corrupt_file_and_starts_fresh()
    {
        File.WriteAllText(PathFor(Id), WrittenByMir[..50]);

        var create = () => CampaignStats.CreateFor(Id);

        var stats = create.Should().NotThrow().Subject;
        stats.LevelIndex.Should().Be(0);
        Registries.CampaignStats.Get(Id).Should().BeSameAs(stats);
        File.ReadAllText(Quarantined(Id).Single()).Should().Be(WrittenByMir[..50], "the bad file is kept for recovery");
    }

    [Fact]
    public void CreateFor_restores_the_backup_after_a_corrupt_file()
    {
        File.WriteAllText(PathFor(Id), WrittenByMir[..50]);
        File.WriteAllText(PathFor(Id) + SaveFile.BackupSuffix, WrittenByMir);

        var stats = CampaignStats.CreateFor(Id);

        stats.LevelIndex.Should().Be(4, "the backup's progress is kept");
        Registries.CampaignStats.Get(Id).Should().BeSameAs(stats);
        Quarantined(Id).Should().ContainSingle();
    }

    [Fact]
    public void CreateFor_picks_up_a_readable_file_the_registry_does_not_have()
    {
        File.WriteAllText(PathFor(Id), WrittenByMir);

        var stats = CampaignStats.CreateFor(Id);

        stats.LevelIndex.Should().Be(4);
        stats.CampaignId.Should().Be(Id);
    }

    [Fact]
    public void Loading_recovers_corrupt_files_and_never_leaves_CreateFor_to_throw()
    {
        Sample("good").Save();
        File.WriteAllText(PathFor("restorable"), "{\"CampaignId\":\"restora");
        var restorable = Sample("restorable");
        restorable.LevelIndex = 2;
        File.WriteAllText(PathFor("restorable") + SaveFile.BackupSuffix, Newtonsoft.Json.JsonConvert.SerializeObject(restorable));
        File.WriteAllText(PathFor("broken"), "not json at all");

        Registries.LoadCampaignsStats();

        Registries.CampaignStats.Get("good").LevelIndex.Should().Be(6);
        Registries.CampaignStats.Get("restorable").LevelIndex.Should().Be(2);
        Registries.CampaignStats.Has("broken").Should().BeFalse();
        Quarantined("broken").Should().ContainSingle();
        Quarantined("restorable").Should().ContainSingle();

        var create = () => CampaignStats.CreateFor("broken");
        create.Should().NotThrow().Which.LevelIndex.Should().Be(0);
    }

    [Fact]
    public void Loading_ignores_temp_and_quarantined_files_and_restores_a_lone_backup()
    {
        Sample("good").Save();
        Sample("good").Save(); // good.json.bak
        File.WriteAllText(PathFor("good") + SaveFile.TempSuffix, "{\"Campaig");
        File.WriteAllText(PathFor("old") + SaveFile.CorruptInfix + "20260101-000000", "garbage");
        var lone = Sample("lone");
        lone.LevelIndex = 3;
        File.WriteAllText(PathFor("lone") + SaveFile.BackupSuffix, Newtonsoft.Json.JsonConvert.SerializeObject(lone));

        Registries.LoadCampaignsStats();

        Registries.CampaignStats.GetAllKeys().Should().BeEquivalentTo("good", "lone");
        Registries.CampaignStats.Get("lone").LevelIndex.Should().Be(3);
        File.Exists(PathFor("lone")).Should().BeTrue("the backup is restored as the file");
    }
}
