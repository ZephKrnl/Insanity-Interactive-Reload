namespace MIR;

/// <summary>
/// Single source of truth for the user-facing name, version, author and links of this build.
/// IIR has its own version (<see cref="DisplayVersion"/>); saves have format versions (<see cref="Serialisation.SaveFormat"/>);
/// <see cref="GameVersion"/> (the assembly version) stays at the MIR base version for mods; the credit and the saves no longer use it.
/// </summary>
public static class Branding
{
    public const string Name = "Insanity Interactive Reloaded";
    public const string DisplayVersion = "0.1.0";

    /// <summary>
    /// The Madness Interactive Reloaded release IIR is based on, for the credit. Fixed here, so bumping IIR's own version
    /// (<see cref="DisplayVersion"/>, or the assembly version) cannot make the credit wrong.
    /// </summary>
    public const string MirBaseVersion = "0.51.1";

    public const string Author = "Zeph";
    public const string RepoUrl = "https://github.com/ZephKrnl/Insanity-Interactive-Reload";
    public const string IssuesUrl = RepoUrl + "/issues";

    /// <summary>
    /// Null hides the Discord button in the main menu.
    /// </summary>
    public static string? DiscordUrl => null;

    public static string NameAndVersion => $"{Name} {DisplayVersion}";

    public static string BasedOn => $"Based on Madness Interactive Reloaded v{MirBaseVersion} by Studio Minus";
}
