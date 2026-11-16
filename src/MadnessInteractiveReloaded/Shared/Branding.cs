namespace MIR;

/// <summary>
/// Single source of truth for the user-facing name, version, author and links of this build.
/// The internal <see cref="GameVersion"/> is unrelated and is left as is.
/// </summary>
public static class Branding
{
    public const string Name = "Insanity Interactive Reloaded";
    public const string DisplayVersion = "0.1.0";
    public const string Author = "Zeph";
    public const string RepoUrl = "https://github.com/ZephKrnl/Insanity-Interactive-Reload";
    public const string IssuesUrl = RepoUrl + "/issues";

    /// <summary>
    /// Null hides the Discord button in the main menu.
    /// </summary>
    public static string? DiscordUrl => null;

    public static string NameAndVersion => $"{Name} {DisplayVersion}";

    public static string BasedOn => $"Based on Madness Interactive Reloaded v{GameVersion.Version} by Studio Minus";
}
