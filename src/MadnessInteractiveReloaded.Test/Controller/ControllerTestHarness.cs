using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MIR.Test.GameTests;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.Onion;

namespace MIR.Test.Controller;

/// <summary>
/// A test game with a fake pad, a recording cursor warper and a <see cref="ControllerLoopEvent"/>, plus helpers that model
/// what the engine does to <see cref="InputState"/> between frames. Create one per test and dispose it at the end.
/// </summary>
internal sealed class ControllerTestHarness : IDisposable
{
    public const float Dt = 1 / 60f;
    public const ushort ButtonA = 0x1000, ButtonB = 0x2000, ButtonStart = 0x0010;
    public const ushort ButtonDPadUp = 0x0001, ButtonDPadDown = 0x0002, ButtonDPadLeft = 0x0004, ButtonDPadRight = 0x0008;
    public const ushort ButtonLeftBumper = 0x0100, ButtonBack = 0x0020;
    public const byte TriggerFull = 255;
    public static readonly Vector2 ScreenCentre = new(960, 540);

    public readonly FakeGamepadSource Pad = new();
    public readonly RecordingCursorWarper Warper = new();
    public readonly ControllerLoopEvent Loop;
    public readonly Game Game;
    public readonly FakeWindow Window;

    /// <param name="menuTargets">What menu navigation moves between. Null uses the real Onion tree, the same as the game.</param>
    public ControllerTestHarness(IMenuTargets? menuTargets = null)
    {
        BaseAssetsFixture.EnsureRegistered();
        Registries.ClearAll();

        ResetStatics();

        Game = TestGameUtil.Create(NullLogger.Instance);
        ResetOnion();
        Window = (FakeWindow)Game.Window;
        Window.Size = new Vector2(1920, 1080);
        Game.State.Input = new InputState();
        Warper.OsMouse = ScreenCentre;
        Game.State.Input.WindowMousePosition = ScreenCentre;
        Game.State.Input.WorldMousePosition = ScreenCentre;

        Loop = new ControllerLoopEvent(Warper, menuTargets);
    }

    public void Dispose()
    {
        Window.OnLoopCycle = null;
        Game.AdditionalLoopEvents.Clear();
        ResetOnion();
        Game.Stop();
        Registries.ClearAll();
        ResetStatics();
    }

    /// <summary>
    /// Puts every static the controller code reads back to its default, with <see cref="Pad"/> as the source.
    /// </summary>
    public void ResetStatics()
    {
        Gamepad.ResetForTests(Pad);
        ControlScheme.ActiveControlScheme = new ControlScheme();
        ControllerLoopEvent.AimDistance = 500;
        ControllerLoopEvent.CursorSpeed = 1;
        MenuNavigator.ResetSuspensionForTests();
    }

    /// <summary>
    /// Forgets the controls, focus and open dropdown another test's menu left in the Onion tree. A test drawing the same
    /// controls gets the same ids, so a triggered control left over would show up open. Onion's static constructor needs
    /// a game, so this runs once <see cref="Game"/> exists.
    /// </summary>
    private static void ResetOnion()
    {
        Onion.ClearCache();
        Onion.Navigator.FocusedControl = null;
        Onion.Navigator.TriggeredControl = null;
        Onion.Navigator.ActiveControl = null;
    }

    public ref InputState Input => ref Game.State.Input;

    public void SetPad(ushort buttons = 0, Vector2 left = default, Vector2 right = default, byte rightTrigger = 0, byte leftTrigger = 0)
    {
        Pad.State.Buttons = buttons;
        Pad.State.ThumbLX = (short)(left.X * 32767);
        Pad.State.ThumbLY = (short)(left.Y * 32767);
        Pad.State.ThumbRX = (short)(right.X * 32767);
        Pad.State.ThumbRY = (short)(right.Y * 32767);
        Pad.State.RightTrigger = rightTrigger;
        Pad.State.LeftTrigger = leftTrigger;
    }

    /// <summary>
    /// Runs one controller frame on fresh engine input (see <see cref="EngineReset"/>). Assertions after it see this frame's input.
    /// The first call is a connect frame (the pad goes from disconnected to connected).
    /// </summary>
    /// <param name="dt">The frame time, <see cref="Dt"/> unless a test needs another frame rate.</param>
    public void Frame(ushort buttons = 0, Vector2 left = default, Vector2 right = default, float dt = Dt, byte leftTrigger = 0, byte rightTrigger = 0)
    {
        EngineReset();
        SetPad(buttons, left, right, rightTrigger, leftTrigger);
        Loop.Update(Game, dt);
    }

    /// <summary>
    /// Steps the whole game (loop events and scene systems) with <see cref="TestGameUtil.StepGame"/> for <paramref name="span"/>,
    /// with <see cref="EngineReset"/> before the first frame and after every frame, the way the engine's <c>LoopCycle</c> gives
    /// each frame fresh input. Without it the aim override would survive into the next frame and read as mouse movement.
    /// Assertions after it see reset input, not the last frame's.
    /// </summary>
    /// <param name="resetFirst">False keeps the current input for the first frame, for a test that set up a key or mouse press.</param>
    /// <param name="render">Also runs the scene's render systems every frame. Onion only works out where its controls can be
    /// hovered while it renders, so a test that points the cursor at a real control needs this.</param>
    public void Step(TimeSpan span, bool resetFirst = true, bool render = false)
    {
        if (resetFirst)
            EngineReset();
        Window.OnLoopCycle = render ? RenderThenReset : EngineReset;
        try
        {
            TestGameUtil.StepGame(span, Game);
        }
        finally
        {
            Window.OnLoopCycle = null;
        }
    }

    private void RenderThenReset()
    {
        Game.Scene?.RenderSystems();
        EngineReset();
    }

    /// <summary>
    /// Models the end of the engine's <c>LoopCycle</c>: clears the per-frame sets and scalars and puts the mouse back where
    /// the OS cursor is. Overrides from the previous frame (aim point, virtual cursor) don't survive, the same as with the
    /// real per-frame copy of <see cref="InputState"/>. Only a warp or a test setting <see cref="RecordingCursorWarper.OsMouse"/>
    /// moves the OS cursor.
    /// </summary>
    public void EngineReset()
    {
        ref var input = ref Input;
        input.KeysDown!.Clear();
        input.KeysUp!.Clear();
        input.MouseButtonsDown!.Clear();
        input.MouseButtonsUp!.Clear();
        input.AnyKey = false;
        input.AnyMouseButton = false;
        input.MouseScrollDelta = 0;
        input.WindowMouseDelta = Vector2.Zero;
        input.WindowMousePosition = Warper.OsMouse;
        input.WorldMousePosition = Window.WindowToWorldPoint(Warper.OsMouse);
    }

    /// <summary>
    /// Builds an Experiment-mode <c>dbg_room</c> scene with a player, adds <see cref="Loop"/> to the game and steps one frame
    /// with the pad connected and idle. Dispose the returned scene when the test ends.
    /// </summary>
    public Scene PrepareGameplayScene()
    {
        Registries.LoadFactions(); // the player prefab needs the "player" faction
        var scene = Game.Scene = SceneUtils.PrepareGameScene(Game, GameMode.Experiment, true, Registries.Levels.Get("dbg_room").Level.Value);
        Game.AdditionalLoopEvents.Add(Loop);
        EngineReset();
        SetPad();
        TestGameUtil.StepGame(TimeSpan.FromSeconds(Dt), Game);

        MadnessUtils.EditingInExperimentMode(scene).Should().BeFalse();
        ControllerLoopEvent.IsGameplay(scene).Should().BeTrue();
        return scene;
    }

    /// <summary>
    /// Builds a menu scene the way the main, campaign and arena menus and character customisation do: a
    /// <see cref="SceneUtils.PrepareMenuScene"/> scene with a living player for display that ignores user input. Adds
    /// <see cref="Loop"/> to the game and steps one frame with the pad connected and idle. Dispose the returned scene when the test ends.
    /// </summary>
    public Scene PrepareMenuSceneWithDisplayPlayer()
    {
        Registries.LoadFactions(); // the player prefab needs the "player" faction
        var scene = Game.Scene = SceneUtils.PrepareMenuScene(Game, "controller_test_menu");
        var player = Prefabs.CreatePlayer(scene, default);
        player.RespondToUserInput = false; // the menu systems set this every frame
        Game.AdditionalLoopEvents.Add(Loop);
        EngineReset();
        SetPad();
        TestGameUtil.StepGame(TimeSpan.FromSeconds(Dt), Game);

        MadnessUtils.FindPlayer(scene, out _, out var character).Should().BeTrue();
        character!.IsAlive.Should().BeTrue();
        return scene;
    }
}

/// <summary>
/// Records warps and models the OS cursor, which lands on the warp target truncated to whole pixels: the engine's
/// <c>OpenTKWindow.WindowToScreenPoint</c> casts X and Y to <c>int</c> before <c>SetCursorPos</c> sees them.
/// </summary>
internal sealed class RecordingCursorWarper : ICursorWarper
{
    public readonly List<Vector2> Positions = new();

    /// <summary>
    /// Where the OS cursor is, in window coordinates. Set it to simulate the user moving the mouse.
    /// </summary>
    public Vector2 OsMouse;

    /// <summary>
    /// When set, the OS keeps a warped cursor at or above-left of this point, the way the engine's <c>ConfineCursor</c>
    /// padding does near the right and bottom edges in fullscreen and borderless modes.
    /// </summary>
    public Vector2? ConfineMax;

    public void Warp(Window window, Vector2 windowPosition)
    {
        Positions.Add(windowPosition);
        var landed = new Vector2((int)windowPosition.X, (int)windowPosition.Y);
        OsMouse = ConfineMax is Vector2 max ? Vector2.Min(landed, max) : landed;
    }
}
