using System;
using System.Reflection;

namespace MIR;

/// <summary>
/// Static class that contains game version information
/// </summary>
public static class GameVersion
{
    /// <summary>
    /// The assembly version, from <c>&lt;Version&gt;</c> in <c>MIR.csproj</c>. It is still MIR's 0.51.1, because mods built for
    /// MIR may compare against it, so keep it there unless mods are checked. IIR does not use it for anything it shows or
    /// saves: IIR's own version is <see cref="Branding.DisplayVersion"/>, the credit uses <see cref="Branding.MirBaseVersion"/>
    /// and save files use <see cref="Serialisation.SaveFormat"/>.
    /// </summary>
    public static readonly Version Version;

    static GameVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName()!.Version!;
        Version = new(v.Major, v.Minor, v.Build);
    }
}