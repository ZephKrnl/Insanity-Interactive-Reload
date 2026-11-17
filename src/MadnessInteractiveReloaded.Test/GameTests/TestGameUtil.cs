using System;
using Microsoft.Extensions.Logging;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.OpenTK;

namespace MIR.Test.GameTests;

public struct TestGameUtil
{
    /// <param name="logger">The game logger. When null the game makes its own, which logs to a file named after the current second, so pass one when creating several games quickly.</param>
    public static Game Create(ILogger? logger = null)
    {
        Game game = new Game(new FakeWindow(), logger: logger);

        BaseAssetsFixture.EnsureRegistered();
        Resources.RegisterType(typeof(AudioData), static p => new FixedAudioData([], 256, 2, 512));
        Resources.RegisterType(typeof(FixedAudioData), static p => new FixedAudioData([], 256, 2, 512));
        //Resources.RegisterType(typeof(StreamAudioData), static p => new StreamAudioData(() => new OggAudioStream(p), 256, 2, 512));
        Registries.ClearAll();
        Registries.LoadWeapons();
        Registries.LoadArmour();
        Registries.LoadStats();
        Registries.LoadLooks();
        Registries.LoadAnimations();
        Registries.LoadLevels();
        Registries.LoadMeleeSequences();
        Registries.LoadCutscenes();
        Registries.LoadCampaigns();
        game.Window.Initialise();

        return game;
    }

    /// <summary>
    /// Runs the game for <paramref name="span"/> of simulated time at 60 frames per second, the way <c>Game.Start</c> does
    /// (Walgelijk 0.30.0): audio and console, loop events, fixed updates (capped at <see cref="Game.MaxFixedUpdatesPerFrame"/>
    /// per frame, also dispatched to loop events), scene updates and <see cref="RoutineScheduler.StepRoutines"/> with the
    /// scaled delta time, then optionally the render systems, then <c>LoopCycle</c>. It skips the clock, the compositor and the render queue.
    /// </summary>
    /// <param name="renderSystems">Also runs <see cref="Scene.RenderSystems"/> every frame. Off by default, because most tests don't need it
    /// and rendering makes them slower and touches more state. Only call it with a game that has a <see cref="FakeWindow"/>, which uses <c>MockGraphics</c>.</param>
    public static void StepGame(TimeSpan span, Game game, bool renderSystems = false)
    {
        if (game.Window is FakeWindow fake)
        {
            var state = game.State;
            var dt = 1 / 60f;
            var fakeTime = TimeSpan.Zero;
            var window = fake;

            double accumulator = 0;

            while (fakeTime < span)
            {
                var unscaledDt = (float)dt;
                var scaledDt = (float)dt * state.Time.TimeScale;

                state.Time.DeltaTimeUnscaled = unscaledDt;
                state.Time.DeltaTime = scaledDt;

                state.Time.SecondsSinceSceneChange += unscaledDt;
                state.Time.SecondsSinceSceneChangeUnscaled += scaledDt;

                state.Time.SecondsSinceLoad += scaledDt;
                state.Time.SecondsSinceLoadUnscaled += unscaledDt;

                game.AudioRenderer.UpdateTracks();
                game.Console.Update();
                game.AudioRenderer.Process(unscaledDt);

                foreach (var e in game.AdditionalLoopEvents)
                    e.Update(game, unscaledDt);

                double fixedUpdateInterval = 1d / game.FixedUpdateRate;
                if (!game.Console.IsActive)
                {
                    accumulator += scaledDt;
                    int iteration = 0;
                    while (accumulator > fixedUpdateInterval)
                    {
                        game.Scene?.FixedUpdateSystems();
                        foreach (var e in game.AdditionalLoopEvents)
                            e.FixedUpdate(game, unscaledDt);
                        accumulator -= fixedUpdateInterval;
                        iteration++;
                        if (iteration >= game.MaxFixedUpdatesPerFrame)
                            break;
                    }

                    state.Time.FixedInterval = (float)fixedUpdateInterval;
                    state.Time.Interpolation = (float)(accumulator / fixedUpdateInterval);

                    game.Scene?.UpdateSystems();
                    RoutineScheduler.StepRoutines(scaledDt);
                    game.Profiling.Tick();
                }

                if (renderSystems)
                    game.Scene?.RenderSystems();

                window.LoopCycle();

                if (!window.IsOpen)
                    break;

                fakeTime += TimeSpan.FromSeconds(dt);
            }

        }
        else throw new Exception("game is not a fake game...");
    }
}
