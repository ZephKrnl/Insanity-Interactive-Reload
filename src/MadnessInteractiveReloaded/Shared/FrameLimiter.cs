using System;

namespace MIR;

/// <summary>
/// The timing core of the frame limiter, with the clock, the sleep and the spin injected so it can be tested.
/// <see cref="Wait"/> is called once per frame, at the start of the frame, and blocks until one frame time has passed
/// since the previous call returned. It sleeps for all but the last <see cref="SpinMarginMs"/> and spins for the rest,
/// because Thread.Sleep can overshoot by about a millisecond even with a 1 ms timer period.
/// </summary>
public sealed class FrameLimiter
{
    /// <summary>
    /// The part of the wait that is spun instead of slept, in milliseconds.
    /// </summary>
    public const double SpinMarginMs = 1.0;

    private readonly Func<double> nowSeconds;
    private readonly Action<double> sleepMilliseconds;
    private readonly Action spinOnce;

    private double frameStart;
    private bool started;

    /// <summary>
    /// Frames per second to hold. Zero or less means uncapped: <see cref="Wait"/> returns immediately.
    /// </summary>
    public double TargetFps { get; set; }

    /// <param name="nowSeconds">A monotonic clock in seconds.</param>
    /// <param name="sleepMilliseconds">Gives up the thread for about this many milliseconds. Only called with a positive value.</param>
    /// <param name="spinOnce">One short busy-wait step.</param>
    public FrameLimiter(Func<double> nowSeconds, Action<double> sleepMilliseconds, Action spinOnce)
    {
        this.nowSeconds = nowSeconds;
        this.sleepMilliseconds = sleepMilliseconds;
        this.spinOnce = spinOnce;
    }

    public void Wait()
    {
        var now = nowSeconds();
        if (TargetFps <= 0 || !started)
        {
            started = true;
            frameStart = now;
            return;
        }

        var frameTime = 1.0 / TargetFps;
        var deadline = frameStart + frameTime;

        var sleepMs = (deadline - now) * 1000.0 - SpinMarginMs;
        if (sleepMs > 0)
            sleepMilliseconds(sleepMs);

        while (nowSeconds() < deadline)
            spinOnce();

        now = nowSeconds();
        // Keep the pace even by stepping from the deadline. After a hitch (more than a whole frame late) start again from
        // now instead, so the following frames are not run back to back to catch up.
        frameStart = now - deadline > frameTime ? now : deadline;
    }
}
