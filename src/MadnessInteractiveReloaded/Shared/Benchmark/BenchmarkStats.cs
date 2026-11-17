using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MIR;

/// <summary>
/// What a benchmark run does: how many AI, for how long, and where the result goes. Make one with <see cref="TryCreate"/>,
/// which checks the ranges.
/// </summary>
public sealed record BenchmarkOptions
{
    public const int DefaultAi = 10;
    public const int DefaultSeconds = 60;
    public const int MinAi = 1;

    /// <summary><see cref="AiCharacterSystem"/> handles at most <see cref="AiCharacterSystem.MaxAiCount"/> AI; the player is not one.</summary>
    public const int MaxAi = AiCharacterSystem.MaxAiCount;

    public const int MinSeconds = 5;
    public const int MaxSeconds = 600;

    /// <summary>The level the benchmark runs in: an experiment room with one flat floor between two walls, and no script.</summary>
    public const string DefaultLevel = "exp_2";

    public int AiCount { get; init; } = DefaultAi;
    public int Seconds { get; init; } = DefaultSeconds;
    public string Level { get; init; } = DefaultLevel;

    /// <summary>Seconds between scripted single kills.</summary>
    public float KillInterval { get; init; } = 1;

    /// <summary>Seconds between scripted mass kills (half of the live AI at once, the BUG-47 case).</summary>
    public float MassKillInterval { get; init; } = 15;

    /// <summary>The most AI spawned in one frame to replace the dead, after the first frame.</summary>
    public int MaxSpawnsPerFrame { get; init; } = 2;

    /// <summary>Where the CSV goes. Null: <see cref="BenchmarkCsvWriter.DefaultDirectory"/>.</summary>
    public string? OutputDirectory { get; init; }

    /// <summary>Stop the game when the run is done (<c>--mode benchmark</c>).</summary>
    public bool ExitWhenDone { get; init; }

    public static bool TryCreate(int ai, int seconds, out BenchmarkOptions? options, out string? error)
    {
        options = null;
        if (ai < MinAi || ai > MaxAi)
        {
            error = $"The AI count must be between {MinAi} and {MaxAi}, not {ai}.";
            return false;
        }

        if (seconds < MinSeconds || seconds > MaxSeconds)
        {
            error = $"The duration must be between {MinSeconds} and {MaxSeconds} seconds, not {seconds}.";
            return false;
        }

        error = null;
        options = new BenchmarkOptions { AiCount = ai, Seconds = seconds };
        return true;
    }
}

/// <summary>
/// Frame times of a benchmark run, and their percentiles. Any unit: what goes in comes out.
/// </summary>
public sealed class BenchmarkFrameTimes
{
    private readonly List<double> samples = new(8192);
    private double[]? sorted;
    private double sum;
    private double max;

    public int Count => samples.Count;
    public double Mean => samples.Count == 0 ? 0 : sum / samples.Count;
    public double Max => max;

    public void Add(double value)
    {
        if (samples.Count == 0 || value > max)
            max = value;
        samples.Add(value);
        sum += value;
        sorted = null;
    }

    /// <summary>
    /// The <paramref name="percentile"/>th percentile (0 to 100), interpolated linearly between the two closest ranks (the
    /// same as numpy's default and Excel's <c>PERCENTILE.INC</c>). Zero when there are no samples.
    /// </summary>
    public double Percentile(double percentile)
    {
        if (!(percentile >= 0 && percentile <= 100))
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "A percentile is between 0 and 100.");

        if (samples.Count == 0)
            return 0;

        if (sorted == null)
        {
            sorted = samples.ToArray();
            Array.Sort(sorted);
        }

        var rank = percentile / 100 * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }
}

/// <summary>
/// The result of one benchmark run: one CSV row.
/// </summary>
public sealed record BenchmarkReport
{
    public DateTime TimestampUtc { get; init; }
    public string Version { get; init; } = string.Empty;
    public string BuildConfiguration { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public int AiCount { get; init; }
    public int SecondsRequested { get; init; }
    public double SecondsRecorded { get; init; }

    /// <summary>The frame cap in effect: a number, <c>uncapped</c>, or <c>none</c> when there is no frame limiter (a test).</summary>
    public string FrameCap { get; init; } = string.Empty;

    public bool VSync { get; init; }
    public int Frames { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double MeanMs { get; init; }
    public double MaxMs { get; init; }
    public int Gc0 { get; init; }
    public int Gc1 { get; init; }
    public int Gc2 { get; init; }
    public double AllocatedMegabytes { get; init; }
    public int PeakEntities { get; init; }
    public int FinalEntities { get; init; }
    public int RagdollsSpawned { get; init; }
    public int PeakRagdolls { get; init; }
    public double ShotsPerMinute { get; init; }
    public double MeleeAttacksPerMinute { get; init; }

    public const string CsvHeader =
        "timestamp_utc,version,build,level,ai,seconds,seconds_recorded,frame_cap,vsync,frames," +
        "p50_ms,p95_ms,p99_ms,mean_ms,max_ms,gc0,gc1,gc2,allocated_mb," +
        "peak_entities,final_entities,ragdolls_spawned,peak_ragdolls,shots_per_min,melee_per_min";

    public static double PerMinute(long count, double seconds) => seconds <= 0 ? 0 : count / (seconds / 60);

    public string ToCsvRow()
    {
        var c = CultureInfo.InvariantCulture;
        string[] fields =
        [
            TimestampUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", c),
            Version,
            BuildConfiguration,
            Level,
            AiCount.ToString(c),
            SecondsRequested.ToString(c),
            SecondsRecorded.ToString("0.000", c),
            FrameCap,
            VSync ? "true" : "false",
            Frames.ToString(c),
            P50Ms.ToString("0.000", c),
            P95Ms.ToString("0.000", c),
            P99Ms.ToString("0.000", c),
            MeanMs.ToString("0.000", c),
            MaxMs.ToString("0.000", c),
            Gc0.ToString(c),
            Gc1.ToString(c),
            Gc2.ToString(c),
            AllocatedMegabytes.ToString("0.00", c),
            PeakEntities.ToString(c),
            FinalEntities.ToString(c),
            RagdollsSpawned.ToString(c),
            PeakRagdolls.ToString(c),
            ShotsPerMinute.ToString("0.0", c),
            MeleeAttacksPerMinute.ToString("0.0", c),
        ];

        var b = new StringBuilder();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
                b.Append(',');
            b.Append(Escape(fields[i]));
        }
        return b.ToString();
    }

    /// <summary>The whole file: the header and this row, each ending in <c>\n</c>.</summary>
    public string ToCsv() => CsvHeader + "\n" + ToCsvRow() + "\n";

    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}

/// <summary>
/// Writes a <see cref="BenchmarkReport"/> to its own CSV file. Never throws: a failure is logged and reported as <c>false</c>.
/// </summary>
public static class BenchmarkCsvWriter
{
    /// <summary>
    /// <c>%LOCALAPPDATA%\IIR\benchmarks</c>: the game's AppData folder (next to <c>userdata</c> and the logs), or a temp folder
    /// when there is no game.
    /// </summary>
    public static string DefaultDirectory
    {
        get
        {
            var appData = Walgelijk.Game.Main?.AppDataDirectory;
            return string.IsNullOrEmpty(appData)
                ? Path.Combine(Path.GetTempPath(), "IIR-no-game", "benchmarks")
                : Path.Combine(appData, "benchmarks");
        }
    }

    public static bool TryWrite(string directory, BenchmarkReport report, out string? path)
    {
        path = null;
        string? tmp = null;
        try
        {
            Directory.CreateDirectory(directory);

            var stem = $"benchmark-{report.TimestampUtc.ToUniversalTime():yyyyMMdd-HHmmss}-ai{report.AiCount}-{report.SecondsRequested}s";
            var target = Path.Combine(directory, stem + ".csv");
            for (int i = 2; File.Exists(target); i++)
                target = Path.Combine(directory, $"{stem}-{i}.csv");

            // Written next to the target and then moved, so a half-written file never has the .csv name.
            tmp = target + ".tmp";
            File.WriteAllText(tmp, report.ToCsv(), new UTF8Encoding(false));
            File.Move(tmp, target, overwrite: false);
            tmp = null;

            path = target;
            return true;
        }
        catch (Exception e)
        {
            Walgelijk.Logger.Error($"Could not write the benchmark result to {directory}: {e.Message}", nameof(BenchmarkCsvWriter));
            return false;
        }
        finally
        {
            if (tmp != null)
                try { File.Delete(tmp); } catch { }
        }
    }
}
