using FluentAssertions;
using MIR.Serialisation;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace MIR.Test.Shared;

public class SaveFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "iir-savefile-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(root, "save.json");
    private string Tmp => FilePath + SaveFile.TempSuffix;
    private string Bak => FilePath + SaveFile.BackupSuffix;

    public SaveFileTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (!Directory.Exists(root))
            return;
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(root, true);
    }

    /// <summary>
    /// Parses "value=&lt;something&gt;" and rejects anything else, like a real save parser rejects a truncated file.
    /// </summary>
    private static string? Parse(string text)
    {
        if (!text.StartsWith("value=") || !text.EndsWith(";"))
            throw new FormatException("not a save: " + text);
        return text["value=".Length..^1];
    }

    private string[] Corrupted(string path) => Directory
        .EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + SaveFile.CorruptInfix + "*")
        .ToArray();

    [Fact]
    public void Write_creates_the_file_when_there_is_none()
    {
        SaveFile.WriteAllText(FilePath, "value=one;");

        File.ReadAllText(FilePath).Should().Be("value=one;");
        File.Exists(Tmp).Should().BeFalse("the temp file is renamed into place");
        File.Exists(Bak).Should().BeFalse("there was nothing to back up");
    }

    [Fact]
    public void Write_replaces_the_file_and_keeps_the_previous_one_as_bak()
    {
        SaveFile.WriteAllText(FilePath, "value=one;");
        SaveFile.WriteAllText(FilePath, "value=two;");

        File.ReadAllText(FilePath).Should().Be("value=two;");
        File.ReadAllText(Bak).Should().Be("value=one;");
        File.Exists(Tmp).Should().BeFalse();
    }

    [Fact]
    public void Write_without_backup_leaves_no_bak()
    {
        SaveFile.WriteAllText(FilePath, "value=one;", keepBackup: false);
        SaveFile.WriteAllText(FilePath, "value=two;", keepBackup: false);

        File.ReadAllText(FilePath).Should().Be("value=two;");
        File.Exists(Bak).Should().BeFalse();
        File.Exists(Tmp).Should().BeFalse();
    }

    [Fact]
    public void Write_uses_utf8_without_a_byte_order_mark()
    {
        SaveFile.WriteAllText(FilePath, "value=ä;");

        File.ReadAllBytes(FilePath).Should().Equal(new UTF8Encoding(false).GetBytes("value=ä;"));
    }

    [Fact]
    public void A_write_that_dies_half_way_never_touches_the_existing_file()
    {
        SaveFile.WriteAllText(FilePath, "value=good;");
        SaveFile.WriteAllText(FilePath, "value=newer;");
        var before = File.ReadAllBytes(FilePath);
        var bakBefore = File.ReadAllBytes(Bak);

        var write = () => SaveFile.Write(FilePath, s =>
        {
            s.Write(Encoding.UTF8.GetBytes("value=tru"));
            throw new IOException("simulated kill");
        });

        write.Should().Throw<IOException>().WithMessage("simulated kill");
        File.ReadAllBytes(FilePath).Should().Equal(before);
        File.ReadAllBytes(Bak).Should().Equal(bakBefore);
    }

    [Fact]
    public void A_truncated_tmp_left_by_a_killed_process_never_replaces_good_data()
    {
        SaveFile.WriteAllText(FilePath, "value=good;");
        File.WriteAllText(Tmp, "value=tru"); // the process died while writing the temp file

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Loaded);
        loaded.Should().Be("good");
        File.ReadAllText(FilePath).Should().Be("value=good;");
        Corrupted(FilePath).Should().BeEmpty();

        SaveFile.WriteAllText(FilePath, "value=next;");

        File.ReadAllText(FilePath).Should().Be("value=next;");
        File.ReadAllText(Bak).Should().Be("value=good;");
        File.Exists(Tmp).Should().BeFalse();
    }

    [Fact]
    public void Load_of_a_missing_file_creates_nothing()
    {
        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Missing);

        loaded.Should().BeNull();
        Directory.EnumerateFileSystemEntries(root).Should().BeEmpty();
    }

    [Fact]
    public void Load_falls_back_to_bak_and_quarantines_the_unreadable_file()
    {
        File.WriteAllText(FilePath, "value=trunc");
        File.WriteAllText(Bak, "value=previous;");

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.LoadedFromBackup);

        loaded.Should().Be("previous");
        var corrupt = Corrupted(FilePath).Should().ContainSingle().Subject;
        File.ReadAllText(corrupt).Should().Be("value=trunc", "the unreadable file is kept, never deleted");
        File.ReadAllText(FilePath).Should().Be("value=previous;", "the backup is restored as the file");
        File.ReadAllText(Bak).Should().Be("value=previous;");
    }

    [Fact]
    public void Quarantined_file_is_named_with_a_timestamp()
    {
        File.WriteAllText(FilePath, "garbage");

        SaveFile.TryLoad(FilePath, Parse, out _, "test save").Should().Be(SaveLoadResult.Unreadable);

        var name = Path.GetFileName(Corrupted(FilePath).Single());
        name.Should().MatchRegex("^" + Regex.Escape("save.json" + SaveFile.CorruptInfix) + @"\d{8}-\d{6}$");
    }

    [Fact]
    public void Unreadable_file_without_a_backup_is_quarantined_and_loads_nothing()
    {
        File.WriteAllText(FilePath, "garbage");

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Unreadable);

        loaded.Should().BeNull();
        File.Exists(FilePath).Should().BeFalse("a fresh save can now be written in its place");
        File.ReadAllText(Corrupted(FilePath).Single()).Should().Be("garbage");
    }

    [Fact]
    public void Unreadable_file_and_backup_are_both_kept()
    {
        File.WriteAllText(FilePath, "garbage");
        File.WriteAllText(Bak, "more garbage");

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Unreadable);

        loaded.Should().BeNull();
        File.ReadAllText(Corrupted(FilePath).Single()).Should().Be("garbage");
        File.ReadAllText(Corrupted(Bak).Single()).Should().Be("more garbage");
    }

    [Fact]
    public void A_backup_without_its_file_is_loaded_and_restored()
    {
        File.WriteAllText(Bak, "value=previous;");

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.LoadedFromBackup);

        loaded.Should().Be("previous");
        File.ReadAllText(FilePath).Should().Be("value=previous;");
    }

    [Fact]
    public void Quarantine_never_overwrites_an_earlier_quarantined_file()
    {
        File.WriteAllText(FilePath, "first");
        SaveFile.Quarantine(FilePath).Should().NotBeNull();
        File.WriteAllText(FilePath, "second");
        SaveFile.Quarantine(FilePath).Should().NotBeNull();

        Corrupted(FilePath).Select(File.ReadAllText).Should().BeEquivalentTo("first", "second");
    }

    [Fact]
    public void Without_quarantine_an_unreadable_file_is_left_alone()
    {
        File.WriteAllText(FilePath, "value=needs a mod;x");
        File.WriteAllText(Bak, "value=previous;");

        SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save", quarantine: false).Should().Be(SaveLoadResult.LoadedFromBackup);

        loaded.Should().Be("previous");
        File.ReadAllText(FilePath).Should().Be("value=needs a mod;x");
        Corrupted(FilePath).Should().BeEmpty();
    }

    [Fact]
    public void Failed_write_to_a_read_only_file_is_reported_not_thrown_and_keeps_the_file()
    {
        SaveFile.WriteAllText(FilePath, "value=good;");
        File.SetAttributes(FilePath, FileAttributes.ReadOnly);

        SaveFile.TryWriteAllText(FilePath, "value=new;", "test save").Should().BeFalse();

        File.ReadAllText(FilePath).Should().Be("value=good;");
        File.Exists(Tmp).Should().BeFalse("a failed write cleans up its temp file");
    }

    [Fact]
    public void Failed_write_to_a_missing_folder_is_reported_not_thrown()
    {
        var path = Path.Combine(root, "missing", "save.json");

        SaveFile.TryWriteAllText(path, "value=new;", "test save").Should().BeFalse();

        Directory.Exists(Path.Combine(root, "missing")).Should().BeFalse();
    }

    private FileStream LockFile() => new(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    [Fact]
    public void A_locked_file_is_inaccessible_and_never_written_this_session()
    {
        SaveFile.WriteAllText(FilePath, "value=good;");
        var original = File.ReadAllBytes(FilePath);

        using (LockFile())
        {
            SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Inaccessible);
            loaded.Should().BeNull();
            SaveFile.TryWriteAllText(FilePath, "value=fresh;", "test save").Should().BeFalse();
        }

        SaveFile.IsWriteBlocked(FilePath).Should().BeTrue();
        SaveFile.TryWriteAllText(FilePath, "value=fresh;", "test save").Should().BeFalse("the file stays protected after the lock is gone");
        var write = () => SaveFile.WriteAllText(FilePath, "value=fresh;");
        write.Should().Throw<IOException>();

        File.ReadAllBytes(FilePath).Should().Equal(original);
        File.Exists(Bak).Should().BeFalse("the original was never rotated into a backup");
        Corrupted(FilePath).Should().BeEmpty("a file that could not be opened is not corrupt");
    }

    [Fact]
    public void A_locked_file_with_a_readable_backup_loads_the_backup_but_is_never_written()
    {
        SaveFile.WriteAllText(FilePath, "value=older;");
        SaveFile.WriteAllText(FilePath, "value=newer;");
        var original = File.ReadAllBytes(FilePath);
        var originalBak = File.ReadAllBytes(Bak);

        using (LockFile())
        {
            SaveFile.TryLoad(FilePath, Parse, out var loaded, "test save").Should().Be(SaveLoadResult.Inaccessible);
            loaded.Should().Be("older", "the backup is used in memory for this session");
        }

        SaveFile.TryWriteAllText(FilePath, "value=session;", "test save").Should().BeFalse();
        SaveFile.TryWriteAllText(FilePath, "value=session again;", "test save").Should().BeFalse();

        File.ReadAllBytes(FilePath).Should().Equal(original);
        File.ReadAllBytes(Bak).Should().Equal(originalBak);
    }

    [Fact]
    public void A_locked_backup_without_its_file_is_inaccessible_and_never_written()
    {
        File.WriteAllText(Bak, "value=previous;");
        var originalBak = File.ReadAllBytes(Bak);

        using (new FileStream(Bak, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            SaveFile.TryLoad(FilePath, Parse, out _, "test save").Should().Be(SaveLoadResult.Inaccessible);

        SaveFile.TryWriteAllText(FilePath, "value=one;", "test save").Should().BeFalse();
        SaveFile.TryWriteAllText(FilePath, "value=two;", "test save").Should().BeFalse();

        File.Exists(FilePath).Should().BeFalse();
        File.ReadAllBytes(Bak).Should().Equal(originalBak);
    }

    [Fact]
    public void TryWrite_writes_atomically_too()
    {
        SaveFile.TryWriteAllText(FilePath, "value=one;", "test save").Should().BeTrue();
        SaveFile.TryWriteAllText(FilePath, "value=two;", "test save").Should().BeTrue();

        File.ReadAllText(FilePath).Should().Be("value=two;");
        File.ReadAllText(Bak).Should().Be("value=one;");
    }
}
