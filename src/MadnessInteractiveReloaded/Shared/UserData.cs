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
    /// Which saves have been read (or deliberately started fresh) this session. <see cref="SaveAll"/> writes only these, so
    /// quitting or crashing during the loading screen never overwrites a real save with defaults.
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
    /// Saves everything that is saved at exit: settings (including a pending debounced save), the player look, unlocked
    /// Improbability disks and the progress of every campaign that exists. Each save runs on its own, so one failure does not
    /// stop the others, and failures are logged, never thrown. Only what was loaded is written (see <see cref="Loaded"/>).
    /// <c>arena.json</c> is not written: nothing reads or changes it until Arena mode exists (BUG-06).
    /// Safe to call more than once, for example from the exit path and then the crash handler.
    /// </summary>
    public static void SaveAll()
    {
        if (Loaded.Settings)
            RunSave("settings", () => Instances.Settings.Save());

        if (Loaded.PlayerLook)
            RunSave("player look", () => CharacterLookDeserialiser.Save(Instances.PlayerLook, Paths.PlayerLookFile));

        if (Loaded.ImprobabilityDisks)
            RunSave("Improbability disks", () => UnlockedDisksFile.Save(Paths.UnlockedImprobabilityDisk, Instances.UnlockedImprobabilityDisks));

        CampaignStats[] campaigns = [];
        RunSave("campaign progress", () => campaigns = [.. Registries.CampaignStats.GetAllValues()]);
        foreach (var item in campaigns)
            RunSave($"campaign progress for {item.CampaignId}", () =>
            {
                if (Registries.Campaigns.Has(item.CampaignId))
                    item.Save();
            });
    }

    /// <summary>
    /// Saves <see cref="Instances.PlayerLook"/> to <c>player.look</c>, for example when the player leaves character creation.
    /// From then on it is also saved at exit. Never throws.
    /// </summary>
    /// <returns>True when it was saved.</returns>
    public static bool SavePlayerLook()
    {
        if (!RunSave("player look", () => CharacterLookDeserialiser.Save(Instances.PlayerLook, Paths.PlayerLookFile)))
            return false;
        Loaded.PlayerLook = true;
        return true;
    }

    /// <summary>
    /// Runs one save, logging instead of throwing.
    /// </summary>
    /// <returns>False when it threw.</returns>
    public static bool RunSave(string what, Action save)
    {
        try
        {
            save();
            return true;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to save {what}: {e}");
            return false;
        }
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
