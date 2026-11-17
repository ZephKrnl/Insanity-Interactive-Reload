using MIR.Serialisation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Walgelijk;
using Walgelijk.Localisation;

namespace MIR;

/// <summary>
/// The game configuration.<br></br>
/// Graphics settings, audio settings, language, etcs.
/// </summary>
public class Settings
{
    /// <summary>
    /// The language used when the configured one does not exist.
    /// </summary>
    public const string DefaultLanguage = "en-GB";

    /// <summary>
    /// How long after the last <see cref="Apply()"/> the settings are written, so a slider drag makes one save.
    /// </summary>
    public const double SaveDelaySeconds = 0.5;

    /// <summary>
    /// The format of the file these settings were read from, migrated to <see cref="SaveFormat.SettingsVersion"/> on load.
    /// A file without one (from MIR, or IIR before versions) counts as 0.
    /// </summary>
    public int FormatVersion = SaveFormat.SettingsVersion;

    /// <summary>
    /// General common settings.
    /// </summary>
    public GeneralSettings General = new();

    /// <summary>
    /// Video specific settings.
    /// </summary>
    public VideoSettings Video = new();

    /// <summary>
    /// Audio levels and settings.
    /// </summary>
    public AudioSettings Audio = new();

    /// <summary>
    /// Input mapping
    /// </summary>
    public ControlScheme Controls = new();

    /// <summary>
    /// Controller settings.
    /// </summary>
    public ControllerSettings Controller = new();

    private readonly SemaphoreSlim saveLock = new(1);

    private static readonly DebouncedSave pendingSave = new(SaveDelaySeconds);

    private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    public class GeneralSettings
    {
        public string Language = "en-GB";
        public float Screenshake = 1;
    }

    public class VideoSettings
    {
        public int FramerateCap = 300;
        public bool Vsync = false;
        public Window Window = Window.Windowed;
        public bool StampRagdolls = true;
    }

    public class AudioSettings
    {
        public float MasterVolume = 0.5f;
        public float MusicVolume = 1;
        public float SfxVolume = 1;
        public float UiVolume = 1;
    }

    public class ControllerSettings
    {
        public bool Enabled = true;
        public bool Vibration = true;
        public float AimDistance = 500;
        public float CursorSpeed = 1;
    }

    public class InputSettings
    {
        public float MasterVolume = 0.5f;
        public float MusicVolume = 1;
        public float SfxVolume = 1;
        public float UiVolume = 1;
    }

    public enum Window
    {
        Windowed,
        Fullscreen
    }

    public WindowType GetWindowType(Window w)
    {
        return w switch
        {
            Window.Windowed => WindowType.Normal,
            Window.Fullscreen => WindowType.BorderlessFullscreen,
            _ => WindowType.Normal,
        };
    }

    /// <summary>
    /// Apply the settings, and save them to disk <see cref="SaveDelaySeconds"/> after the last call.
    /// </summary>
    public void Apply() => Apply(Game.Main);

    /// <summary>
    /// Apply the settings, and save them to disk <see cref="SaveDelaySeconds"/> after the last call.
    /// </summary>
    /// <param name="game"></param>
    public void Apply(Game game)
    {
        game.Window.VSync = Video.Vsync;
        game.UpdateRate = 0; // the engine limiter busy-waits, FrameLimiterLoopEvent sleeps instead
        FrameLimiterLoopEvent.Cap = Video.FramerateCap;
        if (game.Window.WindowType != GetWindowType(Video.Window))
            game.Window.WindowType = GetWindowType(Video.Window);

        game.AudioRenderer.Volume = Audio.MasterVolume;
        AudioTracks.Music.Volume = Audio.MusicVolume;
        AudioTracks.SoundEffects.Volume = Audio.SfxVolume;
        AudioTracks.UserInterface.Volume = Audio.UiVolume;
        ControlScheme.ActiveControlScheme = Controls ?? new();
        Gamepad.Enabled = Controller.Enabled;
        Gamepad.VibrationEnabled = Controller.Vibration;
        ControllerLoopEvent.AimDistance = Controller.AimDistance;
        ControllerLoopEvent.CursorSpeed = Controller.CursorSpeed;
        //game.Console.ToggleKey = ControlScheme.ActiveControlScheme.

        var language = PickLanguage(General.Language, static k => Registries.Languages.Has(k));
        if (language != General.Language)
            Logger.Warn($"Language read from settings \"{General.Language}\" is invalid, so {(language == null ? "the current language is kept" : $"\"{language}\" is used")}. Choices are {string.Join(", ", Registries.Languages.GetAllKeys())}");
        if (language != null && Registries.Languages.TryGet(language, out var found))
        {
            Localisation.CurrentLanguage = found;
            General.Language = language;
        }

        RequestSave(Now());
    }

    /// <summary>
    /// The language to use for <paramref name="requested"/>: itself when it exists, else <see cref="DefaultLanguage"/>, else
    /// null, meaning keep the current language.
    /// </summary>
    public static string? PickLanguage(string? requested, Func<string, bool> isAvailable)
    {
        if (!string.IsNullOrWhiteSpace(requested) && isAvailable(requested))
            return requested;
        return isAvailable(DefaultLanguage) ? DefaultLanguage : null;
    }

    /// <summary>
    /// Ask for a save <see cref="SaveDelaySeconds"/> after <paramref name="now"/> (seconds, any monotonic clock). Every
    /// request pushes the save back, so a burst of changes makes one save.
    /// </summary>
    public static void RequestSave(double now) => pendingSave.Request(now);

    /// <summary>
    /// True while a requested save has not been made.
    /// </summary>
    public static bool SavePending => pendingSave.IsPending;

    /// <summary>
    /// Saves <see cref="UserData.Instances.Settings"/> when a requested save is due. Called on the main thread every frame
    /// by <see cref="SettingsSaveLoopEvent"/>, so the settings are never serialised while the UI changes them.
    /// </summary>
    /// <returns>True when it saved.</returns>
    public static bool SaveIfDue(double now)
    {
        if (!pendingSave.TakeIfDue(now))
            return false;
        UserData.Instances.Settings.Save();
        return true;
    }

    /// <summary>
    /// <see cref="SaveIfDue(double)"/> with the current time.
    /// </summary>
    public static bool SaveIfDue() => SaveIfDue(Now());

    /// <summary>
    /// Drops a requested save without making it.
    /// </summary>
    public static void CancelPendingSave() => pendingSave.Cancel();

    /// <summary>
    /// Save <see cref="UserData.Instances.Settings"/> to disk now, which also makes any requested save. Never throws.
    /// </summary>
    public void Save()
    {
        saveLock.Wait();
        try
        {
            pendingSave.Cancel();
            WriteToFile(UserData.Paths.Settings, UserData.Instances.Settings);
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    /// Try to load the settings from disk. Falls back to <c>settings.json.bak</c> when the file is unreadable, and moves an
    /// unreadable file aside (see <see cref="SaveFile"/>). Files without a version (MIR's) load as version 0 and are migrated.
    /// </summary>
    /// <returns>True when settings were read, from the file or its backup. <paramref name="s"/> is left as it is otherwise.</returns>
    public static bool TryReadFromFile(string path, ref Settings s)
    {
        var before = s;
        ReadFromFile(path, ref s);
        return !ReferenceEquals(before, s);
    }

    /// <summary>
    /// <see cref="TryReadFromFile"/>, returning what was found. On <see cref="SaveLoadResult.Inaccessible"/> the file could
    /// not be opened and will not be written this session; <paramref name="s"/> then holds its backup's settings, if any.
    /// </summary>
    public static SaveLoadResult ReadFromFile(string path, ref Settings s)
    {
        var result = SaveFile.TryLoad(path, Parse, out var read, "settings");
        if (read != null)
            s = read;
        return result;
    }

    private static Settings Parse(string json)
    {
        var read = SaveFormat.DeserialiseJson<Settings>(json, SaveKind.Settings);
        read.Controls ??= new();
        foreach (var k in read.Controls.InputMap.Keys)
        {
            var old = read.Controls.InputMap[k];
            read.Controls.InputMap[k] = new UserInput(old.Inputs, old.Type);
        }
        return read;
    }

    /// <summary>
    /// Save the settings to disk, atomically, keeping the previous file as <c>.bak</c>. Never throws; failures are logged.
    /// </summary>
    public static void WriteToFile(string path, in Settings s)
    {
        try
        {
            var json = JsonConvert.SerializeObject(s, Formatting.Indented);
            SaveFile.TryWriteAllText(path, json, "settings");
        }
        catch (Exception e)
        {
            Logger.Error($"Could not save settings: {e}");
        }
    }
}
