using FluentAssertions;
using MIR.Serialisation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

[Collection("Registry collection")]
public class SettingsRoundTripTests : IDisposable
{
    private readonly TempUserData userData = new();

    public void Dispose()
    {
        Settings.CancelPendingSave();
        userData.Dispose();
    }

    private string SettingsPath => UserData.Paths.Settings;

    private static Settings NonDefault()
    {
        var s = new Settings();
        s.General.Language = "nl-NL";
        s.General.Screenshake = 0.25f;
        s.Video.FramerateCap = 144;
        s.Video.Vsync = true;
        s.Video.Window = Settings.Window.Fullscreen;
        s.Video.StampRagdolls = false;
        s.Audio.MasterVolume = 0.3f;
        s.Audio.MusicVolume = 0.4f;
        s.Audio.SfxVolume = 0.6f;
        s.Audio.UiVolume = 0.7f;
        s.Controller.Enabled = false;
        s.Controller.Vibration = false;
        s.Controller.AimDistance = 321;
        s.Controller.CursorSpeed = 1.5f;
        return s;
    }

    private static void ShouldMatch(Settings read, Settings expected)
    {
        read.General.Should().BeEquivalentTo(expected.General);
        read.Video.Should().BeEquivalentTo(expected.Video);
        read.Audio.Should().BeEquivalentTo(expected.Audio);
        read.Controller.Should().BeEquivalentTo(expected.Controller);
        read.Controls.InputMap.Keys.Should().BeEquivalentTo(expected.Controls.InputMap.Keys);
    }

    /// <summary>
    /// The JSON MIR 0.51.1 wrote: today's fields without <c>FormatVersion</c>.
    /// </summary>
    private static string WrittenByMir(Settings s)
    {
        var obj = JObject.FromObject(s);
        obj.Remove(SaveFormat.VersionKey);
        return obj.ToString(Formatting.Indented);
    }

    [Fact]
    public void Round_trip_keeps_every_value_and_writes_the_format_version()
    {
        var written = NonDefault();

        Settings.WriteToFile(SettingsPath, written);

        JObject.Parse(File.ReadAllText(SettingsPath))[SaveFormat.VersionKey]!.Value<int>().Should().Be(SaveFormat.SettingsVersion);
        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();
        ShouldMatch(read, written);
        read.FormatVersion.Should().Be(SaveFormat.SettingsVersion);
    }

    [Fact]
    public void File_without_a_version_loads_as_version_0_and_is_written_as_version_1()
    {
        var mir = NonDefault();
        File.WriteAllText(SettingsPath, WrittenByMir(mir));
        JObject.Parse(File.ReadAllText(SettingsPath)).ContainsKey(SaveFormat.VersionKey).Should().BeFalse();

        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();

        ShouldMatch(read, mir);
        read.FormatVersion.Should().Be(1, "migrated from 0 to 1 in memory");

        Settings.WriteToFile(SettingsPath, read);
        JObject.Parse(File.ReadAllText(SettingsPath))[SaveFormat.VersionKey]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void Missing_keys_keep_their_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "General": { "Language": "nl-NL" } }""");

        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();

        var defaults = new Settings();
        read.General.Language.Should().Be("nl-NL");
        read.General.Screenshake.Should().Be(defaults.General.Screenshake);
        read.Video.Should().BeEquivalentTo(defaults.Video);
        read.Audio.Should().BeEquivalentTo(defaults.Audio);
        read.Controller.Should().BeEquivalentTo(defaults.Controller);
        read.Controls.Should().NotBeNull();
        read.Controls.InputMap.Should().NotBeEmpty();
        read.FormatVersion.Should().Be(SaveFormat.SettingsVersion);
    }

    [Fact]
    public void Unknown_keys_are_ignored()
    {
        File.WriteAllText(SettingsPath, """
            {
              "FormatVersion": 1,
              "FromTheFuture": { "Anything": [1, 2, 3] },
              "General": { "Language": "nl-NL", "AlsoNew": true },
              "Audio": { "MasterVolume": 0.1 }
            }
            """);

        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();

        read.General.Language.Should().Be("nl-NL");
        read.Audio.MasterVolume.Should().Be(0.1f);
    }

    [Fact]
    public void Newer_format_version_still_loads()
    {
        File.WriteAllText(SettingsPath, """{ "FormatVersion": 99, "General": { "Language": "nl-NL" } }""");

        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();

        read.General.Language.Should().Be("nl-NL");
    }

    [Fact]
    public void Truncated_settings_fall_back_to_the_backup()
    {
        var good = NonDefault();
        Settings.WriteToFile(SettingsPath, good);
        Settings.WriteToFile(SettingsPath, good); // now settings.json.bak holds the same good copy
        File.WriteAllText(SettingsPath, File.ReadAllText(SettingsPath)[..40]);

        var read = new Settings();
        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeTrue();

        ShouldMatch(read, good);
        Directory.EnumerateFiles(userData.Root, "settings.json" + SaveFile.CorruptInfix + "*").Should().ContainSingle();
    }

    [Fact]
    public void Missing_settings_file_reads_nothing()
    {
        var read = new Settings();
        var before = read;

        Settings.TryReadFromFile(SettingsPath, ref read).Should().BeFalse();

        read.Should().BeSameAs(before);
    }

    [Theory]
    [InlineData("nl-NL", "nl-NL")]
    [InlineData("xx-XX", Settings.DefaultLanguage)]
    [InlineData("", Settings.DefaultLanguage)]
    [InlineData(null, Settings.DefaultLanguage)]
    public void Invalid_language_falls_back_to_en_GB(string? requested, string expected)
    {
        string[] available = ["en-GB", "nl-NL"];

        Settings.PickLanguage(requested, available.Contains).Should().Be(expected);
    }

    [Fact]
    public void Invalid_language_without_en_GB_keeps_the_current_language()
    {
        string[] available = ["nl-NL"];

        Settings.PickLanguage("xx-XX", available.Contains).Should().BeNull();
    }

    [Fact]
    public void Requested_save_waits_until_half_a_second_after_the_last_change()
    {
        UserData.Instances.Settings = NonDefault();

        Settings.RequestSave(now: 10.0);
        Settings.RequestSave(now: 10.3); // a slider drag keeps asking
        Settings.SaveIfDue(now: 10.6).Should().BeFalse();
        File.Exists(SettingsPath).Should().BeFalse("nothing is written while changes keep coming");

        Settings.SaveIfDue(now: 10.85).Should().BeTrue();
        File.Exists(SettingsPath).Should().BeTrue();
        Settings.SavePending.Should().BeFalse();

        Settings.SaveIfDue(now: 20.0).Should().BeFalse("one burst of changes makes one save");
    }

    [Fact]
    public void Saving_now_cancels_a_requested_save()
    {
        Settings.RequestSave(now: 10.0);

        UserData.Instances.Settings.Save();

        Settings.SavePending.Should().BeFalse();
        File.Exists(SettingsPath).Should().BeTrue();
        Settings.SaveIfDue(now: 20.0).Should().BeFalse();
    }

    [Fact]
    public void Failed_settings_save_is_logged_not_thrown()
    {
        Directory.Delete(userData.Root, true); // the folder is gone, so the write fails

        var save = () => Settings.WriteToFile(SettingsPath, NonDefault());

        save.Should().NotThrow();
    }

    [Fact]
    public void Debounce_counts_from_the_last_request()
    {
        var d = new DebouncedSave(0.5);

        d.IsPending.Should().BeFalse();
        d.TakeIfDue(100).Should().BeFalse();

        d.Request(1.0);
        d.IsPending.Should().BeTrue();
        d.TakeIfDue(1.49).Should().BeFalse();
        d.Request(1.4);
        d.TakeIfDue(1.6).Should().BeFalse();
        d.TakeIfDue(1.95).Should().BeTrue();
        d.IsPending.Should().BeFalse();
        d.TakeIfDue(5).Should().BeFalse();

        d.Request(6);
        d.Cancel();
        d.TakeIfDue(10).Should().BeFalse();
    }
}
