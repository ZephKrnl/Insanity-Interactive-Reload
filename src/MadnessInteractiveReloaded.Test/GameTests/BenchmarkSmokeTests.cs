using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Walgelijk;
using Xunit;
using Xunit.Abstractions;

namespace MIR.Test.GameTests;

/// <summary>
/// WP-04: the benchmark scenario (<see cref="BenchmarkScene"/>, <see cref="BenchmarkSystem"/>), run headless (no rendering)
/// for a few simulated seconds. Frame times are meaningless here (every step is 1/60 s), so the test prints the wall-clock
/// cost of simulating instead.
/// </summary>
[Collection("Registry collection")]
public class BenchmarkSmokeTests : IDisposable
{
    private const int SimulatedSeconds = 6;
    private readonly ITestOutputHelper output;
    private readonly string outputDir = Path.Combine(Path.GetTempPath(), "iir-benchmark-smoke-" + Guid.NewGuid().ToString("N"));

    private readonly CharacterLook savedPlayerLook = new();

    public BenchmarkSmokeTests(ITestOutputHelper output)
    {
        this.output = output;
        RoutineScheduler.StopAll();
        UserData.Instances.PlayerLook.CopyTo(savedPlayerLook);
    }

    public void Dispose()
    {
        RoutineScheduler.StopAll();
        savedPlayerLook.CopyTo(UserData.Instances.PlayerLook);
        Registries.ClearAll();
        try { Directory.Delete(outputDir, true); } catch { }
    }

    /// <summary>
    /// What the game has set up before a benchmark can run, done the way a test can repeat it in one process.
    /// </summary>
    private static void PrepareRegistries()
    {
        // The game gives the player the grunt look until player.look is read (GameLoadingSteps.LoadPlayerLook). Without it the
        // test player has an empty look, its head and body sit at its feet, and the AI aim into the floor and hardly shoot.
        Registries.Looks.Get("grunt").CopyTo(UserData.Instances.PlayerLook);

        // LoadFactions clears FactionRelationships, which only the faction deserialiser fills, and then gets the factions from
        // the asset cache. So a second call in one process (another test) leaves every faction friendly with every other and
        // the AI never pick a target. Deserialising them again, uncached, puts the relationships back.
        Registries.LoadFactions();
        foreach (var asset in Walgelijk.AssetManager.Assets.EnumerateFolder("data/factions", SearchOption.AllDirectories))
            Walgelijk.AssetManager.Assets.LoadNoCache<Faction>(asset);
        Registries.Factions.Get("aahw").IsEnemiesWith(Registries.Factions.Get("player")).Should().BeTrue("because the benchmark AI must attack the player");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    public void ScenarioRunsHeadlessAndWritesACsv(int ai)
    {
        var logger = new RecordingLogger();
        var game = TestGameUtil.Create(logger);
        PrepareRegistries();

        var before = new
        {
            AiCharacterSystem.AutoSpawn,
            AiCharacterSystem.DisableAI,
            game.State.Time.TimeScale,
            game.UpdateRate,
            God = ImprobabilityDisks.IsEnabled("god"),
        };

        var options = new BenchmarkOptions
        {
            AiCount = ai,
            Seconds = SimulatedSeconds,
            KillInterval = 0.25f,      // many ragdolls in a short run
            MassKillInterval = 2.5f,   // and two mass kills
            OutputDirectory = outputDir,
        };

        var scene = game.Scene = BenchmarkScene.Create(game, options);
        var system = scene.GetSystem<BenchmarkSystem>();
        try
        {
            ImprobabilityDisks.IsEnabled("god").Should().BeTrue("because the player is invincible during the run");

            // Sample while it runs: one simulated second at a time.
            int maxEntities = 0, maxRagdolls = 0;
            var allocatedBefore = GC.GetTotalAllocatedBytes(false);
            var clock = Stopwatch.StartNew();
            for (int s = 0; s <= SimulatedSeconds && system.Phase != BenchmarkSystem.RunPhase.Finished; s++)
            {
                TestGameUtil.StepGame(TimeSpan.FromSeconds(1), game);
                maxEntities = Math.Max(maxEntities, scene.EntityCount);
                maxRagdolls = Math.Max(maxRagdolls, scene.GetAllComponentsOfType<RagdollComponent>().Count());
            }
            // The run measures from the frame after setup, so it ends a frame or two past the requested seconds.
            if (system.Phase != BenchmarkSystem.RunPhase.Finished)
                TestGameUtil.StepGame(TimeSpan.FromSeconds(0.1), game);
            clock.Stop();
            var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
            var simulated = system.ElapsedSeconds;

            output.WriteLine($"N = {ai}: {simulated:0.00} s simulated in {clock.Elapsed.TotalMilliseconds:0} ms wall clock " +
                $"= {clock.Elapsed.TotalMilliseconds / simulated:0.0} ms per simulated second " +
                $"({clock.Elapsed.TotalMilliseconds / Math.Max(1, system.Report?.Frames ?? 1):0.00} ms per frame), " +
                $"{allocated / (1024d * 1024d):0.0} MB allocated ({allocated / (1024d * 1024d) / simulated:0.0} MB per simulated second)");

            using (new AssertionScope())
            {
                system.Phase.Should().Be(BenchmarkSystem.RunPhase.Finished);
                var report = system.Report;
                report.Should().NotBeNull();
                output.WriteLine(BenchmarkReport.CsvHeader);
                output.WriteLine(report!.ToCsvRow());

                report.Frames.Should().BeGreaterThan(SimulatedSeconds * 60 - 5);
                report.P50Ms.Should().BeApproximately(1000 / 60d, 0.01, "because the headless stepper uses a fixed 1/60 s");
                report.AiCount.Should().Be(ai);
                (report.ShotsPerMinute + report.MeleeAttacksPerMinute).Should().BeGreaterThan(0, "because the AI attack the player");
                report.RagdollsSpawned.Should().BeGreaterThan(SimulatedSeconds * 2, "because a kill every 0.25 s and two mass kills make ragdolls");

                // Bounded: N AI plus the player at a few dozen entities each, ragdolls that are cleaned up, and short-lived
                // effects. Measured at about 1100 (N = 10) and 2100 (N = 30) peak; the bound catches a leak, not noise.
                var entityBound = 60 * (ai + 1) + 1500;
                report.PeakEntities.Should().BeLessThan(entityBound);
                maxEntities.Should().BeLessThan(entityBound);
                report.FinalEntities.Should().BeLessThan(entityBound);

                // A ragdoll is deleted once it sleeps for 2 s or lives 10 s (RagdollLifetimeSystem), so they don't all pile up.
                report.PeakRagdolls.Should().BeLessThan(report.RagdollsSpawned, "because some ragdolls are cleaned up during the run");
                maxRagdolls.Should().BeLessThanOrEqualTo(report.RagdollsSpawned);

                system.CsvPath.Should().NotBeNull();
                File.Exists(system.CsvPath).Should().BeTrue();
                Path.GetDirectoryName(system.CsvPath).Should().Be(outputDir);
                var lines = File.ReadAllLines(system.CsvPath!);
                lines.Should().HaveCount(2);
                lines[0].Should().Be(BenchmarkReport.CsvHeader);

                // The run is over, so the game has its settings back.
                AiCharacterSystem.AutoSpawn.Should().Be(before.AutoSpawn);
                AiCharacterSystem.DisableAI.Should().Be(before.DisableAI);
                game.State.Time.TimeScale.Should().Be(before.TimeScale);
                game.UpdateRate.Should().Be(before.UpdateRate);
                ImprobabilityDisks.IsEnabled("god").Should().Be(before.God);

                logger.Errors.Should().BeEmpty();
            }

            output.WriteLine($"AI shots/min {system.Report!.ShotsPerMinute:0.0}, melee/min {system.Report.MeleeAttacksPerMinute:0.0}, " +
                $"peak entities {system.Report.PeakEntities}, final {system.Report.FinalEntities}, peak ragdolls {system.Report.PeakRagdolls}");
        }
        finally
        {
            scene.Dispose();
            game.Stop();
        }
    }

    [Fact]
    public void LeavingTheSceneEarlyRestoresTheGlobalSettings()
    {
        var game = TestGameUtil.Create(new RecordingLogger());
        PrepareRegistries();

        AiCharacterSystem.AutoSpawn = true;
        AiCharacterSystem.DisableAI = true;
        game.State.Time.TimeScale = 0.5f;
        try
        {
            var scene = game.Scene = BenchmarkScene.Create(game, new BenchmarkOptions { AiCount = 3, Seconds = 60, OutputDirectory = outputDir });
            AiCharacterSystem.AutoSpawn.Should().BeFalse();
            AiCharacterSystem.DisableAI.Should().BeFalse();
            game.State.Time.TimeScale.Should().Be(1);
            ImprobabilityDisks.IsEnabled("god").Should().BeTrue();

            TestGameUtil.StepGame(TimeSpan.FromSeconds(0.5), game);
            scene.GetSystem<BenchmarkSystem>().Phase.Should().Be(BenchmarkSystem.RunPhase.Recording);

            game.Scene = new Scene(game); // the player leaves (menu, console, another level)
            scene.Dispose();

            AiCharacterSystem.AutoSpawn.Should().BeTrue();
            AiCharacterSystem.DisableAI.Should().BeTrue();
            game.State.Time.TimeScale.Should().Be(0.5f);
            ImprobabilityDisks.IsEnabled("god").Should().BeFalse();
            Directory.Exists(outputDir).Should().BeFalse("because an unfinished run writes no CSV");
        }
        finally
        {
            AiCharacterSystem.AutoSpawn = false;
            AiCharacterSystem.DisableAI = false;
            game.State.Time.TimeScale = 1;
            game.Scene?.Dispose();
            game.Stop();
        }
    }
}
