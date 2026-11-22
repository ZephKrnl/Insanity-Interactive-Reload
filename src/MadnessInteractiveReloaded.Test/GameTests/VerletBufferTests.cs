using System;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Walgelijk;
using Xunit;

namespace MIR.Test.GameTests;

/// <summary>
/// BUG-47: <see cref="VerletPhysicsSystem"/> used fixed 256-entry buffers, and the engine's <c>CopyAllComponentsOfType</c>
/// stops at the buffer's length without saying so. With enough simultaneous ragdolls (a mass kill), links past the 256th
/// were never solved and nodes past the 256th were never simulated.
/// </summary>
[Collection("Registry collection")]
public class VerletBufferTests : IDisposable
{
    private const int RagdollCount = 40;

    public VerletBufferTests()
    {
        BaseAssetsFixture.EnsureRegistered();
        Registries.ClearAll();
    }

    public void Dispose()
    {
        Registries.ClearAll();
    }

    [Fact]
    public void CopyAllComponentsGrowsUntilEverythingFitsThenReusesTheBuffer()
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        var scene = game.Scene = new Scene(game);
        try
        {
            for (int i = 0; i < 300; i++)
                scene.AttachComponent(scene.CreateEntity(), new TransformComponent());

            var buffer = new TransformComponent[4];
            VerletPhysicsSystem.CopyAllComponents(scene, ref buffer).Should().Be(300);
            buffer.Length.Should().BeGreaterThan(300, "because a full buffer may have left components out, so it grows past the count");

            var grown = buffer;
            VerletPhysicsSystem.CopyAllComponents(scene, ref buffer).Should().Be(300);
            buffer.Should().BeSameAs(grown, "because a buffer that fits is reused, with no allocation per frame");
        }
        finally
        {
            scene.Dispose();
            game.Stop();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void CopyAllComponentsGrowsAnEmptyBuffer(int components)
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        var scene = game.Scene = new Scene(game);
        try
        {
            for (int i = 0; i < components; i++)
                scene.AttachComponent(scene.CreateEntity(), new TransformComponent());

            // An empty buffer is always "full", and doubling zero stays zero: this used to loop forever.
            var buffer = Array.Empty<TransformComponent>();
            var copy = global::System.Threading.Tasks.Task.Run(() => VerletPhysicsSystem.CopyAllComponents(scene, ref buffer));
            copy.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("because copying into an empty buffer must finish");
            copy.Result.Should().Be(components);
            buffer.Length.Should().BeGreaterThan(components);
        }
        finally
        {
            scene.Dispose();
            game.Stop();
        }
    }

    [Fact]
    public void FortySimultaneousRagdollsAreAllSimulatedAndSolved()
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        var level = Registries.Levels.Get("dbg_room").Level.Value;
        var scene = game.Scene = SceneUtils.PrepareGameScene(game, GameMode.Experiment, false, level);

        try
        {
            var layerCounterBefore = FactionLayerCounter();
            var faction = new Faction("none", "none"); // one for all of them: every new faction uses up a collision layer, process-wide

            var characters = Enumerable.Range(0, RagdollCount).Select(i =>
            {
                var x = -2300 + i * 60f;
                return Prefabs.CreateCharacter(scene, new CharacterPrefabParams
                {
                    Name = "ragdoll " + i,
                    Bottom = new Vector2(x, level.GetFloorLevelAt(x)),
                    Faction = faction,
                    Look = Registries.Looks.Get("grunt"),
                    Stats = Registries.Stats.Get("grunt"),
                    Tag = Tags.EnemyAI
                });
            }).ToArray();

            FactionLayerCounter().Should().Be(layerCounterBefore << 1,
                "because this test may only use up one faction collision layer (the layers are process-wide, and shipped factions loaded later would get none)");

            // One frame so the limbs are posed before they become ragdolls, the same as in play.
            TestGameUtil.StepGame(TimeSpan.FromSeconds(1 / 60f), game);

            // All at once, the way the Konami-code kill-all does it (PlayerCharacterSystem).
            foreach (var character in characters)
            {
                character.Kill();
                MadnessUtils.TurnIntoRagdoll(scene, character);
            }

            var nodes = scene.GetAllComponentsOfType<VerletNodeComponent>().ToArray();
            var links = scene.GetAllComponentsOfType<VerletLinkComponent>().ToArray();
            nodes.Length.Should().BeGreaterThan(256, "because this test needs more nodes than the old fixed buffer held");
            links.Length.Should().BeGreaterThan(256, "because this test needs more links than the old fixed buffer held");

            // Markers that only the solver clears: SolveMotion sets every node's acceleration to zero, and SolveConstraints
            // sets the B node's rotation offset for every KeepDistance link (that mode never returns early). Nothing else
            // in the game writes RotationOffset.
            foreach (var node in nodes)
            {
                node.Acceleration = new Vector2(0, 0.25f);
                node.RotationOffset = float.NaN;
            }

            game.State.Time.DeltaTime = game.State.Time.DeltaTimeUnscaled = 1 / 60f;
            scene.GetSystem<VerletPhysicsSystem>().Update();

            var keepDistanceTargets = links.Where(l => l.Mode == VerletLinkMode.KeepDistance).Select(l => l.B).Distinct().ToArray();
            keepDistanceTargets.Should().NotBeEmpty();

            using (new AssertionScope())
            {
                var unsimulated = nodes.Count(n => n.Acceleration != Vector2.Zero);
                unsimulated.Should().Be(0, $"because every one of the {nodes.Length} nodes of {RagdollCount} ragdolls should be simulated");

                var unsolved = keepDistanceTargets.Count(n => float.IsNaN(n.RotationOffset));
                unsolved.Should().Be(0, $"because every one of the {links.Length} links of {RagdollCount} ragdolls should be solved " +
                    $"(counted as the {keepDistanceTargets.Length} nodes at the B end of a KeepDistance link)");
            }
        }
        finally
        {
            scene.Dispose();
            game.Stop();
        }
    }

    private static uint FactionLayerCounter() =>
        (uint)typeof(Faction).GetField("lastAssignedLayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
}
