using FluentAssertions;
using MIR.Serialisation;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// A save that exists but cannot be opened at load (locked by another program, or no read access) is not "missing": it is
/// not marked loaded, and nothing writes over it for the rest of the session, even after the lock is gone.
/// </summary>
[Collection("Registry collection")]
public class InaccessibleSaveTests : IDisposable
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

    private static FileStream Lock(string path) => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    private static void ShouldBeUntouched(string path, byte[] original)
    {
        File.ReadAllBytes(path).Should().Equal(original);
        File.Exists(path + SaveFile.BackupSuffix).Should().BeFalse("the original is never rotated into a backup");
    }

    [Fact]
    public void Locked_settings_are_not_marked_loaded_and_never_written()
    {
        var path = UserData.Paths.Settings;
        File.WriteAllText(path, """{ "General": { "Language": "nl-NL" } }""");
        var original = File.ReadAllBytes(path);

        using (Lock(path))
            UserData.LoadSettings().Should().Be(SaveLoadResult.Inaccessible);

        UserData.Loaded.Settings.Should().BeFalse();
        Settings.RequestSave(now: 10.0);
        Settings.SaveIfDue(now: 11.0); // the debounced save after a settings change
        UserData.Instances.Settings.Save();
        UserData.SaveAll();

        ShouldBeUntouched(path, original);
    }

    [Fact]
    public void Readable_settings_are_marked_loaded()
    {
        File.WriteAllText(UserData.Paths.Settings, """{ "General": { "Language": "nl-NL" } }""");

        UserData.LoadSettings().Should().Be(SaveLoadResult.Loaded);

        UserData.Loaded.Settings.Should().BeTrue();
        UserData.Instances.Settings.General.Language.Should().Be("nl-NL");
    }

    [Fact]
    public void Missing_settings_are_marked_loaded_so_the_defaults_are_saved()
    {
        UserData.LoadSettings().Should().Be(SaveLoadResult.Missing);

        UserData.Loaded.Settings.Should().BeTrue();
    }

    [Fact]
    public void Locked_disks_are_not_marked_loaded_and_never_written()
    {
        var path = UserData.Paths.UnlockedImprobabilityDisk;
        File.WriteAllLines(path, ["tricky", "god"]);
        var original = File.ReadAllBytes(path);

        using (Lock(path))
            UserData.LoadUnlockedImprobabilityDisks().Should().Be(SaveLoadResult.Inaccessible);

        UserData.Loaded.ImprobabilityDisks.Should().BeFalse();
        UserData.Instances.UnlockedImprobabilityDisks.Add("jebus");
        UnlockedDisksFile.Save(path, UserData.Instances.UnlockedImprobabilityDisks).Should().BeFalse();
        UserData.SaveAll();

        ShouldBeUntouched(path, original);
    }

    [Fact]
    public void Readable_disks_are_marked_loaded()
    {
        File.WriteAllLines(UserData.Paths.UnlockedImprobabilityDisk, ["tricky"]);

        UserData.LoadUnlockedImprobabilityDisks().Should().Be(SaveLoadResult.Loaded);

        UserData.Loaded.ImprobabilityDisks.Should().BeTrue();
        UserData.Instances.UnlockedImprobabilityDisks.Should().BeEquivalentTo("tricky");
    }

    [Fact]
    public void Locked_player_look_is_not_marked_loaded_and_never_written()
    {
        var path = UserData.Paths.PlayerLookFile;
        File.WriteAllText(path, SaveTestLooks.LookFile("0.51.1"));
        var original = File.ReadAllBytes(path);

        using (Lock(path))
            GameLoadingSteps.LoadPlayerLook();

        UserData.Loaded.PlayerLook.Should().BeFalse();
        UserData.SavePlayerLook().Should().BeFalse("leaving character creation must not write over it either");
        UserData.SaveAll();

        ShouldBeUntouched(path, original);
        Directory.GetFiles(userData.Root).Select(Path.GetFileName).Where(n => n!.StartsWith("player.look"))
            .Should().Equal(["player.look"], "a file that could not be opened is not moved aside");
    }
}
