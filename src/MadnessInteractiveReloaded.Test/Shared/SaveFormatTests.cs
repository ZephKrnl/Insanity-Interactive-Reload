using FluentAssertions;
using MIR.Serialisation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// The JSON save version and migration hook.
/// </summary>
public class SaveFormatTests
{
    private sealed class Sample
    {
        public int FormatVersion;
        public string Name = "default";
        public TimeSpan Time;
    }

    [Fact]
    public void Json_without_a_version_is_version_0_and_migrates_to_the_current_one()
    {
        var data = JObject.Parse("""{ "Saves": [] }""");

        SaveFormat.ReadVersion(data).Should().Be(0);
        var migrated = SaveFormat.Migrate(SaveKind.ArenaModeSaves, 0, data);

        migrated[SaveFormat.VersionKey]!.Value<int>().Should().Be(SaveFormat.ArenaModeSavesVersion);
    }

    [Fact]
    public void Json_with_a_version_reports_it()
    {
        SaveFormat.ReadVersion(JObject.Parse("""{ "FormatVersion": 3 }""")).Should().Be(3);
    }

    [Fact]
    public void Newer_version_is_loaded_and_stamped_with_the_current_one()
    {
        var migrated = SaveFormat.Migrate(SaveKind.Settings, 99, JObject.Parse("""{ "FormatVersion": 99 }"""));

        migrated[SaveFormat.VersionKey]!.Value<int>().Should().Be(SaveFormat.SettingsVersion);
    }

    [Fact]
    public void Deserialise_keeps_strings_that_look_like_dates_or_times()
    {
        var read = SaveFormat.DeserialiseJson<Sample>("""{ "Name": "2020-01-01T00:00:00", "Time": "00:05:12.5000000" }""", SaveKind.CampaignStats);

        read.Name.Should().Be("2020-01-01T00:00:00");
        read.Time.Should().Be(TimeSpan.FromSeconds(312.5));
        read.FormatVersion.Should().Be(SaveFormat.CampaignStatsVersion);
    }

    [Theory]
    [InlineData("""{ "Name": "x" """)]
    [InlineData("""{ "Name": "x" } trailing""")]
    [InlineData("null")]
    [InlineData("")]
    public void Deserialise_rejects_what_is_not_one_whole_JSON_object(string json)
    {
        var read = () => SaveFormat.DeserialiseJson<Sample>(json, SaveKind.Settings);

        read.Should().Throw<JsonException>();
    }
}
