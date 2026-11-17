using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

public class FrameLimiterTests
{
    /// <summary>A clock that only moves when the limiter sleeps or spins. Sleeps can overshoot, like Thread.Sleep.</summary>
    private sealed class Fake
    {
        public double Now;
        public double SleepOvershootMs;
        public double SpinStepMs = 0.05;
        public List<double> Sleeps = new();
        public double SpunMs;
        public FrameLimiter Limiter;

        public Fake(double fps)
        {
            Limiter = new FrameLimiter(() => Now, ms =>
            {
                Sleeps.Add(ms);
                Now += (ms + SleepOvershootMs) / 1000.0;
            }, () =>
            {
                SpunMs += SpinStepMs;
                Now += SpinStepMs / 1000.0;
            })
            { TargetFps = fps };
        }

        /// <summary>Simulates a frame whose own work takes the given time, then the limiter wait at the start of the next one.</summary>
        public double Frame(double workMs)
        {
            Now += workMs / 1000.0;
            Limiter.Wait();
            return Now;
        }
    }

    [Theory]
    [InlineData(300)]
    [InlineData(144)]
    [InlineData(60)]
    public void Frames_are_paced_at_the_target_rate(double fps)
    {
        var f = new Fake(fps);
        f.Limiter.Wait(); // first frame establishes the origin
        var start = f.Now;
        for (int i = 0; i < 100; i++)
            f.Frame(0.2);
        ((f.Now - start) / 100).Should().BeApproximately(1.0 / fps, 0.0002);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(144)]
    [InlineData(60)]
    public void Never_sleeps_a_negative_or_zero_amount(double fps)
    {
        var f = new Fake(fps) { SleepOvershootMs = 0.8 };
        f.Limiter.Wait();
        for (int i = 0; i < 200; i++)
            f.Frame(i % 7 * 3.0); // a mix of short and long frames
        f.Sleeps.Should().OnlyContain(s => s > 0);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(144)]
    [InlineData(60)]
    public void Spin_per_frame_stays_under_1_5_ms(double fps)
    {
        var f = new Fake(fps);
        f.Limiter.Wait();
        for (int i = 0; i < 100; i++)
        {
            var before = f.SpunMs;
            f.Frame(0.5);
            (f.SpunMs - before).Should().BeLessThan(1.5);
        }
    }

    [Fact]
    public void Sleeps_for_all_but_about_the_last_millisecond()
    {
        var f = new Fake(60);
        f.Limiter.Wait();
        f.Frame(0);
        f.Sleeps.Should().ContainSingle();
        f.Sleeps[0].Should().BeApproximately(1000.0 / 60 - FrameLimiter.SpinMarginMs, 0.01);
    }

    [Fact]
    public void A_late_frame_does_not_sleep_or_spin()
    {
        var f = new Fake(144);
        f.Limiter.Wait();
        f.Frame(50);
        f.Sleeps.Should().BeEmpty();
        f.SpunMs.Should().Be(0);
    }

    [Fact]
    public void After_a_hitch_the_next_frames_are_normal_not_caught_up()
    {
        var f = new Fake(144);
        f.Limiter.Wait();
        f.Frame(0.2);
        f.Frame(500); // hitch
        f.Sleeps.Clear();
        var times = new List<double>();
        for (int i = 0; i < 5; i++)
            times.Add(f.Frame(0.2));
        f.Sleeps.Should().HaveCount(5); // every frame still waits
        for (int i = 1; i < times.Count; i++)
            (times[i] - times[i - 1]).Should().BeApproximately(1.0 / 144, 0.0002);
    }

    [Fact]
    public void Loop_event_starts_at_the_startup_cap_of_144()
    {
        // Reads the static before anything in the test run sets it; nothing but Settings.Apply does.
        FrameLimiterLoopEvent.StartupCap.Should().Be(144);
        FrameLimiterLoopEvent.Cap.Should().Be(FrameLimiterLoopEvent.StartupCap);
    }

    [Fact]
    public void Uncapped_never_waits()
    {
        var f = new Fake(0);
        f.Limiter.Wait();
        for (int i = 0; i < 50; i++)
            f.Frame(0.1);
        f.Sleeps.Should().BeEmpty();
        f.SpunMs.Should().Be(0);
    }

    [Fact]
    public void Negative_target_counts_as_uncapped()
    {
        var f = new Fake(-5);
        f.Frame(0.1);
        f.Frame(0.1);
        f.Sleeps.Should().BeEmpty();
    }

    [Fact]
    public void Changing_the_cap_takes_effect_on_the_next_frame()
    {
        var f = new Fake(0);
        f.Limiter.Wait();
        f.Frame(0.1);
        f.Limiter.TargetFps = 60;
        var t0 = f.Now;
        f.Frame(0.1);
        f.Frame(0.1);
        (f.Now - t0).Should().BeGreaterThan(1.0 / 60);
    }
}
