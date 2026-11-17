using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MIR;

/// <summary>
/// The game's files were renamed from MIR to IIR. Walgelijk names the AppData folder (saves, settings, mods) after the
/// entry assembly, so the rename moved it from <c>%LOCALAPPDATA%\MIR</c> to <c>%LOCALAPPDATA%\IIR</c>.
/// This copies the old folder over once, so players keep their data.
/// </summary>
public static class LegacyDataMigration
{
    /// <summary>
    /// The AppData folder of builds from before the rename.
    /// </summary>
    public const string LegacyFolderName = "MIR";

    /// <summary>
    /// The copy goes into <c>&lt;newFolderName&gt;.migrating</c>, next to the real folder, and is renamed into place only
    /// when it is complete. While this folder exists, a copy was started and did not finish, and the next launch retries it.
    /// </summary>
    public const string StagingSuffix = ".migrating";

    /// <summary>
    /// On a retry, a legacy file whose name the real folder already has (written by a session that ran after the failed
    /// copy) is kept next to it as <c>&lt;name&gt;.legacy</c> (or <c>.legacy-2</c>, …), never dropped.
    /// </summary>
    public const string LegacyCopySuffix = ".legacy";

    /// <summary>
    /// Top-level folder that is not copied: old logs are of no use to the renamed game.
    /// </summary>
    private const string LogsFolderName = "logs";

    /// <summary>
    /// Copies everything in <c>&lt;localAppData&gt;/MIR</c> except its <c>logs</c> folder into
    /// <c>&lt;localAppData&gt;/&lt;newFolderName&gt;</c>, when that folder is missing or empty, or when an earlier copy did not
    /// finish. The copy is made in a staging folder (<see cref="StagingSuffix"/>) and renamed into place at the end, so a
    /// failed copy never leaves a half-filled folder that looks finished.
    /// <para>
    /// If a session ran after a failed copy, the folder has that session's files by the time of the retry. The retry then
    /// adds what is missing and never overwrites a file in it: where both have a file, the legacy one is kept next to it as
    /// <c>&lt;name&gt;.legacy</c> (see <see cref="LegacyCopySuffix"/>), and each is logged.
    /// </para>
    /// The legacy folder is never moved, changed or deleted. Never throws: errors are logged to the console and return false.
    /// Call it before constructing the <see cref="Walgelijk.Game"/>, which creates the new folder.
    /// </summary>
    /// <returns>True when the legacy data was copied.</returns>
    public static bool CopyLegacyDataIfNeeded(string localAppData, string newFolderName)
    {
        try
        {
            var source = new DirectoryInfo(Path.Combine(localAppData, LegacyFolderName));
            var target = new DirectoryInfo(Path.Combine(localAppData, newFolderName));
            var staging = new DirectoryInfo(target.FullName + StagingSuffix);

            if (string.Equals(source.FullName, target.FullName, StringComparison.OrdinalIgnoreCase) || !source.Exists)
                return false;

            var unfinished = staging.Exists;
            var targetHasData = target.Exists && target.EnumerateFileSystemInfos().Any();
            if (targetHasData && !unfinished)
                return false;

            if (unfinished)
                Console.WriteLine("Retrying the unfinished copy of legacy data from \"{0}\"", source.FullName);

            // Overwrite: on a retry the staging folder may hold files from the attempt that failed.
            CopyDirectory(source, staging, isRoot: true);

            target.Refresh();
            if (target.Exists && !target.EnumerateFileSystemInfos().Any())
                target.Delete(recursive: false); // empty, so it can be replaced by the rename below

            target.Refresh();
            if (target.Exists)
                MergeInto(staging, target);
            else
                staging.MoveTo(target.FullName);

            Console.WriteLine("Copied legacy data from \"{0}\" to \"{1}\"", source.FullName, target.FullName);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Failed to copy legacy data from {0} to {1}, will retry on the next launch: {2}", LegacyFolderName, newFolderName, e);
            return false;
        }
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo target, bool isRoot)
    {
        target.Create();

        foreach (var file in source.EnumerateFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);

        foreach (var dir in source.EnumerateDirectories())
        {
            if (isRoot && dir.Name.Equals(LogsFolderName, StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(dir, new DirectoryInfo(Path.Combine(target.FullName, dir.Name)), isRoot: false);
        }
    }

    /// <summary>
    /// Moves every file in <paramref name="staging"/> into <paramref name="target"/>. Where <paramref name="target"/> already
    /// has the file, the legacy one goes next to it as <c>&lt;name&gt;.legacy</c> instead. Then the staging folder, now
    /// without files, is removed. If any file is still in it, it is left for the next launch.
    /// </summary>
    private static void MergeInto(DirectoryInfo staging, DirectoryInfo target)
    {
        foreach (var file in staging.GetFiles("*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target.FullName, Path.GetRelativePath(staging.FullName, file.FullName));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                var keptAs = FreeName(destination + LegacyCopySuffix);
                file.MoveTo(keptAs);
                Console.WriteLine("Kept \"{0}\", written after the earlier copy failed; the legacy version is next to it as \"{1}\".",
                    destination, Path.GetFileName(keptAs));
                continue;
            }

            file.MoveTo(destination);
        }

        foreach (var dir in staging.GetDirectories("*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target.FullName, Path.GetRelativePath(staging.FullName, dir.FullName)));

        if (staging.EnumerateFiles("*", SearchOption.AllDirectories).Any())
        {
            Console.Error.WriteLine("Left \"{0}\": it still has files that were not moved.", staging.FullName);
            return;
        }

        staging.Delete(recursive: true); // only empty folders are left
    }

    /// <summary>
    /// <paramref name="path"/>, or <c>&lt;path&gt;-2</c>, <c>-3</c>, … if it is taken.
    /// </summary>
    private static string FreeName(string path)
    {
        var name = path;
        for (int i = 2; File.Exists(name) || Directory.Exists(name); i++)
            name = path + "-" + i;
        return name;
    }
}

/// <summary>
/// Mods, levels and cutscenes refer to the game's assembly by its old name, <c>MIR</c>. This hands them the renamed one.
/// </summary>
public static class LegacyAssemblyRedirect
{
    /// <summary>
    /// The game assembly's name before the rename.
    /// </summary>
    public const string LegacyAssemblyName = "MIR";

    private static int registered;

    /// <summary>
    /// Adds <see cref="Resolve"/> to <see cref="AppDomain.AssemblyResolve"/>. Only the first call does anything.
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref registered, 1) == 0)
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    /// <summary>
    /// Returns the game assembly when <paramref name="e"/> asks for <c>MIR</c>, and null otherwise.
    /// </summary>
    public static Assembly? Resolve(object? sender, ResolveEventArgs e)
    {
        string? name;
        try
        {
            name = new AssemblyName(e.Name).Name;
        }
        catch (Exception)
        {
            return null; // not a valid assembly name, so not ours
        }

        return string.Equals(name, LegacyAssemblyName, StringComparison.OrdinalIgnoreCase)
            ? typeof(LegacyAssemblyRedirect).Assembly
            : null;
    }
}
