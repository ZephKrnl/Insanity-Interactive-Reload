using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Walgelijk;
using Xunit;

namespace MIR.Test.GameTests;

[Collection("Registry collection")]
public class StepGameTests : global::System.IDisposable
{
    public StepGameTests()
    {
        RoutineScheduler.StopAll();
    }

    public void Dispose()
    {
        RoutineScheduler.StopAll();
    }

    [Fact]
    public void DelayedRoutineCompletesAfterExpectedSteps()
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        bool done = false;
        RoutineScheduler.Start(routine());

        TestGameUtil.StepGame(TimeSpan.FromSeconds(0.5), game);
        done.Should().BeFalse("because only half of the delay has been simulated");

        TestGameUtil.StepGame(TimeSpan.FromSeconds(0.75), game);
        done.Should().BeTrue("because more than the full delay has been simulated, and the engine steps routines every frame");

        game.Stop();

        IEnumerator<IRoutineCommand> routine()
        {
            yield return new RoutineDelay(1);
            done = true;
        }
    }

    [Fact]
    public void RoutineDoesNotProgressWithZeroTimeScale()
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        bool done = false;
        game.State.Time.TimeScale = 0;
        RoutineScheduler.Start(routine());

        TestGameUtil.StepGame(TimeSpan.FromSeconds(2), game);
        done.Should().BeFalse("because routines are stepped with the scaled delta time");

        game.State.Time.TimeScale = 1;
        game.Stop();

        IEnumerator<IRoutineCommand> routine()
        {
            yield return new RoutineDelay(1);
            done = true;
        }
    }

    [Fact]
    public void FixedUpdatesPerFrameAreCapped()
    {
        var game = TestGameUtil.Create(NullLogger.Instance);
        var counter = new CountingLoopEvent();
        game.AdditionalLoopEvents.Add(counter);
        game.MaxFixedUpdatesPerFrame = 2;
        game.State.Time.TimeScale = 100; // one 1/60 s frame asks for many fixed updates

        TestGameUtil.StepGame(TimeSpan.FromSeconds(1d / 60d), game);
        counter.FixedUpdates.Should().BeInRange(1, 2, "because the engine runs fixed updates for loop events too, but never more than the cap per frame");
        game.State.Time.TimeScale = 1;
        game.Stop();
    }

    private sealed class CountingLoopEvent : IGameLoopEvent
    {
        public int FixedUpdates;
        public void Update(Game game, float dt) { }
        public void FixedUpdate(Game game, float interval) => FixedUpdates++;
    }
}
