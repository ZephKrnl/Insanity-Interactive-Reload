using System;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using MIR.Test.GameTests;
using Walgelijk;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class ControllerLoopEventTests : IDisposable
{
    private const float Dt = ControllerTestHarness.Dt;
    private const ushort ButtonA = ControllerTestHarness.ButtonA, ButtonB = ControllerTestHarness.ButtonB, ButtonStart = ControllerTestHarness.ButtonStart;

    private readonly ControllerTestHarness harness = new();
    private readonly FakeGamepadSource fake;
    private readonly RecordingCursorWarper warper;
    private readonly ControllerLoopEvent loop;
    private readonly Game game;
    private readonly FakeWindow window;

    public ControllerLoopEventTests()
    {
        fake = harness.Pad;
        warper = harness.Warper;
        loop = harness.Loop;
        game = harness.Game;
        window = harness.Window;
    }

    public void Dispose() => harness.Dispose();

    private ref InputState Input => ref harness.Input;

    private void SetPad(ushort buttons = 0, Vector2 left = default, Vector2 right = default) => harness.SetPad(buttons, left, right);

    /// <inheritdoc cref="ControllerTestHarness.Frame"/>
    private void Frame(ushort buttons = 0, Vector2 left = default, Vector2 right = default, float dt = Dt) => harness.Frame(buttons, left, right, dt);

    /// <inheritdoc cref="ControllerTestHarness.PrepareGameplayScene"/>
    private Scene PrepareGameplayScene() => harness.PrepareGameplayScene();

    [Fact]
    public void Menu_A_press_and_release_inject_click()
    {
        Frame(ButtonA);
        Input.MouseButtonsDown.Should().Contain(MouseButton.Left);
        Input.MouseButtonsHeld.Should().Contain(MouseButton.Left);

        Frame();
        Input.MouseButtonsUp.Should().Contain(MouseButton.Left);
        Input.MouseButtonsHeld.Should().NotContain(MouseButton.Left);
    }

    [Fact]
    public void Pad_release_does_not_release_real_mouse_button()
    {
        Input.MouseButtonsHeld!.Add(MouseButton.Left);

        Frame(ButtonA);
        Frame();

        Input.MouseButtonsHeld.Should().Contain(MouseButton.Left);
    }

    [Fact]
    public void Disconnect_releases_injected_click()
    {
        Frame(ButtonA);
        Input.MouseButtonsHeld.Should().Contain(MouseButton.Left);

        fake.Connected = false;
        Frame(ButtonA);

        Input.MouseButtonsHeld.Should().NotContain(MouseButton.Left);
        Input.MouseButtonsUp.Should().Contain(MouseButton.Left);
    }

    [Fact]
    public void Right_stick_moves_cursor_and_warps()
    {
        Frame();
        var start = loop.VirtualCursor;

        for (int i = 0; i < 10; i++)
            Frame(right: new Vector2(1, 0));

        loop.VirtualCursor.X.Should().BeGreaterThan(start.X);
        warper.Positions.Should().HaveCount(10);
        warper.Positions.Select(p => p.X).Should().BeInAscendingOrder();
        warper.Positions.Select(p => p.X).Distinct().Should().HaveCount(10);
        Input.WindowMousePosition.Should().Be(loop.VirtualCursor);
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad, "the OS echo of each warp is not the user moving the mouse");
    }

    [Fact]
    public void Start_injects_escape()
    {
        Frame();
        Frame(ButtonStart);
        Input.KeysDown.Should().Contain(Key.Escape);
    }

    [Fact]
    public void Menu_B_injects_escape()
    {
        Frame();
        Frame(ButtonB);
        Input.KeysDown.Should().Contain(Key.Escape);
    }

    [Fact]
    public void Default_input_state_does_not_throw()
    {
        // Before the window's first loop cycle the input sets are null.
        game.State.Input = default;
        loop.Update(game, Dt);

        SetPad(ButtonA | ButtonB | ButtonStart | ControllerTestHarness.ButtonDPadRight, left: new Vector2(1, 0), right: new Vector2(1, 0));
        var update = () => loop.Update(game, Dt);
        update.Should().NotThrow();
        SetPad();
        update.Should().NotThrow();
    }

    [Fact]
    public void Unfocused_window_ignores_pad()
    {
        window.Focused = false;

        Frame();
        Frame(ButtonA | ButtonStart, right: new Vector2(1, 0));
        Frame(ButtonA | ButtonStart, right: new Vector2(1, 0));

        warper.Positions.Should().BeEmpty();
        Input.MouseButtonsDown.Should().BeEmpty();
        Input.MouseButtonsHeld.Should().BeEmpty();
        Input.KeysDown.Should().BeEmpty();
    }

    [Fact]
    public void Disabled_controller_does_not_warp()
    {
        Gamepad.Enabled = false;

        Frame();
        for (int i = 0; i < 5; i++)
            Frame(right: new Vector2(1, 0));

        warper.Positions.Should().BeEmpty();
    }

    [Fact]
    public void Mouse_movement_switches_back_to_keyboard()
    {
        Frame();
        Frame(right: new Vector2(1, 0));
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
        var warpTarget = warper.Positions[^1];

        // The OS now reports the cursor at the (truncated) warp target: that is not the user moving the mouse.
        Frame();
        Input.WindowMousePosition.Should().Be(warper.OsMouse);
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);

        warper.OsMouse = warpTarget + new Vector2(50, 0);
        Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
    }

    [Fact]
    public void Mouse_moves_cursor_even_with_stick_deflected()
    {
        var thumb = new Vector2(0.5f, 0); // the thumb stays on the stick, deflected
        Frame();
        for (int i = 0; i < 5; i++)
            Frame(right: thumb);
        loop.VirtualCursor.X.Should().BeGreaterThan(ControllerTestHarness.ScreenCentre.X, "the stick drives the cursor");

        // The user moves the mouse well away from where the stick put the cursor.
        var mouse = new Vector2(400, 300);
        warper.OsMouse = mouse;
        Frame(right: thumb);

        Vector2.Distance(loop.VirtualCursor, mouse).Should().BeLessThan(5, "the cursor stays where the mouse put it");
        Vector2.Distance(warper.OsMouse, mouse).Should().BeLessThan(5, "no warp snaps the OS cursor back");
        Input.WindowMousePosition.Should().Be(loop.VirtualCursor);

        // The stick carries on from where the mouse left the cursor.
        for (int i = 0; i < 5; i++)
            Frame(right: thumb);
        loop.VirtualCursor.X.Should().BeInRange(mouse.X, mouse.X + 30);
        loop.VirtualCursor.Y.Should().BeApproximately(mouse.Y, 0.5f);
    }

    [Fact]
    public void Slow_stick_moves_cursor_in_sub_pixel_steps()
    {
        // About 0.3 px a frame: each warp lands on a whole pixel, so the cursor must keep the remainder between frames.
        var slow = RightStickInput(0.106f, 0);
        Frame();
        for (int i = 0; i < 20; i++)
            Frame(right: slow);

        loop.VirtualCursor.X.Should().BeApproximately(ControllerTestHarness.ScreenCentre.X + 6, 0.5f);
        warper.OsMouse.X.Should().BeApproximately(ControllerTestHarness.ScreenCentre.X + 6, 1);
    }

    /// <summary>
    /// The right stick input that the radial deadzone turns into a stick of <paramref name="magnitude"/>, pointing
    /// <paramref name="degrees"/> anticlockwise from right. Stick Y is up-positive, so negative angles point down the screen.
    /// </summary>
    private static Vector2 RightStickInput(float magnitude, float degrees)
    {
        const float deadzone = 8689;
        float raw = (deadzone + magnitude * (32767 - deadzone)) / 32767;
        float angle = degrees * MathF.PI / 180;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * raw;
    }

    /// <summary>
    /// Holds the right stick in a menu for <paramref name="seconds"/> at <paramref name="fps"/>. Returns how far, in window
    /// pixels, the menu cursor and the OS cursor moved, and how far the stick's speed should have taken them.
    /// </summary>
    private (Vector2 cursor, Vector2 os, Vector2 expected) HoldRightStick(Vector2 input, float fps, float seconds)
    {
        float dt = 1 / fps;
        Frame(dt: dt);
        var start = loop.VirtualCursor;
        var osStart = warper.OsMouse;

        int frames = (int)MathF.Round(fps * seconds);
        for (int i = 0; i < frames; i++)
            Frame(right: input, dt: dt);

        var stick = Gamepad.Current.RightStick;
        var velocity = stick * stick.Length() * ControllerLoopEvent.MenuCursorSpeed * (window.Size.Y / 1080f);
        velocity.Y = -velocity.Y; // window Y points down
        return (loop.VirtualCursor - start, warper.OsMouse - osStart, velocity * frames * dt);
    }

    [Theory]
    [InlineData(-45)] // down and right: both axes reach the same fraction together
    [InlineData(135)] // up and left
    [InlineData(45)]
    [InlineData(-135)]
    public void Slow_diagonal_stick_moves_cursor(float degrees)
    {
        // About 0.7 px a frame on each axis. The engine truncates every warp to whole pixels, so the cursor only gets
        // anywhere if it keeps the sub-pixel remainder on both axes at once.
        var (cursor, os, expected) = HoldRightStick(RightStickInput(0.3f, degrees), fps: 144, seconds: 1);

        expected.Length().Should().BeApproximately(144, 2, "the test drives a 0.3 stick");
        (cursor.X / expected.X).Should().BeInRange(0.8f, 1.05f, "the menu cursor moves along X at the stick's speed");
        (cursor.Y / expected.Y).Should().BeInRange(0.8f, 1.05f, "the menu cursor moves along Y at the stick's speed");
        (os.X / expected.X).Should().BeInRange(0.8f, 1.05f, "the OS cursor follows along X");
        (os.Y / expected.Y).Should().BeInRange(0.8f, 1.05f, "the OS cursor follows along Y");
    }

    [Theory]
    [InlineData(-20)] // down and right
    [InlineData(20)]
    [InlineData(160)]
    [InlineData(-160)]
    public void Shallow_angle_stick_moves_minor_axis(float degrees)
    {
        // Under 1 px a frame on Y and about 2.6 px on X.
        var (cursor, os, expected) = HoldRightStick(RightStickInput(0.5f, degrees), fps: 144, seconds: 1);

        MathF.Abs(expected.Y).Should().BeApproximately(137, 2, "the test drives a 0.5 stick 20 degrees off horizontal");
        (cursor.Y / expected.Y).Should().BeInRange(0.8f, 1.05f, "the menu cursor moves along the minor axis too");
        (os.Y / expected.Y).Should().BeInRange(0.8f, 1.05f, "the OS cursor follows along the minor axis");
        (cursor.X / expected.X).Should().BeInRange(0.8f, 1.05f);
    }

    [Fact]
    public void Mouse_use_while_stick_pins_cursor_skips_warp()
    {
        // The stick holds the cursor against the right edge, so nothing warps and the anchor sees the mouse move.
        Frame();
        for (int i = 0; i < 60; i++)
            Frame(right: new Vector2(1, 0));
        loop.VirtualCursor.X.Should().Be(window.Size.X - 1);
        int warps = warper.Positions.Count;

        var mouse = new Vector2(window.Size.X - 200, 540);
        warper.OsMouse = mouse;
        Frame(right: new Vector2(1, 0));

        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
        warper.Positions.Should().HaveCount(warps, "the frame the mouse is used, the stick doesn't move the cursor");
        loop.VirtualCursor.Should().Be(mouse);
        Input.WindowMousePosition.Should().Be(mouse);
    }

    [Fact]
    public void Slow_mouse_drift_switches_back()
    {
        Frame();
        Frame(ControllerTestHarness.ButtonBack);
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);

        // 1 px a frame: under any per-frame threshold, but it adds up.
        for (int moved = 1; moved <= 10; moved++)
        {
            warper.OsMouse += new Vector2(1, 0);
            Frame();
            if (moved <= ControllerLoopEvent.MouseMoveThreshold)
                Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad, "{0} px from where the pad took over is a bump", moved);
        }

        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse, "10 px of slow movement is the user moving the mouse");
    }

    [Fact]
    public void Clamped_warp_echo_does_not_flip_device()
    {
        // In fullscreen the engine confines the cursor, so a warp at the right edge lands 12 px short of its target.
        float edge = window.Size.X - 1;
        warper.ConfineMax = new Vector2(edge - 12, window.Size.Y - 1);
        warper.OsMouse = new Vector2(edge - 100, 540);
        Frame();

        for (int i = 0; i < 8; i++)
        {
            Frame(right: new Vector2(1, 0));
            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad, "frame {0}: the echo of a clamped warp is not the user moving the mouse", i);
        }
        warper.Positions[^1].X.Should().Be(edge, "the stick drove the cursor into the edge");
        warper.OsMouse.X.Should().Be(edge - 12, "the OS clamped the warp");

        Frame();
        Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);

        // Accepting the clamp doesn't stop real movement from counting.
        warper.OsMouse += new Vector2(-10, 0);
        Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
    }

    [Fact]
    public void Menu_scene_with_display_player_is_menu_mode()
    {
        var scene = harness.PrepareMenuSceneWithDisplayPlayer();
        try
        {
            ControllerLoopEvent.IsGameplay(scene).Should().BeFalse("the main menu's player is only on display");

            Frame();
            Gamepad.GameplayInputAllowed.Should().BeFalse();

            Frame(ButtonA);
            Input.MouseButtonsDown.Should().Contain(MouseButton.Left, "A clicks in menus");
            Gamepad.GameplayInputAllowed.Should().BeFalse("A must not also jump the display player");

            Frame();
            var start = loop.VirtualCursor;
            warper.Positions.Clear();
            Frame(right: new Vector2(1, 0));
            warper.Positions.Should().ContainSingle("the right stick moves the menu cursor");
            loop.VirtualCursor.X.Should().BeGreaterThan(start.X);
            Input.WindowMousePosition.Should().Be(loop.VirtualCursor, "the aim override must not move the UI hover point");

            Frame();
            Frame(ButtonB);
            Input.KeysDown.Should().Contain(Key.Escape, "B goes back in menus");
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <summary>
    /// Draws a menu back button every frame, the way the menu systems do, and counts the frames it reported a press.
    /// </summary>
    private sealed class BackButtonProbe : Walgelijk.System
    {
        public int Presses;

        public override void Update()
        {
            if (MenuUiUtils.BackButton())
                Presses++;
        }
    }

    [Fact]
    public void Pad_B_triggers_menu_back_button()
    {
        // Like the campaign menu: a menu scene with a display player and a back button.
        var scene = harness.PrepareMenuSceneWithDisplayPlayer();
        try
        {
            var probe = new BackButtonProbe();
            scene.AddSystem(probe);
            var oneFrame = TimeSpan.FromSeconds(Dt);

            SetPad();
            harness.Step(oneFrame);
            probe.Presses.Should().Be(0);

            SetPad(ButtonB);
            harness.Step(oneFrame);
            probe.Presses.Should().Be(1, "B goes back in menus");
            harness.Step(TimeSpan.FromSeconds(0.1));
            probe.Presses.Should().Be(1, "holding B goes back once");

            // A confirmation dialog is open: B leaves it to be answered rather than backing out from under it.
            SetPad();
            harness.Step(oneFrame);
            scene.AttachComponent(scene.CreateEntity(), new ConfirmationDialogComponent("Are you sure?", () => { }));
            SetPad(ButtonB);
            harness.Step(oneFrame);
            probe.Presses.Should().Be(1);
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Pad_B_does_not_trigger_back_button_in_gameplay()
    {
        var scene = PrepareGameplayScene();
        try
        {
            var probe = new BackButtonProbe();
            scene.AddSystem(probe);

            SetPad(ButtonB);
            harness.Step(TimeSpan.FromSeconds(Dt));

            probe.Presses.Should().Be(0, "B is melee in gameplay");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Gameplay_aim_follows_right_stick()
    {
        var scene = PrepareGameplayScene();
        MadnessUtils.FindPlayer(scene, out _, out var character).Should().BeTrue();

        Frame(right: new Vector2(0.707f, 0.707f));

        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
        var offset = Input.WorldMousePosition - character!.Positioning.GlobalCenter;
        var direction = Vector2.Normalize(offset);
        direction.X.Should().BeApproximately(0.707f, 0.02f);
        direction.Y.Should().BeApproximately(0.707f, 0.02f);
        offset.Length().Should().BeApproximately(ControllerLoopEvent.AimDistance * character.Positioning.Scale, 0.5f);
        Input.WindowMousePosition.Should().Be(window.WorldToWindowPoint(Input.WorldMousePosition));

        scene.Dispose();
    }

    [Fact]
    public void Gameplay_aim_is_sticky()
    {
        var scene = PrepareGameplayScene();

        Frame(right: new Vector2(-1, 0));
        loop.AimDirection.X.Should().BeApproximately(-1, 0.001f);

        Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad, "the aim override must not read as the user moving the mouse");
        loop.AimDirection.X.Should().BeApproximately(-1, 0.001f);
        loop.AimDirection.Y.Should().BeApproximately(0, 0.001f);

        // A second, different direction: each release keeps the latest one.
        Frame(right: new Vector2(0, 1));
        Frame();
        Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
        loop.AimDirection.X.Should().BeApproximately(0, 0.001f);
        loop.AimDirection.Y.Should().BeApproximately(1, 0.001f);

        scene.Dispose();
    }

    [Fact]
    public void Aim_survives_pause_and_resume()
    {
        var scene = PrepareGameplayScene();
        try
        {
            scene.FindAnyComponent<PauseComponent>(out var pause).Should().BeTrue();

            // Up, because it can't match the horizontal (FlipScaling, 0) fallback whichever way the player faces.
            SetPad(right: new Vector2(0, 1));
            harness.Step(TimeSpan.FromSeconds(0.1));
            loop.AimDirection.Y.Should().BeApproximately(1, 0.001f);

            pause!.Paused = true;
            SetPad();
            harness.Step(TimeSpan.FromSeconds(0.1));
            ControllerLoopEvent.IsGameplay(scene).Should().BeFalse();

            pause.Paused = false;
            harness.Step(TimeSpan.FromSeconds(Dt)); // the stick stays centred

            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
            ControllerLoopEvent.IsGameplay(scene).Should().BeTrue();
            loop.AimDirection.X.Should().BeApproximately(0, 0.05f, "the aim picks up where it was before the pause");
            loop.AimDirection.Y.Should().BeApproximately(1, 0.05f, "the aim picks up where it was before the pause");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Switching_from_mouse_keeps_mouse_aim_direction()
    {
        var scene = PrepareGameplayScene();
        try
        {
            MadnessUtils.FindPlayer(scene, out _, out var character).Should().BeTrue();
            var upLeft = Vector2.Normalize(new Vector2(-1, 1));

            // FakeWindow maps window to world one to one, so this puts the mouse up and to the left of the player.
            warper.OsMouse = character!.Positioning.GlobalCenter + upLeft * 400;
            harness.Step(TimeSpan.FromSeconds(0.1));
            Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);

            // Back has no gameplay binding: it makes the pad active without moving the player or the right stick.
            Frame(ControllerTestHarness.ButtonBack);

            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
            loop.AimDirection.X.Should().BeApproximately(upLeft.X, 0.05f, "the pad starts from where the mouse aimed");
            loop.AimDirection.Y.Should().BeApproximately(upLeft.Y, 0.05f, "the pad starts from where the mouse aimed");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void New_character_does_not_aim_at_world_origin()
    {
        var first = PrepareGameplayScene();
        Scene? second = null;
        try
        {
            var upLeft = Vector2.Normalize(new Vector2(-0.6f, 0.8f));
            SetPad(right: upLeft);
            harness.Step(TimeSpan.FromSeconds(0.1));
            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
            loop.AimDirection.X.Should().BeApproximately(upLeft.X, 0.01f);

            // A level change: the next scene's player spawns far from the world origin. No system has aimed it yet, so its
            // aim target is still (0, 0) when the loop event runs first on that scene's first frame.
            second = game.Scene = SceneUtils.PrepareGameScene(game, GameMode.Experiment, false, Registries.Levels.Get("dbg_room").Level.Value);
            Prefabs.CreatePlayer(second, new Vector2(5000, 0));
            MadnessUtils.FindPlayer(second, out _, out var character).Should().BeTrue();
            character!.AimTargetPosition.Should().Be(Vector2.Zero);
            float facing = character.Positioning.FlipScaling;
            var towardOrigin = Vector2.Normalize(-character.Positioning.GlobalCenter);

            SetPad(); // the right stick is centred
            harness.Step(TimeSpan.FromSeconds(Dt));

            ControllerLoopEvent.IsGameplay(second).Should().BeTrue();
            Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
            loop.AimDirection.X.Should().BeApproximately(MathF.Abs(upLeft.X) * facing, 0.01f, "the pad keeps its direction, turned the way the new character faces");
            loop.AimDirection.Y.Should().BeApproximately(upLeft.Y, 0.01f, "the pad keeps its direction, turned the way the new character faces");
            Vector2.Dot(loop.AimDirection, towardOrigin).Should().BeLessThan(0.5f, "the world origin is not where the player aimed");
        }
        finally
        {
            first.Dispose();
            second?.Dispose();
        }
    }

    [Fact]
    public void Menu_B_does_not_fire_melee()
    {
        var scene = PrepareGameplayScene();
        scene.FindAnyComponent<PauseComponent>(out var pause).Should().BeTrue();

        pause!.Paused = true;
        Frame();
        Frame(ButtonB);

        Input.KeysDown.Should().Contain(Key.Escape);
        Gamepad.GameplayInputAllowed.Should().BeFalse();
        // PauseSystem may unpause on that Escape before the player system runs, so the pad's B must not also melee.
        pause.Paused = false;
        ControlScheme.ActiveControlScheme.Pressed(GameAction.Melee, game.State.Input).Should().BeFalse();

        scene.Dispose();
    }

    [Fact]
    public void Gameplay_B_fires_melee()
    {
        var scene = PrepareGameplayScene();

        Frame(ButtonB);

        Gamepad.GameplayInputAllowed.Should().BeTrue();
        ControlScheme.ActiveControlScheme.Pressed(GameAction.Melee, game.State.Input).Should().BeTrue();
        Input.KeysDown.Should().NotContain(Key.Escape, "B is only Escape in menus");

        scene.Dispose();
    }

    /// <summary>
    /// Holds LB (Ability1) in gameplay, makes the pad unusable with <paramref name="loseThePad"/> and checks that the frame it
    /// goes reports the release, so <c>CharacterAbilitySystem</c> ends the ability (the Auditor slow-mo would stay on otherwise).
    /// </summary>
    private void AssertAbilityReleasedWhen(Action loseThePad)
    {
        var scene = PrepareGameplayScene();
        try
        {
            var scheme = ControlScheme.ActiveControlScheme;
            Frame(ControllerTestHarness.ButtonLeftBumper);
            scheme.Held(GameAction.Ability1, Input).Should().BeTrue();

            loseThePad();
            Frame(ControllerTestHarness.ButtonLeftBumper);
            scheme.Released(GameAction.Ability1, Input).Should().BeTrue("the ability has to end when the pad goes away");
            scheme.Held(GameAction.Ability1, Input).Should().BeFalse();

            Frame(ControllerTestHarness.ButtonLeftBumper);
            scheme.Released(GameAction.Ability1, Input).Should().BeFalse("the release is reported once");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Unplug_while_holding_ability_releases_it() => AssertAbilityReleasedWhen(() => fake.Connected = false);

    [Fact]
    public void Focus_loss_while_holding_ability_releases_it() => AssertAbilityReleasedWhen(() => window.Focused = false);

    [Fact]
    public void Disabling_controller_while_holding_ability_releases_it() => AssertAbilityReleasedWhen(() => Gamepad.Enabled = false);

    [Fact]
    public void Entering_gameplay_releases_injected_click()
    {
        var scene = PrepareGameplayScene();
        scene.FindAnyComponent<PauseComponent>(out var pause).Should().BeTrue();

        pause!.Paused = true;
        Frame(ButtonA);
        Input.MouseButtonsHeld.Should().Contain(MouseButton.Left);

        pause.Paused = false;
        Frame(ButtonA);
        Input.MouseButtonsHeld.Should().NotContain(MouseButton.Left);
        Input.MouseButtonsUp.Should().Contain(MouseButton.Left);

        Frame();
        Input.MouseButtonsUp.Should().BeEmpty("the pad's click was already released when gameplay resumed");

        scene.Dispose();
    }

    [Fact]
    public void A_pressed_in_gameplay_does_not_click_when_released_in_menu()
    {
        var scene = PrepareGameplayScene();
        scene.FindAnyComponent<PauseComponent>(out var pause).Should().BeTrue();

        Frame(ButtonA);
        Input.MouseButtonsDown.Should().BeEmpty();

        pause!.Paused = true;
        Frame(ButtonA);
        Frame();
        Input.MouseButtonsUp.Should().BeEmpty("the pad never sent a click down in the menu");

        scene.Dispose();
    }
}
