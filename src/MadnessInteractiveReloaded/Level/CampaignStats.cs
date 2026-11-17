using MIR.Serialisation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Walgelijk;

namespace MIR;

/// <summary>
/// Statistics for a played campaign
/// </summary>
public class CampaignStats
{
    /// <summary>
    /// The associated campaign
    /// </summary>
    public string CampaignId = string.Empty;

    /// <summary>
    /// The format of the file this was read from, migrated to <see cref="SaveFormat.CampaignStatsVersion"/> on load. A file
    /// without one (from MIR, or IIR before versions) counts as 0.
    /// </summary>
    public int FormatVersion = SaveFormat.CampaignStatsVersion;

    /// <summary>
    /// True when its file existed but could not be opened at load (locked by another program, or no access). The stats are
    /// used in memory, but <see cref="Save"/> does nothing for the rest of the session, so the file is never overwritten.
    /// </summary>
    [JsonIgnore]
    public bool DoNotSave { get; set; }

    /// <summary>
    /// Current level progress
    /// </summary>
    public int LevelIndex;

    /// <summary>
    /// Current level progress, clamped to be inside the bounds. This is useful because <see cref="LevelIndex"/> can equal the total amount of levels if the campaign was completed.
    /// </summary>
    [JsonIgnore]
    public int LevelIndexClamped
    {
        get
        {
            if (Registries.Campaigns.TryGet(CampaignId, out var c))
                return int.Clamp(LevelIndex, 0, c.Levels.Length - 1);
            return LevelIndex;
        }
    }

    /// <summary>
    /// Stats per level
    /// </summary>
    public Dictionary<string, LevelStats> ByLevel = new();

    public CampaignStats()
    {

    }

    public CampaignStats(string campaignId)
    {
        CampaignId = campaignId;
    }

    [JsonIgnore]
    public TimeSpan TotalTimeSpent => TimeSpan.FromTicks(ByLevel.Values.Sum(static l => l.TotalTimeSpent.Ticks));

    [JsonIgnore]
    public int TotalKills => ByLevel.Values.Sum(static l => l.Kills);

    [JsonIgnore]
    public int TotalDeaths => ByLevel.Values.Sum(static l => l.Deaths);

    [JsonIgnore]
    public int LevelsPassedFirstTry => ByLevel.Values.Count(static l => l.Attempts == 1);

    /// <summary>
    /// Path to the file for this instance. Note that this file might not exist yet.
    /// </summary>
    public string GetFilePath() => UserData.Paths.CampaignStatsDir + CampaignId + ".json";

    public void IncrementKills(Level level)
    {
        var entry = ByLevel.Ensure(level.Id);
        entry.Kills++;
    }

    public void IncrementAttempts(Level level)
    {
        var entry = ByLevel.Ensure(level.Id);
        entry.Attempts++;
    }

    public void IncrementDeaths(Level level)
    {
        var entry = ByLevel.Ensure(level.Id);
        entry.Deaths++;
    }

    public void IncrementTimer(Level level, float dtSeconds)
    {
        var entry = ByLevel.Ensure(level.Id);
        entry.TotalTimeSpent += TimeSpan.FromSeconds(dtSeconds);
    }

    public bool IsLevelLocked(string levelId)
    {
        if (Registries.Campaigns.TryGet(CampaignId, out var c))
        {
            var levelIndex = Array.IndexOf(c.Levels, levelId);
            return levelIndex == -1 || levelIndex > LevelIndex;
        }
        return true;
    }

    /// <summary>
    /// Create a <see cref="CampaignStats"/> registry (<see cref="Registries.CampaignStats"/>) entry and instance for the given campaign ID.
    /// When a file for it exists (the registry did not load it), it is read with the usual recovery: its backup is used if it
    /// is unreadable, and an unreadable file is moved aside to <c>.corrupt-…</c> so the campaign starts fresh. A file that
    /// cannot be opened gives stats that are never saved (<see cref="DoNotSave"/>). It never throws because of the file.
    /// </summary>
    /// <exception cref="InvalidOperationException">The registry already has an entry for <paramref name="campaignId"/>.</exception>
    public static CampaignStats CreateFor(string campaignId)
    {
        if (Registries.CampaignStats.Has(campaignId))
            throw new InvalidOperationException("The registry already contains a campaign stats entry for " + campaignId);

        var stats = Load(UserData.Paths.CampaignStatsDir + campaignId + ".json");
        if (stats != null)
            stats.CampaignId = campaignId; // the file is named after the campaign; that wins over what it says inside
        stats ??= new CampaignStats(campaignId);

        Registries.CampaignStats.Register(campaignId, stats);
        return stats;
    }

    /// <summary>
    /// Read a campaign stats file, with recovery (see <see cref="SaveFile.TryLoad"/>): an unreadable file falls back to its
    /// <c>.bak</c>, and is moved aside to <c>.corrupt-…</c>. Files without a version (MIR's) are migrated.
    /// </summary>
    /// <returns>The stats, or null when there is no readable file or backup. When the file exists but cannot be opened, stats
    /// marked <see cref="DoNotSave"/>: its backup's, or fresh ones named after the file.</returns>
    public static CampaignStats? Load(string path)
    {
        var result = SaveFile.TryLoad(path, static json => SaveFormat.DeserialiseJson<CampaignStats>(json, SaveKind.CampaignStats),
            out var stats, "campaign progress");

        if (result == SaveLoadResult.Inaccessible)
        {
            stats ??= new CampaignStats(Path.GetFileNameWithoutExtension(path));
            stats.DoNotSave = true;
        }

        return stats;
    }

    /// <summary>
    /// The campaign stats files in <paramref name="directory"/> (and below): every <c>*.json</c>, plus the file a lone
    /// <c>*.json.bak</c> belongs to, so <see cref="Load"/> can recover it. Temporary and quarantined files are skipped.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string directory)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                files.Add(file);
            else if (file.EndsWith(".json" + SaveFile.BackupSuffix, StringComparison.OrdinalIgnoreCase))
                files.Add(file[..^SaveFile.BackupSuffix.Length]);
        }
        return files;
    }

    /// <summary>
    /// Save the changes to the file at <see cref="GetFilePath"/>, atomically, keeping the previous file as <c>.bak</c>.
    /// Never throws; a failure (a read-only folder, for example) is logged. Does nothing when <see cref="DoNotSave"/> is set.
    /// </summary>
    public void Save()
    {
        if (DoNotSave)
            return; // the file could not be opened at load; that was logged then

        FormatVersion = SaveFormat.CampaignStatsVersion;
        var json = JsonConvert.SerializeObject(this);
        SaveFile.TryWriteAllText(GetFilePath(), json, "campaign progress for " + CampaignId);
    }
}
