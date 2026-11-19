namespace MIR;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Walgelijk;
using Walgelijk.SimpleDrawing;

/// <summary>
/// Runs the benchmark (BUG-14): keeps <see cref="BenchmarkOptions.AiCount"/> AI alive against an invincible player, kills
/// some of them on a schedule so ragdolls and blood decals pile up, and measures every frame for
/// <see cref="BenchmarkOptions.Seconds"/>. The result goes to a CSV (<see cref="BenchmarkCsvWriter"/>). Made by
/// <see cref="BenchmarkScene.Create"/>.
/// </summary>
public class BenchmarkSystem : Walgelijk.System
{
    public enum RunPhase
    {
        /// <summary>The first frame: the AI are spawned, nothing is measured.</summary>
        Setup,
        Recording,
        Finished
    }

    public readonly BenchmarkOptions Options;

    public RunPhase Phase { get; private set; } = RunPhase.Setup;

    /// <summary>Set when the run is finished.</summary>
    public BenchmarkReport? Report { get; private set; }

    /// <summary>The CSV written for <see cref="Report"/>, or null when writing failed.</summary>
    public string? CsvPath { get; private set; }

    /// <summary>Seconds measured so far (unscaled).</summary>
    public double ElapsedSeconds { get; private set; }

    /// <summary>Invoked once, when the run is finished and the CSV written (or not).</summary>
    public event Action<BenchmarkSystem>? Finished;

    /// <summary>Taken when the scene is activated; null before that.</summary>
    private BenchmarkScene.GlobalState? restoreState;
    private readonly BenchmarkFrameTimes frameTimes = new();
    private readonly List<CharacterComponent> liveAi = [];
    private WeaponInstructions[] firearms = [];
    private WeaponInstructions[] meleeWeapons = [];
    private int spawnCounter;

    private float killTimer, massKillTimer;
    private int gc0, gc1, gc2;
    private long allocatedBytes, shots, melee;
    private int peakEntities, peakRagdolls;
    private readonly HashSet<Entity> ragdollsSeen = [];

    public BenchmarkSystem(BenchmarkOptions options)
    {
        Options = options;
    }

    public override void OnActivate()
    {
        // Here and not in BenchmarkScene.Create: by now the previous scene has been deactivated and has saved or restored
        // its own settings, so this snapshot is what the game goes back to.
        if (Phase == RunPhase.Finished || restoreState is { IsRestored: false })
            return;
        restoreState = new BenchmarkScene.GlobalState(Game);
        BenchmarkScene.GlobalState.ApplyBenchmarkSettings(Game);
    }

    public override void Update()
    {
        if (Phase == RunPhase.Finished)
            return;

        CollectLiveAi();

        if (Phase == RunPhase.Setup)
        {
            var weapons = Registries.Weapons.GetAllValues().ToArray();
            firearms = weapons.Where(static w => w.WeaponData.WeaponType == WeaponType.Firearm).ToArray();
            meleeWeapons = weapons.Where(static w => w.WeaponData.WeaponType == WeaponType.Melee).ToArray();

            SpawnMissing(int.MaxValue);
            BeginRecording();
            return;
        }

        if (MadnessUtils.IsPaused(Scene))
            return; // the pause menu is not part of the measurement

        var dt = Time.DeltaTimeUnscaled;
        frameTimes.Add(dt);
        ElapsedSeconds += dt;

        peakEntities = Math.Max(peakEntities, Scene.EntityCount);
        peakRagdolls = Math.Max(peakRagdolls, CountRagdolls());

        killTimer += dt;
        massKillTimer += dt;
        if (massKillTimer >= Options.MassKillInterval)
        {
            massKillTimer = 0;
            killTimer = 0;
            for (int i = 0; i < liveAi.Count; i += 2)
                ScriptedKill(liveAi[i]);
        }
        else if (killTimer >= Options.KillInterval && liveAi.Count > 0)
        {
            killTimer = 0;
            ScriptedKill(liveAi[Utilities.RandomInt(0, liveAi.Count)]);
        }

        CollectLiveAi();
        SpawnMissing(Options.MaxSpawnsPerFrame);

        if (ElapsedSeconds >= Options.Seconds)
            Finish();
    }

    public override void OnDeactivate()
    {
        // Leaving the scene early (menu, console, crash handler) must not leave benchmark settings behind.
        restoreState?.Restore(Game);
    }

    private void BeginRecording()
    {
        gc0 = GC.CollectionCount(0);
        gc1 = GC.CollectionCount(1);
        gc2 = GC.CollectionCount(2);
        allocatedBytes = GC.GetTotalAllocatedBytes(false);
        shots = AiActivityCounters.Shots;
        melee = AiActivityCounters.MeleeAttacks;
        peakEntities = Scene.EntityCount;
        Phase = RunPhase.Recording;
        Logger.Log($"Benchmark started: {Options.AiCount} AI for {Options.Seconds} s in {Options.Level}", nameof(BenchmarkSystem));
    }

    private void Finish()
    {
        Phase = RunPhase.Finished;

        var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBytes;
        var gcDelta = (GC.CollectionCount(0) - gc0, GC.CollectionCount(1) - gc1, GC.CollectionCount(2) - gc2);
        var seconds = ElapsedSeconds;

        Report = new BenchmarkReport
        {
            TimestampUtc = DateTime.UtcNow,
            Version = Branding.DisplayVersion,
#if DEBUG
            BuildConfiguration = "Debug",
#else
            BuildConfiguration = "Release",
#endif
            Level = Options.Level,
            AiCount = Options.AiCount,
            SecondsRequested = Options.Seconds,
            SecondsRecorded = seconds,
            FrameCap = DescribeFrameCap(Game),
            VSync = ReadVSync(Game),
            Frames = frameTimes.Count,
            P50Ms = frameTimes.Percentile(50) * 1000,
            P95Ms = frameTimes.Percentile(95) * 1000,
            P99Ms = frameTimes.Percentile(99) * 1000,
            MeanMs = frameTimes.Mean * 1000,
            MaxMs = frameTimes.Max * 1000,
            Gc0 = gcDelta.Item1,
            Gc1 = gcDelta.Item2,
            Gc2 = gcDelta.Item3,
            AllocatedMegabytes = allocated / (1024d * 1024d),
            PeakEntities = peakEntities,
            FinalEntities = Scene.EntityCount,
            RagdollsSpawned = ragdollsSeen.Count,
            PeakRagdolls = peakRagdolls,
            ShotsPerMinute = BenchmarkReport.PerMinute(AiActivityCounters.Shots - shots, seconds),
            MeleeAttacksPerMinute = BenchmarkReport.PerMinute(AiActivityCounters.MeleeAttacks - melee, seconds),
        };

        var directory = Options.OutputDirectory ?? BenchmarkCsvWriter.DefaultDirectory;
        if (BenchmarkCsvWriter.TryWrite(directory, Report, out var path))
        {
            CsvPath = path;
            Logger.Log($"Benchmark finished: p50 {Report.P50Ms:0.00} ms, p95 {Report.P95Ms:0.00} ms, p99 {Report.P99Ms:0.00} ms, written to {path}", nameof(BenchmarkSystem));
        }

        // The run is over: remove the AI (killing them would make one last pile of ragdolls) and give the game back its
        // settings, also while the results stay on screen.
        foreach (var ai in liveAi)
        {
            ai.DeleteHeldWeapon(Scene);
            ai.Delete(Scene);
        }
        liveAi.Clear();
        restoreState?.Restore(Game);

        try
        {
            Audio.PlayOnce(Sounds.UiConfirm);
        }
        catch (Exception)
        {
            // only a sound
        }

        Finished?.Invoke(this);

        if (Options.ExitWhenDone)
            Game.Stop();
    }

    private void CollectLiveAi()
    {
        liveAi.Clear();
        foreach (var character in Scene.GetAllComponentsOfType<CharacterComponent>())
            if (character.IsAlive && Scene.HasTag(character.Entity, Tags.EnemyAI))
                liveAi.Add(character);
    }

    /// <summary>Live ragdolls; also remembers every ragdoll seen, for the number spawned during the run (scripted kills or not).</summary>
    private int CountRagdolls()
    {
        int count = 0;
        foreach (var ragdoll in Scene.GetAllComponentsOfType<RagdollComponent>())
        {
            ragdollsSeen.Add(ragdoll.Entity);
            count++;
        }
        return count;
    }

    private void SpawnMissing(int maxThisFrame)
    {
        var level = Level.CurrentLevel;
        if (level == null)
            return;

        var faction = Registries.Factions.Get("aahw");
        var stats = Registries.Stats.Get("grunt");
        var look = Registries.Looks.Get("grunt");

        float playerX = 0;
        if (MadnessUtils.FindPlayer(Scene, out _, out var player))
            playerX = player.Positioning.GlobalCenter.X;

        var bounds = level.LevelBounds;
        float minX = bounds.MinX + 300, maxX = bounds.MaxX - 300;

        for (int spawned = 0; liveAi.Count < Options.AiCount && spawned < maxThisFrame; spawned++)
        {
            // Spread over the room, but never on top of the player.
            float x = Utilities.RandomFloat(minX, maxX);
            if (MathF.Abs(x - playerX) < 400)
                x = playerX + (x < playerX ? -400 : 400);
            x = Utilities.Clamp(x, minX, maxX);
            var bottom = new Vector2(x, level.GetFloorLevelAt(x));

            // A fixed mix so every run exercises shooting and melee: of every 8, six with a firearm, one with a melee weapon and
            // one unarmed. Gunners come first: only Level.MaxSimultaneousAttackingEnemies AI attack at once, the slots go in
            // component order, and an AI without a slot rarely shoots. Melee fighters also block the gunners' line of sight.
            WeaponInstructions? weapon = (spawnCounter++ % 8) switch
            {
                6 => Pick(meleeWeapons),
                7 => null,
                _ => Pick(firearms),
            };

            var character = weapon == null
                ? Prefabs.CreateEnemy(Scene, bottom, stats, look, faction)
                : Prefabs.CreateEnemyWithWeapon(Scene, bottom, weapon, stats, look, faction);
            liveAi.Add(character);
        }

        static WeaponInstructions? Pick(WeaponInstructions[] from) => from.Length == 0 ? null : from[Utilities.RandomInt(0, from.Length)];
    }

    private void ScriptedKill(CharacterComponent character)
    {
        if (!character.IsAlive)
            return;

        var center = character.Positioning.Body.ComputedVisualCenter;
        character.Kill();
        MadnessUtils.TurnIntoRagdoll(Scene, character, Utilities.RandomPointInCircle() * 140, Utilities.RandomFloat(-90, 90));

        // Blood decals, the same calls the game makes for wounds.
        Prefabs.CreateBloodSplat(Scene, center, Utilities.RandomFloat(0, 360), character.Look.BloodColour, 256);
        Prefabs.CreateBloodSplat(Scene, center + new Vector2(Utilities.RandomFloat(-200, 200), Utilities.RandomFloat(-100, 100)),
            Utilities.RandomFloat(0, 360), character.Look.BloodColour, 180);
    }

    private static string DescribeFrameCap(Game game)
    {
        if (!game.AdditionalLoopEvents.Any(static e => e is FrameLimiterLoopEvent))
            return game.UpdateRate > 0 ? game.UpdateRate.ToString(CultureInfo.InvariantCulture) : "none";
        var cap = FrameLimiterLoopEvent.Cap;
        return cap <= 0 ? "uncapped" : cap.ToString(CultureInfo.InvariantCulture);
    }

    private static bool ReadVSync(Game game)
    {
        try
        {
            return game.Window.VSync;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override void Render()
    {
        var windowSize = Window.Size;

        Draw.Reset();
        Draw.Order = RenderOrders.UserInterface.WithOrder(10);
        Draw.ScreenSpace = true;
        Draw.Colour = Colors.White;

        if (Phase != RunPhase.Finished)
        {
            Draw.Font = Fonts.Impact;
            Draw.FontSize = 48;
            Draw.Text($"{(int)ElapsedSeconds} / {Options.Seconds}", new Vector2(windowSize.X / 2, 64), Vector2.One,
                HorizontalTextAlign.Center, VerticalTextAlign.Middle, windowSize.X);
            Draw.FontSize = 18;
            Draw.Font = Fonts.Inter;
            Draw.Text($"BENCHMARK  {Options.AiCount} AI", new Vector2(windowSize.X / 2, 104), Vector2.One,
                HorizontalTextAlign.Center, VerticalTextAlign.Middle, windowSize.X);
            Draw.Reset();
            return;
        }

        Draw.Colour = Colors.Black.WithAlpha(0.8f);
        Draw.Quad(Vector2.Zero, windowSize);
        Draw.Colour = Colors.White;
        Draw.Font = Fonts.Impact;
        Draw.FontSize = 48;
        Draw.Text("BENCHMARK RESULTS", new Vector2(windowSize.X / 2, 64), Vector2.One, HorizontalTextAlign.Center, VerticalTextAlign.Middle, windowSize.X);

        var r = Report;
        if (r != null)
        {
            Draw.Font = Fonts.Inter;
            Draw.FontSize = 20;
            float y = 140;
            line($"{r.AiCount} AI, {r.SecondsRecorded:0.0} s, {r.Frames} frames, {r.BuildConfiguration} build, frame cap {r.FrameCap}, VSync {(r.VSync ? "on" : "off")}");
            line($"Frame time  p50 {r.P50Ms:0.00} ms   p95 {r.P95Ms:0.00} ms   p99 {r.P99Ms:0.00} ms   mean {r.MeanMs:0.00} ms   max {r.MaxMs:0.00} ms");
            line($"GC  gen0 {r.Gc0}   gen1 {r.Gc1}   gen2 {r.Gc2}   allocated {r.AllocatedMegabytes:0.0} MB");
            line($"Entities  peak {r.PeakEntities}   final {r.FinalEntities}   ragdolls spawned {r.RagdollsSpawned}   peak ragdolls {r.PeakRagdolls}");
            line($"AI shots/min {r.ShotsPerMinute:0.0}   melee attacks/min {r.MeleeAttacksPerMinute:0.0}");
            y += 16;
            line(CsvPath != null ? "Written to " + CsvPath : "The CSV could not be written; see the log.");
#if DEBUG
            Draw.Colour = Colors.Red;
            line("DEBUG BUILD: these numbers are not representative.");
#endif

            void line(string text)
            {
                Draw.Text(text, new Vector2(windowSize.X / 2, y), Vector2.One, HorizontalTextAlign.Center, VerticalTextAlign.Middle, windowSize.X);
                y += 32;
            }
        }

        Draw.Reset();
    }
}
