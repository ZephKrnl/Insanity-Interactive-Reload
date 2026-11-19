using MIR.Cutscenes;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.AssetManager.Deserialisers;
using Walgelijk.CommonAssetDeserialisers;
using Walgelijk.CommonAssetDeserialisers.Audio;
using Walgelijk.CommonAssetDeserialisers.Audio.Qoa;
using Walgelijk.Localisation;
using Walgelijk.Onion;
using Walgelijk.Onion.Animations;
using Walgelijk.OpenTK;
using Walgelijk.OpenTK.MotionTK;
using Walgelijk.PortAudio;
using Walgelijk.SimpleDrawing;

namespace MIR;

public class MadnessInteractiveReloaded
{
    public static Game? Game { get; private set; }

    public static void PrepareResourceInitialise()
    {
        // common stuff
        AssetDeserialisers.Register(new OggFixedAudioDeserialiser());
        AssetDeserialisers.Register(new OggStreamAudioDeserialiser());
        AssetDeserialisers.Register(new QoaFixedAudioDeserialiser());
        AssetDeserialisers.Register(new WaveFixedAudioDeserialiser());
        AssetDeserialisers.Register(new FontDeserialiser());

        // game stuff
        AssetDeserialisers.Register(new LevelDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new AnimationDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new CharacterLookDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new CharacterStatsDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new ArenaModeWave.AssetDeserialiser());
        AssetDeserialisers.Register(new Faction.FactionDeserialiser());
        AssetDeserialisers.Register(new ConVarsDeserialiser());
        AssetDeserialisers.Register(new ByteArrayDeserialiser());
        AssetDeserialisers.Register(new CutsceneDeserialiser());
        AssetDeserialisers.Register(new ArmourDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new HandArmourDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new MeleeSequenceDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new CharacterPresetDeserialiser.AssetDeserialiser());
        AssetDeserialisers.Register(new Campaign.AssetDeserialiser());
        AssetDeserialisers.Register(new WeaponDeserialiser.AssetDeserialiser());

        // "temporary" migration bridge
        AssetDeserialisers.Register(new DelegateDeserialiserBridge<Language>(Language.Load, "json"));
        AssetDeserialisers.Register(new DelegateDeserialiserBridge<Video>(p => new Video(p), "mp4"));

        // load asset packs
        IdUtil.VanillaPackageIds.Add(new PackageId("base"));
        foreach (var a in Directory.EnumerateFiles("resources", "*.waa"))
            try
            {
                Assets.RegisterPackage(a);
            }
            catch (Exception e)
            {
                Logger.Warn(e);
            }

        Assets.Load<FixedAudioData>("sounds/null.wav");

        // set fallbacks

        AssetDeserialisers.SetFallbackForType(typeof(Texture), Textures.Error);
        AssetDeserialisers.SetFallbackForType(typeof(FixedAudioData), Assets.LoadNoCache<FixedAudioData>("sounds/error.wav"));
        AssetDeserialisers.SetFallbackForType(typeof(StreamAudioData), Assets.LoadNoCache<StreamAudioData>("sounds/error.ogg"));

        // all of the following should be obsolete at some point:

        Resources.BasePath = "resources";
        Resources.SetBasePathForType<IReadableTexture>("textures");
        Resources.SetBasePathForType<Texture>("textures");
        Resources.SetBasePathForType<AudioData>("sounds");
        Resources.SetBasePathForType<FixedAudioData>("sounds");
        Resources.SetBasePathForType<StreamAudioData>("sounds");
        Resources.SetBasePathForType<Font>("fonts");
        //Resources.SetBasePathForType<CharacterAnimation>("data/animations");
        //Resources.SetBasePathForType<CharacterStats>("data/stats");
        //Resources.SetBasePathForType<CharacterLook>("data/looks");
        //Resources.SetBasePathForType<Level>("data/levels");
        //Resources.SetBasePathForType<Language>("locale");
        //Resources.SetBasePathForType<MeleeSequence>("data/melee_sequences");
        //Resources.SetBasePathForType<Cutscene>("data/cutscenes");
        //Resources.SetBasePathForType<Video>("video");
        //Resources.SetBasePathForType<Campaign>("data/campaigns");

        Resources.RegisterType(typeof(Language), Language.Load);
        Resources.RegisterType(typeof(CharacterAnimation), static s =>
        {
            using var f = new FileStream(s, FileMode.Open, FileAccess.Read);
            return AnimationDeserialiser.Load(f, Path.GetFileNameWithoutExtension(s), Path.GetDirectoryName(s) ?? string.Empty);
        });
        Resources.RegisterType(typeof(CharacterStats), CharacterStatsDeserialiser.Load);
        Resources.RegisterType(typeof(ArmourPiece), ArmourDeserialiser.LoadFromPath);
        Resources.RegisterType(typeof(CharacterLook), CharacterLookDeserialiser.Load);
        Resources.RegisterType(typeof(Level), static s =>
        {
            using var f = new FileStream(s, FileMode.Open, FileAccess.Read);
            return LevelDeserialiser.Load(f, Path.GetFileNameWithoutExtension(s));
        });
        Resources.RegisterType(typeof(Video), p => new Video(p));
        Resources.RegisterType(typeof(ExperimentCharacterPreset), CharacterPresetDeserialiser.Load);
        Resources.RegisterType(typeof(Campaign), Campaign.LoadFromFile);
    }

    public MadnessInteractiveReloaded()
    {
        // TODO this is fucked up
        BuiltInShaders.TexturedFragment = @"#version 330 core

in vec2 uv;
in vec4 vertexColor;

out vec4 color;

uniform sampler2D mainTex;
uniform vec4 tint = vec4(1,1,1,1);

void main()
{
    color = vertexColor * texture(mainTex, uv) * tint;
}";

        AudioRenderer? audioRenderer = null;

        try
        {
            audioRenderer = new OpenALAudioRenderer();
        }
        catch (Exception e)
        {
            audioRenderer = null;
            Console.Error.WriteLine("Audio renderer failed to initialise: " + e.Message);
        }

        // Before the Game, whose constructor creates %LOCALAPPDATA%\<entry assembly name>: copy the saves, settings and
        // mods from the folder that builds named MIR used.
        bool copiedLegacyData = false;
        var entryName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryName != null && entryName != LegacyDataMigration.LegacyFolderName)
            copiedLegacyData = LegacyDataMigration.CopyLegacyDataIfNeeded(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), entryName);

        Game = new Game(
             new OpenTKWindow(Branding.Name, -Vector2.One, new Vector2(1920, 1080) * 0.8f),
             audioRenderer
        );

        if (copiedLegacyData)
            Logger.Log($"Copied saves, settings and mods from %LOCALAPPDATA%\\{LegacyDataMigration.LegacyFolderName} to {Game.AppDataDirectory}");

        // The sleeping frame limiter replaces the engine busy-wait (UpdateRate stays 0). It goes before the controller event so
        // the pad is polled after the sleep, with the freshest input. The cap is set by Settings.Apply.
        WinTimerResolution.Begin();
        Game.AdditionalLoopEvents.Add(new FrameLimiterLoopEvent());
        // Runs before the scene systems every frame, so pad input is in place when they read it.
        Game.AdditionalLoopEvents.Add(new ControllerLoopEvent(new Win32CursorWarper()));
        // Makes the settings save Settings.Apply asks for, on the main thread, half a second after the last change.
        Game.AdditionalLoopEvents.Add(new SettingsSaveLoopEvent());
        Logger.Log(XInputNative.IsAvailable ? "XInput: available, controller support on" : "XInput: not available, controller support off");

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        Game.Window.SetIcon(TextureLoader.FromFile("resources/textures/icon.png"));
        Game.UpdateRate = 0; // engine limiter off, see FrameLimiterLoopEvent
        Game.FixedUpdateRate = 60;
        Game.Window.VSync = false;
        Game.AudioRenderer.Volume = 0.5f;

        PrepareResourceInitialise();

        Game.Console.UI.BackgroundColour = new(25, 0, 0, 250);
        Game.Console.UI.InputBoxColour = new(15, 0, 0);
        Game.Console.UI.FilterBoxColour = new(35, 0, 0);
        Game.Console.UI.BackgroundIntensity = 3;

        Onion.Configuration.RenderLayer = RenderOrders.UserInterface.Layer;
        Onion.Configuration.ScrollSensitivity = 60;
        Onion.Configuration.AudioTrack = AudioTracks.UserInterface;
        Onion.Animation.DefaultDurationSeconds = 0.05f;
        Onion.Animation.Default.Clear();
        //Onion.Animation.Default.Add(new FlickerAnimation());
        Onion.Animation.Default.Add(new FadeAnimation());

        ref var uiTheme = ref Onion.Theme.Base;
        uiTheme.Background = new(Colors.Transparent);
        uiTheme.Foreground = new(Colors.Black.WithAlpha(.8f));
        uiTheme.Font = Fonts.Oxanium;
        uiTheme.FontSize = 16;
        uiTheme.Padding = 3;
        uiTheme.Text = new(Colors.White, Colors.Red, Colors.Red);
        uiTheme.Accent = new(Colors.Red, Colors.White, Colors.White);
        uiTheme.OutlineColour = new(Colors.Red, Colors.Red, Colors.Red * 0.5f);
        uiTheme.OutlineWidth = new(0, 2, 2);
        uiTheme.Rounding = 0;
        uiTheme.FocusBoxColour = Colors.Red;
        uiTheme.ScrollbarWidth = 16;
        uiTheme.ScrollbarTracker = new(Colors.Red.Brightness(0.8f), Colors.White, Colors.Red.Brightness(0.5f));
        uiTheme.ScrollbarBackground = Colors.Red.Brightness(0.2f);

        TextureLoader.Settings.FilterMode = FilterMode.Linear;
        TextureLoader.Settings.WrapMode = WrapMode.Clamp;

        //TODO hoe kan dit beter
        Directory.CreateDirectory(UserData.Paths.BaseDir);
        Directory.CreateDirectory(UserData.Paths.ExperimentDir);
        Directory.CreateDirectory(UserData.Paths.ExperimentCharacterPresets);
        Directory.CreateDirectory(UserData.Paths.CampaignStatsDir);

        ModLoader.AddSource(new LocalModCollectionSource(new DirectoryInfo(Path.Combine(Game.Main.AppDataDirectory, "mods/"))));
        ModLoader.AddSource(new LocalModCollectionSource(new DirectoryInfo("./mods")));

#if DEBUG
        Game.Profiling.DrawQuickProfiler = false;
        Game.Console.DrawConsoleNotification = true;
        Game.DevelopmentMode = false;
#else
        Game.Profiling.DrawQuickProfiler = false;
        Game.Console.DrawConsoleNotification = false;
        Game.DevelopmentMode = false;
#endif
        //var loadingScene = Game.Scene = TextTestingScene.Create(Game);
        Game.Scene = GameOpeningScene.Create(Game);

        // (duston): try to load an autoexec first, if no autoexec
        // use cmd line args.
        GameLoadingScene.OnFinishedLoading += () =>
        {
            if (StartupBenchmark != null)
            {
                // Unattended benchmark (--mode benchmark): no autoexec, so every run starts the same. The loading scene sets
                // the main menu right after this event, so the benchmark scene replaces it a moment later.
                var options = StartupBenchmark;
                MadnessUtils.Delay(1, () => StartBenchmark(Game, options));
                return;
            }

            AutoExec.Run();
        };

        Draw.TextMeshGenerator.KerningMultiplier = 0.9f;
        Draw.TextMeshGenerator.LineHeightMultiplier = 0.9f;
        Draw.CacheTextMeshes = -1;

        Game.Compositor.Flags = RenderTargetFlags.HDR;
        Game.Compositor.Enabled = false;

        Logger.Log("GPU: " + GL.GetString(StringName.Renderer));

        try
        {
            Game.Start();
        }
        finally
        {
            // Save first, and here rather than after the try, so progress, the look and settings are also saved when the loop
            // throws (BUG-05), and a throw in the clean-up below cannot skip it. SaveAll never throws, so it cannot hide the
            // loop's exception. arena.json is no longer written (BUG-06).
            SaveAll();

            // Nothing updates the pad once the loop has ended. Stop the motors, also when the loop itself throws.
            Gamepad.StopRumble();
            WinTimerResolution.End();
        }

        // TODO invoke mod unload thing i dont remember

        Game.Scene.Dispose();
        Resources.UnloadAll();
        Assets.ClearRegistry();
    }

    /// <summary>
    /// Set by <c>--mode benchmark</c> before the game starts: run this benchmark as soon as loading finishes, then exit.
    /// </summary>
    public static BenchmarkOptions? StartupBenchmark;

    private static bool startupBenchmarkWritten;

    /// <summary>
    /// The process exit code once the game has stopped: 0, except for <c>--mode benchmark</c> when the benchmark did not write
    /// its CSV (it could not start, the write failed, the game crashed or was closed first): then 1.
    /// </summary>
    public static int ExitCode => StartupBenchmark != null && !startupBenchmarkWritten ? 1 : 0;

    private static void StartBenchmark(Game game, BenchmarkOptions options)
    {
        try
        {
            var scene = game.Scene = BenchmarkScene.Create(game, options);
            scene.GetSystem<BenchmarkSystem>().Finished += b =>
            {
                startupBenchmarkWritten = b.CsvPath != null;
                if (b.CsvPath != null)
                    Console.WriteLine("Benchmark written to " + b.CsvPath);
                else
                    Console.Error.WriteLine("Benchmark finished, but the CSV could not be written (see the log)");
            };
        }
        catch (Exception e)
        {
            Logger.Error($"The benchmark could not start: {e}");
            Console.Error.WriteLine("The benchmark could not start: " + e.Message);
            game.Stop();
        }
    }

    private static int exitSaveDone;

    /// <summary>
    /// Saves everything that is saved at exit (see <see cref="UserData.SaveAll"/>): each save on its own, errors logged, never
    /// throws. Called when the game loop ends, normally or not, and by the crash handler.
    /// <para>
    /// It runs once per exit: after a crash in the loop, the <c>finally</c> has already saved, and saving again from the crash
    /// handler would replace every <c>.bak</c> (the state before this exit) with the state at the crash.
    /// </para>
    /// </summary>
    public static void SaveAll()
    {
        if (Interlocked.Exchange(ref exitSaveDone, 1) == 1)
        {
            Logger.Log("Exit save already done");
            return;
        }

        try
        {
            UserData.SaveAll();
        }
        catch (Exception e)
        {
            Logger.Error($"Saving at exit failed: {e}");
        }
    }

    /// <summary>
    /// Lets the next <see cref="SaveAll"/> run again. Only for tests, which call it more than once per process.
    /// </summary>
    internal static void ResetExitSaveForTests() => Interlocked.Exchange(ref exitSaveDone, 0);
}
