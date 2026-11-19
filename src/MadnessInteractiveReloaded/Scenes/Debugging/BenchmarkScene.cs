namespace MIR;

using System;
using System.Numerics;
using Walgelijk;

/// <summary>
/// The performance benchmark scene: the <see cref="BenchmarkSystem"/> scenario in an experiment room (BUG-14).
/// </summary>
public static class BenchmarkScene
{
    /// <summary>
    /// The global settings the benchmark changes, taken before it changes them. <see cref="BenchmarkSystem"/> takes it and calls
    /// <see cref="ApplyBenchmarkSettings"/> when its scene is activated, which is after the previous scene was deactivated:
    /// experiment mode saves AutoSpawn on deactivation, and a benchmark started during a benchmark must see the settings
    /// from before the first one. <see cref="Restore"/> puts them back; it runs when the run ends and when the scene is left,
    /// and only does anything the first time.
    /// </summary>
    public sealed class GlobalState
    {
        public readonly bool AutoSpawn;
        public readonly bool DisableAI;
        public readonly float TimeScale;
        public readonly int UpdateRate;
        public readonly bool DevelopmentMode;
        public readonly bool? GodDisk;
        private bool restored;

        public GlobalState(Game game)
        {
            AutoSpawn = AiCharacterSystem.AutoSpawn;
            DisableAI = AiCharacterSystem.DisableAI;
            TimeScale = game.State.Time.TimeScale;
            UpdateRate = game.UpdateRate;
            DevelopmentMode = game.DevelopmentMode;
            GodDisk = ImprobabilityDisks.All.TryGetValue("god", out var god) ? god.Enabled : null;
        }

        public bool IsRestored => restored;

        /// <summary>The settings the benchmark runs with. The frame cap and VSync are left alone: they are what it measures
        /// under, and go into the CSV.</summary>
        public static void ApplyBenchmarkSettings(Game game)
        {
            game.State.Time.TimeScale = 1;
            AiCharacterSystem.DisableAI = false;
            AiCharacterSystem.AutoSpawn = false; // the benchmark spawns its own AI, there is no EnemySpawningSystem
            game.DevelopmentMode = false;

            // "god" makes the player ignore bullets and melee hits; the player's Invincible flag covers the rest. Player only.
            if (ImprobabilityDisks.All.TryGetValue("god", out var god))
                god.Enabled = true;
        }

        public void Restore(Game game)
        {
            if (restored)
                return;
            restored = true;

            AiCharacterSystem.AutoSpawn = AutoSpawn;
            AiCharacterSystem.DisableAI = DisableAI;
            game.State.Time.TimeScale = TimeScale;
            game.UpdateRate = UpdateRate;
            game.DevelopmentMode = DevelopmentMode;
            if (GodDisk.HasValue && ImprobabilityDisks.All.TryGetValue("god", out var god))
                god.Enabled = GodDisk.Value;
        }
    }

    public static Scene Create(Game game, BenchmarkOptions options)
    {
        var entry = Registries.Levels.Get(options.Level);
        var level = entry.Level.Value;

        // No global setting is changed here: this runs before game.Scene deactivates the current scene (see GlobalState).
        var scene = SceneUtils.PrepareGameScene(game, GameMode.Experiment, true, level);
        scene.ScenePersistence = ScenePersistence.Dispose;

        if (!MadnessUtils.FindPlayer(scene, out var player, out var character))
            throw new Exception($"No player in the benchmark level \"{options.Level}\"");

        player.RespondToUserInput = false;

        // In the middle of the room, so the AI come from both sides instead of queueing up in one line of fire.
        var x = level.LevelBounds.MinX + level.LevelBounds.Width / 2;
        var floor = new Vector2(x, level.GetFloorLevelAt(x) + CharacterConstants.GetFloorOffset(character.Positioning.Scale));
        character.Positioning.GlobalCenter = floor;
        character.Positioning.GlobalTarget = floor with { Y = 0 };
        character.Flags |= CharacterFlags.Invincible;
        if (Registries.Weapons.TryGet("aug", out var aug))
            character.EquipWeapon(scene, Prefabs.CreateWeapon(scene, default, aug));

        scene.AddSystem(new BenchmarkSystem(options));
        return scene;
    }
}
