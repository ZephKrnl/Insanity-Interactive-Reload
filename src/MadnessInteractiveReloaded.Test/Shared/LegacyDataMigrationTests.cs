using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

[Collection("Registry collection")]
public class LegacyDataMigrationTests : IDisposable
{
    private const string NewFolderName = "IIR";

    private readonly string root = Path.Combine(Path.GetTempPath(), "iir-legacy-migration-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(root, LegacyDataMigration.LegacyFolderName);
    private string Target => Path.Combine(root, NewFolderName);

    public LegacyDataMigrationTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static void WriteFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    /// <summary>
    /// Every file and folder under <paramref name="dir"/>, relative to it, with file contents, so two trees can be compared.
    /// </summary>
    private static string[] Snapshot(string dir) => Directory
        .EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories)
        .Select(p => Path.GetRelativePath(dir, p) + (File.Exists(p) ? " = " + File.ReadAllText(p) : "/"))
        .Order(StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void Copies_legacy_data_when_new_folder_missing()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "settings.json"), "{\"volume\":0.5}");
        WriteFile(Path.Combine(Legacy, "userdata", "campaigns", "stats.json"), "{\"kills\":12}");
        Directory.CreateDirectory(Path.Combine(Legacy, "userdata", "experiment"));
        WriteFile(Path.Combine(Legacy, "mods", "cool_mod", "mod.json"), "{\"id\":\"cool_mod\"}");
        WriteFile(Path.Combine(Legacy, "top.txt"), "top level");
        WriteFile(Path.Combine(Legacy, "logs", "log.txt"), "old log");
        var before = Snapshot(Legacy);

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeTrue();

        File.ReadAllText(Path.Combine(Target, "userdata", "settings.json")).Should().Be("{\"volume\":0.5}");
        File.ReadAllText(Path.Combine(Target, "userdata", "campaigns", "stats.json")).Should().Be("{\"kills\":12}");
        Directory.Exists(Path.Combine(Target, "userdata", "experiment")).Should().BeTrue("empty subfolders are copied too");
        File.ReadAllText(Path.Combine(Target, "mods", "cool_mod", "mod.json")).Should().Be("{\"id\":\"cool_mod\"}");
        File.ReadAllText(Path.Combine(Target, "top.txt")).Should().Be("top level");
        Directory.Exists(Path.Combine(Target, "logs")).Should().BeFalse("old logs are not carried over");

        Snapshot(Legacy).Should().Equal(before, "the legacy folder is copied, never moved or changed");
    }

    [Fact]
    public void Does_not_copy_when_new_folder_has_data()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "settings.json"), "legacy");
        WriteFile(Path.Combine(Target, "userdata", "settings.json"), "current");

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeFalse();

        File.ReadAllText(Path.Combine(Target, "userdata", "settings.json")).Should().Be("current");
        Snapshot(Target).Should().Equal("userdata/", Path.Combine("userdata", "settings.json") + " = current");
    }

    [Fact]
    public void Does_not_copy_when_no_legacy_folder()
    {
        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeFalse();

        Directory.Exists(Target).Should().BeFalse("nothing is created when there is nothing to copy");
    }

    [Fact]
    public void Copies_into_existing_empty_folder()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "settings.json"), "legacy");
        Directory.CreateDirectory(Target);

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeTrue();

        File.ReadAllText(Path.Combine(Target, "userdata", "settings.json")).Should().Be("legacy");
    }

    [Fact]
    public void Returns_false_instead_of_throwing_when_a_file_cannot_be_read()
    {
        var locked = Path.Combine(Legacy, "userdata", "settings.json");
        WriteFile(locked, "legacy");

        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var copy = () => LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName);
            copy.Should().NotThrow().Which.Should().BeFalse();
        }
    }

    private string Staging => Path.Combine(root, NewFolderName + LegacyDataMigration.StagingSuffix);

    [Fact]
    public void A_copy_that_failed_half_way_is_retried_on_the_next_launch()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "a.txt"), "a");
        var locked = Path.Combine(Legacy, "userdata", "settings.json");
        WriteFile(locked, "legacy settings");
        WriteFile(Path.Combine(Legacy, "userdata", "z.txt"), "z");

        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeFalse();

        Directory.Exists(Target).Should().BeFalse("a half-finished copy never lands in the real folder");

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeTrue("the unfinished copy is retried");

        File.ReadAllText(Path.Combine(Target, "userdata", "a.txt")).Should().Be("a");
        File.ReadAllText(Path.Combine(Target, "userdata", "settings.json")).Should().Be("legacy settings");
        File.ReadAllText(Path.Combine(Target, "userdata", "z.txt")).Should().Be("z");
        Directory.Exists(Staging).Should().BeFalse("the staging folder is renamed into place");
    }

    [Fact]
    public void A_retry_after_a_session_ran_fills_in_what_was_left_behind_without_overwriting()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "settings.json"), "legacy settings");
        WriteFile(Path.Combine(Legacy, "userdata", "campaigns", "c.json"), "legacy progress");
        WriteFile(Path.Combine(Staging, "userdata", "settings.json"), "legacy settings"); // the earlier attempt got this far
        WriteFile(Path.Combine(Target, "userdata", "settings.json"), "changed in the session since");
        WriteFile(Path.Combine(Target, "logs", "log.txt"), "session log");

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeTrue();

        File.ReadAllText(Path.Combine(Target, "userdata", "settings.json")).Should().Be("changed in the session since");
        File.ReadAllText(Path.Combine(Target, "userdata", "campaigns", "c.json")).Should().Be("legacy progress");
        File.ReadAllText(Path.Combine(Target, "logs", "log.txt")).Should().Be("session log");
        Directory.Exists(Staging).Should().BeFalse();
        File.ReadAllText(Path.Combine(Legacy, "userdata", "settings.json")).Should().Be("legacy settings", "the legacy folder is never changed");
    }

    [Fact]
    public void A_finished_copy_is_not_repeated()
    {
        WriteFile(Path.Combine(Legacy, "userdata", "settings.json"), "legacy");
        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeTrue();
        WriteFile(Path.Combine(Legacy, "userdata", "later.txt"), "written by MIR later");

        LegacyDataMigration.CopyLegacyDataIfNeeded(root, NewFolderName).Should().BeFalse();

        File.Exists(Path.Combine(Target, "userdata", "later.txt")).Should().BeFalse();
    }

    [Fact]
    public void Redirect_maps_MIR_to_current_assembly()
    {
        var game = typeof(MadnessUtils).Assembly;

        LegacyAssemblyRedirect.Resolve(null, new ResolveEventArgs("MIR, Version=0.51.1.0, Culture=neutral, PublicKeyToken=null"))
            .Should().BeSameAs(game);
        LegacyAssemblyRedirect.Resolve(null, new ResolveEventArgs("MIR")).Should().BeSameAs(game);
        LegacyAssemblyRedirect.Resolve(null, new ResolveEventArgs("SomethingElse")).Should().BeNull();
        LegacyAssemblyRedirect.Resolve(null, new ResolveEventArgs("MIR.Test, Version=1.0.0.0")).Should().BeNull();
    }

    [Fact]
    public void Registered_redirect_resolves_type_names_saved_with_the_MIR_assembly()
    {
        // Levels, cutscenes and mods name the game's types as "MIR.Something, MIR".
        LegacyAssemblyRedirect.Register();
        LegacyAssemblyRedirect.Register(); // a second call is harmless

        Type.GetType("MIR.MadnessUtils, MIR").Should().BeSameAs(typeof(MadnessUtils));
    }
}
