using System;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace MIR.Test.Shared;

[Collection("Registry collection")]
public class BenchmarkStatsTests
{
    [Theory]
    [InlineData(60, 60)]
    [InlineData(60, 144)]
    [InlineData(60, 300)]
    [InlineData(600, 0)] // uncapped, the longest run
    public void RecordingAFullRunDoesNotGrowTheFrameTimeList(int seconds, int frameCap)
    {
        var stats = new BenchmarkFrameTimes();
        stats.Reserve(BenchmarkFrameTimes.ExpectedFrames(seconds, frameCap));
        var capacity = stats.Capacity;

        var fps = frameCap > 0 ? frameCap : BenchmarkFrameTimes.UncappedFrameRateCeiling;
        for (int i = 0; i < seconds * fps; i++)
            stats.Add(1d / fps);

        stats.Capacity.Should().Be(capacity, "because growing the list during the measured window allocates (large object heap) and skews allocated_mb and gc2");
    }

    [Fact]
    public void ExpectedFramesCoversTheRunWithHeadroom()
    {
        BenchmarkFrameTimes.ExpectedFrames(60, 144).Should().BeGreaterThan(60 * 144);
        BenchmarkFrameTimes.ExpectedFrames(60, 0).Should().BeGreaterThan(60 * BenchmarkFrameTimes.UncappedFrameRateCeiling);
    }

    [Fact]
    public void PercentilesInterpolateBetweenTheClosestRanks()
    {
        var stats = new BenchmarkFrameTimes();
        foreach (var i in Enumerable.Range(1, 100).Reverse()) // order must not matter
            stats.Add(i);

        stats.Count.Should().Be(100);
        stats.Percentile(0).Should().Be(1);
        stats.Percentile(100).Should().Be(100);
        stats.Percentile(50).Should().BeApproximately(50.5, 1e-9);
        stats.Percentile(95).Should().BeApproximately(95.05, 1e-9);
        stats.Percentile(99).Should().BeApproximately(99.01, 1e-9);
        stats.Mean.Should().BeApproximately(50.5, 1e-9);
        stats.Max.Should().Be(100);
    }

    [Fact]
    public void ASingleSampleIsEveryPercentile()
    {
        var stats = new BenchmarkFrameTimes();
        stats.Add(0.016);
        stats.Percentile(50).Should().Be(0.016);
        stats.Percentile(99).Should().Be(0.016);
    }

    [Fact]
    public void NoSamplesGiveZeroInsteadOfThrowing()
    {
        var stats = new BenchmarkFrameTimes();
        stats.Count.Should().Be(0);
        stats.Percentile(50).Should().Be(0);
        stats.Mean.Should().Be(0);
        stats.Max.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    [InlineData(double.NaN)]
    public void PercentileOutsideZeroToHundredIsRejected(double p)
    {
        var stats = new BenchmarkFrameTimes();
        stats.Add(1);
        var act = () => stats.Percentile(p);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddingAfterAPercentileStillGivesTheRightAnswer()
    {
        var stats = new BenchmarkFrameTimes();
        stats.Add(3);
        stats.Add(1);
        stats.Percentile(100).Should().Be(3);
        stats.Add(5);
        stats.Percentile(100).Should().Be(5);
        stats.Percentile(0).Should().Be(1);
    }

    private static BenchmarkReport SampleReport() => new()
    {
        TimestampUtc = new DateTime(2026, 11, 17, 13, 45, 7, DateTimeKind.Utc),
        Version = "0.1.0",
        BuildConfiguration = "Release",
        Level = "exp_2",
        AiCount = 10,
        SecondsRequested = 60,
        SecondsRecorded = 60.0123,
        FrameCap = "300",
        VSync = false,
        Frames = 3600,
        P50Ms = 2.5,
        P95Ms = 4.25,
        P99Ms = 7.125,
        MeanMs = 2.75,
        MaxMs = 31.5,
        Gc0 = 12,
        Gc1 = 3,
        Gc2 = 1,
        AllocatedMegabytes = 123.456,
        PeakEntities = 900,
        FinalEntities = 850,
        RagdollsSpawned = 70,
        PeakRagdolls = 12,
        ShotsPerMinute = 345.6,
        MeleeAttacksPerMinute = 12.3,
    };

    [Fact]
    public void CsvHasOneHeaderAndOneRowWithTheSameColumnCount()
    {
        var csv = SampleReport().ToCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        lines.Should().HaveCount(2);
        lines[0].Should().Be(BenchmarkReport.CsvHeader);
        lines[1].Split(',').Length.Should().Be(lines[0].Split(',').Length);
    }

    [Fact]
    public void CsvRowHasTheValuesInHeaderOrderWithInvariantNumbers()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL"); // a comma decimal separator must not leak into the CSV
        try
        {
            var header = BenchmarkReport.CsvHeader.Split(',');
            var row = SampleReport().ToCsvRow().Split(',');
            string Col(string name) => row[Array.IndexOf(header, name)];

            Col("timestamp_utc").Should().Be("2026-11-17T13:45:07Z");
            Col("version").Should().Be("0.1.0");
            Col("build").Should().Be("Release");
            Col("level").Should().Be("exp_2");
            Col("ai").Should().Be("10");
            Col("seconds").Should().Be("60");
            Col("seconds_recorded").Should().Be("60.012");
            Col("frame_cap").Should().Be("300");
            Col("vsync").Should().Be("false");
            Col("frames").Should().Be("3600");
            Col("p50_ms").Should().Be("2.500");
            Col("p95_ms").Should().Be("4.250");
            Col("p99_ms").Should().Be("7.125");
            Col("mean_ms").Should().Be("2.750");
            Col("max_ms").Should().Be("31.500");
            Col("gc0").Should().Be("12");
            Col("gc1").Should().Be("3");
            Col("gc2").Should().Be("1");
            Col("allocated_mb").Should().Be("123.46");
            Col("peak_entities").Should().Be("900");
            Col("final_entities").Should().Be("850");
            Col("ragdolls_spawned").Should().Be("70");
            Col("peak_ragdolls").Should().Be("12");
            Col("shots_per_min").Should().Be("345.6");
            Col("melee_per_min").Should().Be("12.3");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CsvFieldsWithCommasOrQuotesAreQuoted()
    {
        var row = (SampleReport() with { Version = "1,2 \"beta\"" }).ToCsvRow();
        row.Should().Contain("\"1,2 \"\"beta\"\"\"");
    }

    [Fact]
    public void PerMinuteRateUsesTheRecordedSeconds()
    {
        BenchmarkReport.PerMinute(30, 60).Should().Be(30);
        BenchmarkReport.PerMinute(30, 30).Should().Be(60);
        BenchmarkReport.PerMinute(5, 0).Should().Be(0);
    }

    [Fact]
    public void WriterCreatesTheFolderAndOneFilePerRun()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iir-bench-test-" + Guid.NewGuid().ToString("N"), "benchmarks");
        try
        {
            BenchmarkCsvWriter.TryWrite(dir, SampleReport(), out var first).Should().BeTrue();
            BenchmarkCsvWriter.TryWrite(dir, SampleReport(), out var second).Should().BeTrue();

            first.Should().NotBe(second, "because a second run in the same second must not overwrite the first");
            File.ReadAllText(first!).Should().Be(SampleReport().ToCsv());
            Path.GetFileName(first).Should().StartWith("benchmark-20261117-134507-ai10-60s").And.EndWith(".csv");
            Directory.GetFiles(dir).Should().HaveCount(2, "because no temporary file is left behind");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(dir)!, true); } catch { }
        }
    }

    [Fact]
    public void WriterReturnsFalseInsteadOfThrowingWhenTheFolderCannotBeMade()
    {
        var file = Path.GetTempFileName(); // a file where the folder should be
        try
        {
            BenchmarkCsvWriter.TryWrite(Path.Combine(file, "benchmarks"), SampleReport(), out var path).Should().BeFalse();
            path.Should().BeNull();
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(10, 60, true)]
    [InlineData(1, 5, true)]
    [InlineData(BenchmarkOptions.MaxAi, BenchmarkOptions.MaxSeconds, true)]
    [InlineData(0, 60, false)]
    [InlineData(-3, 60, false)]
    [InlineData(BenchmarkOptions.MaxAi + 1, 60, false)]
    [InlineData(10, BenchmarkOptions.MinSeconds - 1, false)]
    [InlineData(10, BenchmarkOptions.MaxSeconds + 1, false)]
    public void OptionsValidateTheAiCountAndDuration(int ai, int seconds, bool valid)
    {
        BenchmarkOptions.TryCreate(ai, seconds, out var options, out var error).Should().Be(valid);
        if (valid)
        {
            options!.AiCount.Should().Be(ai);
            options.Seconds.Should().Be(seconds);
            error.Should().BeNull();
        }
        else
        {
            options.Should().BeNull();
            error.Should().NotBeNullOrWhiteSpace();
        }
    }
}
