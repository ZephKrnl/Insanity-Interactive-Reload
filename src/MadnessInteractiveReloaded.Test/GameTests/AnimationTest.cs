using FluentAssertions;
using System;
using System.Numerics;
using Walgelijk;
using Walgelijk.AssetManager;
using Xunit;

namespace MIR.Test.GameTests;

[Collection("Registry collection")]
public class AnimationTest : global::System.IDisposable
{
    public AnimationTest()
    {
        BaseAssetsFixture.EnsureRegistered();
        Registries.ClearAll();
    }

    [Fact]
    public void AnimationMixingTest()
    {
        var game = TestGameUtil.Create();
        Scene scene = game.Scene = SceneUtils.PrepareGameScene(game, GameMode.Experiment, false, Registries.Levels.Get("dbg_room").Level.Value);
        var character = Prefabs.CreateCharacter(scene, new CharacterPrefabParams
        {
            Name = string.Empty,
            Bottom = default,
            Faction = new Faction("none", "none"),
            Look = Registries.Looks.Get("grunt"),
            Stats = Registries.Stats.Get("grunt"),
            Tag = Tags.EnemyAI
        });
        scene.UpdateSystems();

        scene.HasEntity(character.Entity).Should().BeTrue();

        var bodyTransform = scene.GetComponentFrom<TransformComponent>(character.Positioning.Body.Entity);
        character.PlayAnimation(Animations.Dodge[0]);
        var duration = Animations.Dodge[0].TotalDuration;
        // play animation

        character.Animations.Should().ContainSingle();
        character.MainAnimation.Should().NotBeNull();
        //character.IsPlayingAnimationOfType(AnimationType.Jump).Should().BeFalse();
        //character.IsPlayingAnimationOfType(AnimationType.Dodge).Should().BeTrue();
        // TODO test animation constraints instead

        // verify that it and it alone is playing

        TestGameUtil.StepGame(TimeSpan.FromSeconds(duration / 4), game);
        character.MainAnimation!.ScaledTimer.Should().BeApproximately(character.MainAnimation.ScaledDuration / 4, 0.05f,
            "because this amount of time has been simulated so the animation should have progressed too.");
        // simulate game for half the duration, make sure the animation has been playing

        character.PlayAnimation(Animations.DeathByHeadshot.FromFront[0]);
        // play new animation in the middle of another animation

        TestGameUtil.StepGame(TimeSpan.FromSeconds(0.5f), game);
        character.IsPlayingAnimation.Should().BeTrue();
        character.Animations.Should().HaveCount(2, "because both animations should still be playing");
        // verify that both animations are still playing

        // Check the blend part-way through the mix, where a broken blend shows. Once the mix is over the result is just the
        // last animation, whatever the blend does. The mix progress is set by hand, so the test doesn't depend on timing.
        foreach (var progress in new[] { 0.25f, 0.5f, 0.75f })
        {
            character.AnimationMixProgress = progress;
            var factor = Easings.Quad.InOut(progress); // what CalculateMixedAnimation eases the progress with
            var from = character.Animations[0];
            var to = character.Animations[1];

            var mixedState = CharacterUtilities.CalculateMixedAnimation(character);
            mixedState.BodyRotation.Should().BeApproximately(Utilities.LerpAngle(from.GetBodyRotation(), to.GetBodyRotation(), factor), 0.05f, $"at {progress:P0} of the mix");
            (Vector2.Lerp(from.GetBodyPosition(), to.GetBodyPosition(), factor) - mixedState.BodyPosition).LengthSquared().Should().BeLessThan(0.05f, $"at {progress:P0} of the mix");
        }

        // verify the mixed animation state

        game.Scene.Dispose();
        game.Stop();
    }

    [Fact]
    public void BasicAnimationTest()
    {
        var game = TestGameUtil.Create();
        Scene scene = game.Scene = SceneUtils.PrepareGameScene(game, GameMode.Experiment, false, Registries.Levels.Get("dbg_room").Level.Value);
        var character = Prefabs.CreateCharacter(scene, new CharacterPrefabParams
        {
            Name = string.Empty,
            Bottom = default,
            Faction = new Faction("none", "none"),
            Look = Registries.Looks.Get("grunt"),
            Stats = Registries.Stats.Get("grunt"),
            Tag = Tags.EnemyAI
        });
        scene.UpdateSystems();

        scene.HasEntity(character.Entity).Should().BeTrue();

        character.PlayAnimation(Animations.Dodge[0]);
        // play animation

        character.IsPlayingAnimation.Should().BeTrue();
        //character.IsPlayingAnimationOfType(AnimationType.Jump).Should().BeFalse();
        //character.IsPlayingAnimationOfType(AnimationType.Dodge).Should().BeTrue();
        // TODO test animation constraints instead
        character.MainAnimation.Should().NotBeNull();
        // verify that it and it alone is playing

        TestGameUtil.StepGame(TimeSpan.FromSeconds(character.MainAnimation!.ScaledDuration / 2), game);
        character.MainAnimation.ScaledTimer.Should().BeApproximately(character.MainAnimation.ScaledDuration / 2, 0.05f,
            "because this amount of time has been simulated so the animation should have progressed too");
        // simulate game for half the animation duration and verify that it has been playing for that amount of time

        TestGameUtil.StepGame(TimeSpan.FromSeconds(10), game);
        character.MainAnimation.Should().BeNull("because no animations should be playing");
        character.IsPlayingAnimation.Should().BeFalse();
        // simulate game for 10 seconds and verify that the animation has stopped playing

        game.Scene.Dispose();
        game.Stop();
    }

    public void Dispose()
    {
        Registries.ClearAll();
    }
}
