using System.Diagnostics;
using System.Threading;
using Walgelijk;

namespace MIR;

/// <summary>
/// Limits the frame rate by sleeping, instead of the engine's busy-wait (which is off when <see cref="Game.UpdateRate"/> is 0).
/// It waits at the start of the frame, so the wait is part of the frame time the engine measures and frame pacing stays even.
/// Register it before <see cref="ControllerLoopEvent"/> so the pad is polled after the wait, with the freshest input.
/// </summary>
public sealed class FrameLimiterLoopEvent : IGameLoopEvent
{
    /// <summary>
    /// The frame cap in frames per second. Zero means uncapped.
    /// </summary>
    public static int Cap
    {
        get => (int)Limiter.TargetFps;
        set => Limiter.TargetFps = value;
    }

    private static readonly FrameLimiter Limiter = new(
        () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
        ms => Thread.Sleep(System.TimeSpan.FromMilliseconds(ms)),
        () => Thread.SpinWait(20));

    public void Update(Game game, float dt) => Limiter.Wait();

    public void FixedUpdate(Game game, float interval)
    {
    }
}
