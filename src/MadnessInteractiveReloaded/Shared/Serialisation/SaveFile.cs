using System;
using System.IO;
using System.Text;
using Walgelijk;

namespace MIR.Serialisation;

/// <summary>
/// What <see cref="SaveFile.TryLoad"/> found.
/// </summary>
public enum SaveLoadResult
{
    /// <summary>Neither the file nor its backup exists.</summary>
    Missing,
    /// <summary>The file was read.</summary>
    Loaded,
    /// <summary>The file was missing or unreadable, and its backup was read instead.</summary>
    LoadedFromBackup,
    /// <summary>The file (and its backup, if any) could not be read. Nothing was loaded; start fresh.</summary>
    Unreadable,
}

/// <summary>
/// Crash-safe writes and recovering reads for save files, with all the save logging in one place.
/// <para>
/// A write goes to <c>&lt;file&gt;.tmp</c>, is flushed to disk, and only then replaces the file in one rename, keeping the
/// previous file as <c>&lt;file&gt;.bak</c>. A crash or power loss mid-write leaves the old file untouched, at worst with a
/// truncated <c>.tmp</c> next to it, which nothing reads and the next write overwrites.
/// </para>
/// <para>
/// A read falls back to the <c>.bak</c> when the file is missing or unreadable. An unreadable file is renamed to
/// <c>&lt;file&gt;.corrupt-&lt;yyyyMMdd-HHmmss&gt;</c> and is never deleted, so a player (or a bug report) can still get at it.
/// </para>
/// </summary>
public static class SaveFile
{
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";
    public const string CorruptInfix = ".corrupt-";
    public const string CorruptTimestampFormat = "yyyyMMdd-HHmmss";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes whatever <paramref name="write"/> puts in the stream to <paramref name="path"/>, atomically, and logs the save.
    /// Throws when the write fails; the existing file is not touched until the new contents are complete on disk, and the
    /// temp file is removed. Thread-safe for different paths.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="write">Writes the new contents to the stream it is given.</param>
    /// <param name="keepBackup">Keep the previous file as <c>.bak</c>. Turn it off for files written to folders that other
    /// code lists, like editor saves into an asset folder.</param>
    public static void Write(string path, Action<Stream> write, bool keepBackup = true)
    {
        var tmp = path + TempSuffix;
        try
        {
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(tmp, path, keepBackup ? path + BackupSuffix : null);
            else
                File.Move(tmp, path);

            Logger.Log($"Saved \"{Path.GetFileName(path)}\"");
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>
    /// Writes <paramref name="contents"/> as UTF-8 without a byte order mark (like <see cref="File.WriteAllText(string, string?)"/>),
    /// atomically. Throws when the write fails.
    /// </summary>
    public static void WriteAllText(string path, string contents, bool keepBackup = true)
    {
        var bytes = Utf8NoBom.GetBytes(contents);
        Write(path, s => s.Write(bytes), keepBackup);
    }

    /// <summary>
    /// <see cref="WriteAllText"/> that logs instead of throwing. Use it wherever a failed save must not stop the game,
    /// such as a read-only save folder.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="contents">The new contents.</param>
    /// <param name="what">What is saved, for the log, such as "settings".</param>
    /// <param name="keepBackup">Keep the previous file as <c>.bak</c>.</param>
    /// <returns>True when the file was written.</returns>
    public static bool TryWriteAllText(string path, string contents, string what, bool keepBackup = true)
    {
        try
        {
            WriteAllText(path, contents, keepBackup);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error($"Could not save {what} to \"{path}\": {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reads <paramref name="path"/> with <paramref name="parse"/>, falling back to its <c>.bak</c>. When the backup is used,
    /// it is also restored as the file. Never throws for a missing or bad file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="value">What was loaded, or null.</param>
    /// <param name="parse">Turns the file's text into a value. Throwing or returning null means the file is unreadable.</param>
    /// <param name="what">What is loaded, for the log, such as "campaign progress".</param>
    /// <param name="quarantine">Rename an unreadable file to <c>.corrupt-…</c>. Turn it off when "unreadable" can also mean
    /// "needs something that is missing right now", like a look that uses armour from a mod that is not installed: the
    /// file is then left where it is, and the backup is only read, not restored over it.</param>
    public static SaveLoadResult TryLoad<T>(string path, Func<string, T?> parse, out T? value, string what, bool quarantine = true)
        where T : class
    {
        value = null;
        var backup = path + BackupSuffix;

        var fileExists = File.Exists(path);
        var backupExists = File.Exists(backup);
        var fileUnparsable = false;
        if (fileExists)
        {
            switch (TryRead(path, parse, what, out value))
            {
                case ReadOutcome.Ok:
                    return SaveLoadResult.Loaded;
                case ReadOutcome.Unparsable:
                    fileUnparsable = true;
                    break;
            }
        }

        if (backupExists)
        {
            switch (TryRead(backup, parse, what + " backup", out value))
            {
                case ReadOutcome.Ok:
                    var fileGone = !fileExists;
                    if (fileUnparsable && quarantine)
                        fileGone = Quarantine(path) != null;

                    if (fileGone && quarantine)
                        RestoreBackup(backup, path, what);

                    Logger.Warn($"Loaded {what} from its backup \"{backup}\"");
                    return SaveLoadResult.LoadedFromBackup;

                case ReadOutcome.Unparsable:
                    if (quarantine)
                        Quarantine(backup);
                    break;
            }
        }

        if (fileUnparsable && quarantine)
            Quarantine(path);

        if (fileExists || backupExists)
        {
            Logger.Error($"Could not load {what}: \"{path}\" and its backup are unreadable. Starting fresh.");
            return SaveLoadResult.Unreadable;
        }

        return SaveLoadResult.Missing;
    }

    /// <summary>
    /// Renames <paramref name="path"/> to <c>&lt;path&gt;.corrupt-&lt;yyyyMMdd-HHmmss&gt;</c> (with a counter if that name is
    /// taken), so a fresh file can be written in its place while the old one is kept.
    /// </summary>
    /// <returns>The new name, or null when the file could not be renamed (it is then left as it is).</returns>
    public static string? Quarantine(string path)
    {
        try
        {
            var stem = path + CorruptInfix + DateTime.Now.ToString(CorruptTimestampFormat);
            var target = stem;
            for (int i = 2; File.Exists(target) || Directory.Exists(target); i++)
                target = stem + "-" + i;

            File.Move(path, target);
            Logger.Warn($"Moved unreadable save \"{path}\" aside to \"{Path.GetFileName(target)}\"");
            return target;
        }
        catch (Exception e)
        {
            Logger.Error($"Could not move unreadable save \"{path}\" aside: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private enum ReadOutcome
    {
        Ok,
        /// <summary>The file could not be opened or read (locked, no access). Its contents may be fine.</summary>
        ReadFailed,
        /// <summary>The file was read, but its contents are not a valid save.</summary>
        Unparsable,
    }

    private static ReadOutcome TryRead<T>(string path, Func<string, T?> parse, string what, out T? value) where T : class
    {
        value = null;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Logger.Error($"Could not read {what} at \"{path}\": {e.GetType().Name}: {e.Message}");
            return ReadOutcome.ReadFailed;
        }

        try
        {
            value = parse(text) ?? throw new InvalidDataException("the file is empty or null");
            return ReadOutcome.Ok;
        }
        catch (Exception e)
        {
            value = null;
            Logger.Error($"Unreadable {what} at \"{path}\": {e.GetType().Name}: {e.Message}");
            return ReadOutcome.Unparsable;
        }
    }

    private static void RestoreBackup(string backup, string path, string what)
    {
        try
        {
            Write(path, s =>
            {
                using var source = File.OpenRead(backup);
                source.CopyTo(s);
            }, keepBackup: false);
            Logger.Warn($"Restored {what} from its backup");
        }
        catch (Exception e)
        {
            Logger.Error($"Could not restore {what} from its backup \"{backup}\": {e.GetType().Name}: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
            // a leftover .tmp is harmless: nothing reads it and the next write overwrites it
        }
    }
}
