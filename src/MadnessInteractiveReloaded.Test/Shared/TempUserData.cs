using System;
using System.Collections.Generic;
using System.IO;

namespace MIR.Test.Shared;

/// <summary>
/// A throwaway userdata folder under <see cref="Path.GetTempPath"/>. Points <see cref="UserData.Paths.BaseDir"/> at it and
/// puts back the previous folder, settings instance and <see cref="UserData.Loaded"/> flags on dispose, then deletes it.
/// Tests that use it never see the real save folder. Use it only from the "Registry collection", which runs serially,
/// because the paths and flags are global.
/// </summary>
internal sealed class TempUserData : IDisposable
{
    public readonly string Root;

    private readonly string previousBaseDir;
    private readonly Settings previousSettings;
    private readonly HashSet<string> previousDisks;
    private readonly bool previousSettingsLoaded;
    private readonly bool previousPlayerLookLoaded;
    private readonly bool previousDisksLoaded;

    public TempUserData()
    {
        Root = Path.Combine(Path.GetTempPath(), "iir-save-tests-" + Guid.NewGuid().ToString("N"));
        if (!Path.GetFullPath(Root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test userdata must live under the temp folder");

        previousBaseDir = UserData.Paths.BaseDir;
        previousSettings = UserData.Instances.Settings;
        previousDisks = UserData.Instances.UnlockedImprobabilityDisks;
        previousSettingsLoaded = UserData.Loaded.Settings;
        previousPlayerLookLoaded = UserData.Loaded.PlayerLook;
        previousDisksLoaded = UserData.Loaded.ImprobabilityDisks;

        Directory.CreateDirectory(Root);
        UserData.Paths.BaseDir = Root + Path.DirectorySeparatorChar;
        UserData.Instances.Settings = new Settings();
        UserData.Instances.UnlockedImprobabilityDisks = [];
        UserData.Loaded.Settings = false;
        UserData.Loaded.PlayerLook = false;
        UserData.Loaded.ImprobabilityDisks = false;

        Directory.CreateDirectory(UserData.Paths.CampaignStatsDir);
        Directory.CreateDirectory(UserData.Paths.ExperimentCharacterPresets);
    }

    public string PathOf(string relative) => Path.Combine(Root, relative);

    public void Dispose()
    {
        UserData.Paths.BaseDir = previousBaseDir;
        UserData.Instances.Settings = previousSettings;
        UserData.Instances.UnlockedImprobabilityDisks = previousDisks;
        UserData.Loaded.Settings = previousSettingsLoaded;
        UserData.Loaded.PlayerLook = previousPlayerLookLoaded;
        UserData.Loaded.ImprobabilityDisks = previousDisksLoaded;

        if (!Directory.Exists(Root))
            return;

        foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(Root, true);
    }
}
