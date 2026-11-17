using FluentAssertions;
using MIR.Serialisation;
using System;
using System.IO;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// The exit and crash save (<see cref="UserData.SaveAll"/>), and the player look, which is saved when leaving character
/// creation and only overwritten at exit when it was read.
/// </summary>
[Collection("Registry collection")]
public class SaveAllTests : IDisposable
{
    private readonly TempUserData userData = new();
    private readonly SaveTestLooks looks = new();
    private readonly CharacterLook previousPlayerLook = new(UserData.Instances.PlayerLook);

    public void Dispose()
    {
        Settings.CancelPendingSave();
        previousPlayerLook.CopyTo(UserData.Instances.PlayerLook);
        looks.Dispose();
        userData.Dispose();
    }

    [Fact]
    public void Exit_save_runs_once_so_a_crash_handler_save_keeps_the_earlier_backup()
    {
        File.WriteAllText(UserData.Paths.Settings, """{ "General": { "Language": "en-GB" } }""");
        UserData.Loaded.Settings = true;
        MadnessInteractiveReloaded.ResetExitSaveForTests();
        try
        {
            UserData.Instances.Settings.General.Language = "nl-NL";
            MadnessInteractiveReloaded.SaveAll(); // from the finally after the game loop
            UserData.Instances.Settings.General.Language = "de-DE";
            MadnessInteractiveReloaded.SaveAll(); // from the crash handler
        }
        finally
        {
            MadnessInteractiveReloaded.ResetExitSaveForTests();
        }

        var saved = new Settings();
        Settings.TryReadFromFile(UserData.Paths.Settings, ref saved).Should().BeTrue();
        saved.General.Language.Should().Be("nl-NL", "the second exit save does nothing");
        var backup = new Settings();
        Settings.TryReadFromFile(UserData.Paths.Settings + SaveFile.BackupSuffix, ref backup).Should().BeTrue();
        backup.General.Language.Should().Be("en-GB", "the backup keeps the generation from before this session's exit");
    }

    [Fact]
    public void Exit_saves_come_first_in_the_finally_after_the_game_loop()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "MIR.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "MadnessInteractiveReloaded", "MadnessInteractiveReloaded.cs"));

        var start = source.IndexOf("Game.Start();", StringComparison.Ordinal);
        var finallyAt = source.IndexOf("finally", start, StringComparison.Ordinal);
        var end = source.IndexOf("Game.Scene.Dispose();", finallyAt, StringComparison.Ordinal);
        start.Should().BePositive();
        finallyAt.Should().BePositive();
        end.Should().BePositive();
        var block = source[finallyAt..end];

        var save = block.IndexOf("SaveAll();", StringComparison.Ordinal);
        save.Should().BePositive("the finally saves");
        save.Should().BeLessThan(block.IndexOf("Gamepad.StopRumble();", StringComparison.Ordinal), "so a throw there cannot skip the save");
        save.Should().BeLessThan(block.IndexOf("WinTimerResolution.End();", StringComparison.Ordinal), "so a throw there cannot skip the save");
    }

    [Fact]
    public void SaveAll_writes_nothing_that_was_not_loaded()
    {
        string[] files = [UserData.Paths.Settings, UserData.Paths.PlayerLookFile, UserData.Paths.UnlockedImprobabilityDisk, UserData.Paths.ArenaModeSaves];
        foreach (var f in files)
            File.WriteAllText(f, "ORIGINAL");

        UserData.SaveAll();

        foreach (var f in files)
        {
            File.ReadAllText(f).Should().Be("ORIGINAL", f + " was not loaded, so it must not be overwritten");
            File.Exists(f + SaveFile.BackupSuffix).Should().BeFalse();
        }
    }

    [Fact]
    public void SaveAll_never_writes_the_arena_file()
    {
        File.WriteAllText(UserData.Paths.ArenaModeSaves, "ORIGINAL");
        UserData.Loaded.Settings = UserData.Loaded.PlayerLook = UserData.Loaded.ImprobabilityDisks = true;
        looks.Look("player").CopyTo(UserData.Instances.PlayerLook);

        UserData.SaveAll();

        File.ReadAllText(UserData.Paths.ArenaModeSaves).Should().Be("ORIGINAL");
        File.Exists(UserData.Paths.Settings).Should().BeTrue();
        File.Exists(UserData.Paths.PlayerLookFile).Should().BeTrue();
        File.Exists(UserData.Paths.UnlockedImprobabilityDisk).Should().BeTrue();
    }

    [Fact]
    public void SaveAll_keeps_going_when_one_save_fails()
    {
        UserData.Loaded.Settings = UserData.Loaded.PlayerLook = UserData.Loaded.ImprobabilityDisks = true;
        UserData.Instances.PlayerLook.Body = null!; // cannot be saved
        UserData.Instances.UnlockedImprobabilityDisks = ["tricky"];
        var campaign = new Campaign { Id = "campaign", Levels = ["lvl"] };
        Registries.Campaigns.Register(campaign.Id, campaign);
        Registries.CampaignStats.Register(campaign.Id, new CampaignStats(campaign.Id) { LevelIndex = 1 });
        Registries.CampaignStats.Register("unknown", new CampaignStats("unknown"));

        var save = () => UserData.SaveAll();

        save.Should().NotThrow();
        File.Exists(UserData.Paths.Settings).Should().BeTrue();
        File.Exists(UserData.Paths.PlayerLookFile).Should().BeFalse();
        UnlockedDisksFile.Load(UserData.Paths.UnlockedImprobabilityDisk).Should().Equal("tricky");
        CampaignStats.Load(UserData.Paths.CampaignStatsDir + "campaign.json")!.LevelIndex.Should().Be(1);
        File.Exists(UserData.Paths.CampaignStatsDir + "unknown.json").Should().BeFalse("only campaigns that exist are saved, as before");
    }

    [Fact]
    public void Exit_save_flushes_a_pending_settings_save()
    {
        UserData.Instances.Settings.General.Language = "nl-NL";
        UserData.Loaded.Settings = true;
        Settings.RequestSave(now: 10.0);

        UserData.SaveAll();

        Settings.SavePending.Should().BeFalse();
        var read = new Settings();
        Settings.TryReadFromFile(UserData.Paths.Settings, ref read).Should().BeTrue();
        read.General.Language.Should().Be("nl-NL");
    }

    [Fact]
    public void Player_look_from_MIR_loads_at_startup()
    {
        File.WriteAllText(UserData.Paths.PlayerLookFile, SaveTestLooks.LookFile("0.51.1"));

        GameLoadingSteps.LoadPlayerLook();

        UserData.Instances.PlayerLook.HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
        UserData.Loaded.PlayerLook.Should().BeTrue();
    }

    [Fact]
    public void Missing_player_look_starts_as_the_default_and_is_saved_at_exit()
    {
        GameLoadingSteps.LoadPlayerLook();

        UserData.Loaded.PlayerLook.Should().BeTrue();
        UserData.Instances.PlayerLook.Body.Should().BeSameAs(looks.Body);
    }

    [Fact]
    public void Unreadable_player_look_is_left_alone_and_not_overwritten_at_exit()
    {
        const string needsAMod = "0.51.1\n\nbody\n   armour_from_a_removed_mod\n";
        File.WriteAllText(UserData.Paths.PlayerLookFile, needsAMod);

        GameLoadingSteps.LoadPlayerLook();
        UserData.SaveAll();

        UserData.Loaded.PlayerLook.Should().BeFalse();
        UserData.Instances.PlayerLook.Body.Should().BeSameAs(looks.Body, "the default look is used for this session");
        File.ReadAllText(UserData.Paths.PlayerLookFile).Should().Be(needsAMod);
        Directory.EnumerateFiles(userData.Root, "player.look" + SaveFile.CorruptInfix + "*").Should().BeEmpty();
    }

    [Fact]
    public void Unreadable_player_look_is_moved_aside_before_character_creation_saves_over_it()
    {
        const string needsAMod = "0.51.1\n\nbody\n   armour_from_a_removed_mod\n";
        File.WriteAllText(UserData.Paths.PlayerLookFile, needsAMod);
        GameLoadingSteps.LoadPlayerLook();
        looks.Look("player").CopyTo(UserData.Instances.PlayerLook);

        UserData.SavePlayerLook().Should().BeTrue();
        UserData.SaveAll();
        UserData.SaveAll();

        var aside = Directory.GetFiles(userData.Root, "player.look" + SaveFile.UnloadedInfix + "*").Should().ContainSingle().Subject;
        Path.GetFileName(aside).Should().MatchRegex(@"^player\.look\.unloaded-\d{8}-\d{6}$");
        File.ReadAllText(aside).Should().Be(needsAMod, "the look this session could not read is kept, never deleted");
        CharacterLookDeserialiser.Load(UserData.Paths.PlayerLookFile).HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
    }

    [Fact]
    public void A_look_that_was_read_is_saved_in_place_not_moved_aside()
    {
        File.WriteAllText(UserData.Paths.PlayerLookFile, SaveTestLooks.LookFile("0.51.1"));
        GameLoadingSteps.LoadPlayerLook();

        UserData.SavePlayerLook().Should().BeTrue();

        Directory.GetFiles(userData.Root, "player.look" + SaveFile.UnloadedInfix + "*").Should().BeEmpty();
        File.Exists(UserData.Paths.PlayerLookFile + SaveFile.BackupSuffix).Should().BeTrue();
    }

    [Fact]
    public void Leaving_character_creation_saves_the_look_and_marks_it_loaded()
    {
        looks.Look("player").CopyTo(UserData.Instances.PlayerLook);

        UserData.SavePlayerLook().Should().BeTrue();

        UserData.Loaded.PlayerLook.Should().BeTrue();
        CharacterLookDeserialiser.Load(UserData.Paths.PlayerLookFile).HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
    }

    [Fact]
    public void A_failed_save_is_reported_not_thrown()
    {
        UserData.RunSave("test", () => throw new IOException("disk full")).Should().BeFalse();
        UserData.RunSave("test", () => { }).Should().BeTrue();
    }
}
