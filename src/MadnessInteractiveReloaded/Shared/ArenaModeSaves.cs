using MIR.Serialisation;
using Newtonsoft.Json;

namespace MIR;

/// <summary>
/// Arena mode progress, <c>arena.json</c>. Arena mode does not exist yet: nothing loads this at startup and nothing saves it
/// (it used to be overwritten with an empty default at every exit, BUG-06). An existing file is left as it is.
/// </summary>
public class ArenaModeSaves
{
    /// <summary>
    /// Reads <c>arena.json</c> with recovery (see <see cref="SaveFile"/>), migrating files without a version. Returns an empty
    /// instance when there is no readable file.
    /// </summary>
    public static ArenaModeSaves Load()
    {
        SaveFile.TryLoad(UserData.Paths.ArenaModeSaves,
            static json => SaveFormat.DeserialiseJson<ArenaModeSaves>(json, SaveKind.ArenaModeSaves),
            out var read, "arena saves");
        return read ?? new();
    }

    /// <summary>
    /// Writes <c>arena.json</c>, atomically, keeping a <c>.bak</c>. Not called until Arena mode exists. Never throws.
    /// </summary>
    public static void Save(ArenaModeSaves instance)
    {
        instance.FormatVersion = SaveFormat.ArenaModeSavesVersion;
        SaveFile.TryWriteAllText(UserData.Paths.ArenaModeSaves, JsonConvert.SerializeObject(instance), "arena saves");
    }

    /// <summary>
    /// The format of the file this was read from, migrated to <see cref="SaveFormat.ArenaModeSavesVersion"/> on load.
    /// </summary>
    public int FormatVersion = SaveFormat.ArenaModeSavesVersion;

    public ArenaModeSave[] Saves = [];

    public class ArenaModeSave
    {
        public string PlayerName = "Hero";
        public int Money = 0;

        public CharacterState Player = new()
        {
            Experience = 0,
            Look = new CharacterLook(Registries.Looks.Get("grunt")),
            Stats = new CharacterStats(Registries.Stats.Get("agent")),
            
        };
        public CharacterState[] Allies = [];
    }

    public class CharacterState
    {
        public CharacterLook Look = new();
        public CharacterStats Stats = new();

        public (string, int)? EquippedWeapon;

        public int Experience;

        public int Level => (int)float.Ceiling(float.Max(Experience, 1) / 100f);
    }

    /* THINGS TO CONSIDER FOR ARENA MODE
     *
     * This is not Project Nexus, it is Madness Interactive
     * 
     * | DIFFERENCE                           | IMPLEMENT TIME ESTIMATE |
     * | -------------------------------------|-------------------------|
     * | A: Firearms cannot be reloaded       | Mid: animations, AI...  |
     * | B: Melee weapons have no durability  | Low                     |
     * | C: Can't move up or down             | Unfeasible              |
     * | Ç: Can't take cover                  | Mid                     |
     * | D: Melee is not especially fun       | HIgh                    |
     * 
     * COMPENSATION
     * A: Guns are cheap
     * B: Melee weapons straight up have to have durability
     * C: Multiple arena levels for variety
     * Ç: Powerups / pickuppables / abilities (disks?) to replenish dodge / cause more damage
     * D: More melee animation types
     * 
     * NOTES
     * I never liked the idea of having allies
     */
}
