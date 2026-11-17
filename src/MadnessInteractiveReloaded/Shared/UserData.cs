using System;
using System.Collections.Generic;
using System.IO;
using Walgelijk;

namespace MIR;

/// <summary>
/// Directories for saving data such as settings, player look, experiment mode characters, etc.
/// </summary>
public static class UserData
{
    public static class Paths
    {
        /// <summary>
        /// The folder all user data lives in: <c>%LOCALAPPDATA%\IIR\userdata\</c>, from the game's AppData folder. Tests point
        /// it at a temporary folder. It ends in a separator, because the paths below are built by appending to it.
        /// </summary>
        public static string BaseDir = DefaultBaseDir();

        /// <summary>
        /// The game creates its AppData folder before anything reads these paths. Without a game (tests that have not
        /// created one) there is no AppData folder, so this points at a throwaway temp folder instead of throwing, and
        /// never at the real save folder.
        /// </summary>
        private static string DefaultBaseDir()
        {
            var appData = Game.Main?.AppDataDirectory;
            return appData != null
                ? Path.Combine(appData, "userdata/")
                : Path.Combine(Path.GetTempPath(), "IIR-no-game", "userdata/");
        }

        public static string ExperimentDir => BaseDir + "experiment/";

        public static string Settings => BaseDir + "settings.json";
        public static string CampaignStatsDir => BaseDir + "campaigns/";
        public static string PlayerLookFile => BaseDir + "player.look";
        public static string ExperimentCharacterPresets => ExperimentDir + "character_presets/";

        public static string ArenaModeSaves => BaseDir + "arena.json";

        public static string UnlockedImprobabilityDisk => BaseDir + "disks.txt";
    }

    /// <summary>
    /// Which saves have been read (or deliberately started fresh) this session.
    /// </summary>
    public static class Loaded
    {
        /// <summary>Set by the last loading step, which reads the settings.</summary>
        public static bool Settings;

        /// <summary>Set when <c>player.look</c> was read or is missing, or the player saved a look. Stays false when the
        /// file could not be read (for example, it uses armour from a mod that is not installed), so it is not overwritten.</summary>
        public static bool PlayerLook;

        /// <summary>Set by the last loading step, which reads <c>disks.txt</c>.</summary>
        public static bool ImprobabilityDisks;
    }

    /// <summary>
    /// Instances for player info.
    /// </summary>
    public static class Instances
    {
        /// <summary>
        /// The player's look preset that they have configured.
        /// </summary>
        public static readonly CharacterLook PlayerLook = new() { Name = "Player" };

        /// <summary>
        /// The player's game settings.
        /// </summary>
        public static Settings Settings = new();

        /// <summary>
        /// The player's arena mode save data
        /// </summary>
        public static ArenaModeSaves ArenaMode = new();

        /// <summary> 
        /// All disk IDs unlocked by the player
        /// </summary>
        public static HashSet<string> UnlockedImprobabilityDisks = [];
    }
}
