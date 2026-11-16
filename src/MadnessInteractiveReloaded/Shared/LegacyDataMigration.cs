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
    /// Top-level folder that is not copied: old logs are of no use to the renamed game.
    /// </summary>
    private const string LogsFolderName = "logs";

    /// <summary>
    /// Copies everything in <c>&lt;localAppData&gt;/MIR</c> except its <c>logs</c> folder into
    /// <c>&lt;localAppData&gt;/&lt;newFolderName&gt;</c>, when that folder is missing or empty. The legacy folder is never
    /// moved, changed or deleted. Never throws: errors are logged to the console and return false.
    /// Call it before constructing the <see cref="Walgelijk.Game"/>, which creates the new folder.
    /// </summary>
    /// <returns>True when the legacy data was copied.</returns>
    public static bool CopyLegacyDataIfNeeded(string localAppData, string newFolderName)
    {
        try
        {
            var source = new DirectoryInfo(Path.Combine(localAppData, LegacyFolderName));
            var target = new DirectoryInfo(Path.Combine(localAppData, newFolderName));

            if (string.Equals(source.FullName, target.FullName, StringComparison.OrdinalIgnoreCase) || !source.Exists)
                return false;

            if (target.Exists && target.EnumerateFileSystemInfos().Any())
                return false;

            CopyDirectory(source, target, isRoot: true);
            Console.WriteLine("Copied legacy data from \"{0}\" to \"{1}\"", source.FullName, target.FullName);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Failed to copy legacy data from {0} to {1}: {2}", LegacyFolderName, newFolderName, e);
            return false;
        }
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo target, bool isRoot)
    {
        target.Create();

        foreach (var file in source.EnumerateFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: false);

        foreach (var dir in source.EnumerateDirectories())
        {
            if (isRoot && dir.Name.Equals(LogsFolderName, StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(dir, new DirectoryInfo(Path.Combine(target.FullName, dir.Name)), isRoot: false);
        }
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
