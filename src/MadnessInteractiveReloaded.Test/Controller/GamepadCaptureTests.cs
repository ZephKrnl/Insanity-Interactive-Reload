using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Walgelijk;
using Walgelijk.Onion;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class GamepadCaptureTests : IDisposable
{
    private const ushort A = ControllerTestHarness.ButtonA, B = ControllerTestHarness.ButtonB, X = 0x4000;
    private const ushort Back = ControllerTestHarness.ButtonBack, Start = ControllerTestHarness.ButtonStart;

    public GamepadCaptureTests() => ResetStatics();

    public void Dispose() => ResetStatics();

    private static void ResetStatics()
    {
        Gamepad.ResetForTests(new FakeGamepadSource());
        ControlScheme.ActiveControlScheme = new ControlScheme();
        MenuNavigator.ResetSuspensionForTests();
        MIR.Controls.GamepadSwitchButton.ResetForTests();
    }

    private static GamepadState Pad(ushort buttons = 0, Vector2 left = default, Vector2 right = default,
        float leftTrigger = 0, float rightTrigger = 0)
        => new(true, buttons, left, right, leftTrigger, rightTrigger);

    /// <summary>
    /// Begins a capture and lets everything go for a frame, so the next button down binds.
    /// </summary>
    private static GamepadCapture Armed()
    {
        var capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Waiting);
        return capture;
    }

    [Fact]
    public void Capture_ignores_the_button_that_started_it()
    {
        var capture = new GamepadCapture();
        capture.Begin();
        capture.Active.Should().BeTrue();

        capture.Update(Pad(A)).Should().Be(GamepadCaptureResult.Waiting, "A is still held from the press that opened the capture");
        capture.Update(Pad(A)).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Waiting, "a frame with nothing down only arms it");

        capture.Update(Pad(A)).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.A);
        capture.Active.Should().BeFalse();
    }

    [Fact]
    public void Back_cancels()
    {
        var capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad(Back)).Should().Be(GamepadCaptureResult.Waiting, "a Back held when the capture began has to be let go first");
        capture.Active.Should().BeTrue();
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Waiting);

        capture.Update(Pad(Back | X)).Should().Be(GamepadCaptureResult.Cancelled, "Back wins over a button pressed with it");
        capture.Active.Should().BeFalse();
        capture.BoundButton.Should().Be(GamepadButton.None);

        var other = Armed();
        other.Cancel();
        other.Active.Should().BeFalse();
    }

    [Fact]
    public void Back_cancels_while_a_trigger_is_held()
    {
        var capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad(leftTrigger: 1)).Should().Be(GamepadCaptureResult.Waiting, "LT was held before the capture began");
        capture.Update(Pad(leftTrigger: 1)).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad(Back, leftTrigger: 1)).Should().Be(GamepadCaptureResult.Cancelled, "Back cancels whatever else is still held");
        capture.Active.Should().BeFalse();
        capture.BoundButton.Should().Be(GamepadButton.None);

        // A worn stick resting past the direction threshold doesn't lock the capture either.
        capture = new GamepadCapture();
        capture.Begin();
        var drift = new Vector2(0.4f, 0);
        capture.Update(Pad(left: drift)).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad(Back, left: drift)).Should().Be(GamepadCaptureResult.Cancelled);
    }

    [Fact]
    public void Held_button_binds_only_after_release()
    {
        const ushort LeftBumper = ControllerTestHarness.ButtonLeftBumper;

        var capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad(A | LeftBumper)).Should().Be(GamepadCaptureResult.Waiting, "both were held when it began");
        capture.Update(Pad(A)).Should().Be(GamepadCaptureResult.Waiting, "letting LB go only arms LB");
        capture.Update(Pad(A | LeftBumper)).Should().Be(GamepadCaptureResult.Bound, "LB was seen up, so pressing it again binds, with A still held");
        capture.BoundButton.Should().Be(GamepadButton.LeftBumper);

        // A button that was up from the start binds while another is still held.
        capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad(rightTrigger: 1)).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad(X, rightTrigger: 1)).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.X);

        // One held through the whole capture never binds itself.
        capture = new GamepadCapture();
        capture.Begin();
        for (int i = 0; i < 5; i++)
            capture.Update(Pad(rightTrigger: 1)).Should().Be(GamepadCaptureResult.Waiting);
    }

    [Fact]
    public void Disconnect_cancels_capture()
    {
        var capture = Armed();
        capture.Update(GamepadState.Disconnected).Should().Be(GamepadCaptureResult.Cancelled, "the pad went away mid-capture");
        capture.Active.Should().BeFalse();
        capture.BoundButton.Should().Be(GamepadButton.None);
        capture.Update(Pad(X)).Should().Be(GamepadCaptureResult.Cancelled, "it stays cancelled");

        // Begun with no pad at all, it cancels on its first frame rather than wait for a button that can't come.
        capture = new GamepadCapture();
        capture.Begin();
        capture.Update(GamepadState.Disconnected).Should().Be(GamepadCaptureResult.Cancelled);
    }

    [Fact]
    public void Start_is_not_bindable()
    {
        GamepadCapture.IsBindable(GamepadButton.None).Should().BeFalse();
        GamepadCapture.IsBindable(GamepadButton.Start).Should().BeFalse();
        GamepadCapture.IsBindable(GamepadButton.Back).Should().BeFalse();
        foreach (var b in Enum.GetValues<GamepadButton>().Except([GamepadButton.None, GamepadButton.Start, GamepadButton.Back]))
            GamepadCapture.IsBindable(b).Should().BeTrue("{0} can be bound", b);

        var capture = Armed();
        capture.Update(Pad(Start)).Should().Be(GamepadCaptureResult.Waiting, "Start stays the pause button");
        capture.Update(Pad(Start | X)).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.X);
    }

    [Fact]
    public void Trigger_and_stick_directions_bind()
    {
        var capture = Armed();
        capture.Update(Pad(rightTrigger: 1)).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.RightTrigger);

        capture = Armed();
        capture.Update(Pad(leftTrigger: 0.5f)).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.LeftTrigger);

        capture = Armed();
        capture.Update(Pad(right: new Vector2(0, 1))).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.RightStickUp);

        capture = Armed();
        capture.Update(Pad(left: new Vector2(0.2f, 0))).Should().Be(GamepadCaptureResult.Waiting, "a push short of a direction isn't one");
        capture.Update(Pad(left: new Vector2(-1, 0))).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.LeftStickLeft);

        // A stick pushed when the capture began has to come back to the middle first, like a button.
        capture = new GamepadCapture();
        capture.Begin();
        capture.Update(Pad(left: new Vector2(0, -1))).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Waiting);
        capture.Update(Pad(left: new Vector2(0, -1))).Should().Be(GamepadCaptureResult.Bound);
        capture.BoundButton.Should().Be(GamepadButton.LeftStickDown);
    }

    [Fact]
    public void Capture_suspends_and_restores_menu_input()
    {
        MenuNavigator.BeginFrame();
        MenuNavigator.InputSuspended.Should().BeFalse();

        var capture = new GamepadCapture();
        capture.Begin();
        MenuNavigator.InputSuspended.Should().BeTrue("from the frame it began in");

        for (int i = 0; i < 3; i++)
        {
            MenuNavigator.BeginFrame();
            MenuNavigator.InputSuspended.Should().BeTrue("frame {0} of the capture", i);
            capture.Update(Pad(i == 0 ? A : (ushort)0)).Should().Be(GamepadCaptureResult.Waiting);
            MenuNavigator.InputSuspended.Should().BeTrue();
        }

        MenuNavigator.BeginFrame();
        capture.Update(Pad(B)).Should().Be(GamepadCaptureResult.Bound);
        MenuNavigator.InputSuspended.Should().BeTrue("it holds for the whole frame it binds in");

        MenuNavigator.BeginFrame();
        MenuNavigator.InputSuspended.Should().BeFalse("a frame after it bound, the menus have the pad back");

        // Cancelling stops the lease the same way.
        capture.Begin();
        MenuNavigator.BeginFrame();
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Waiting);
        MenuNavigator.BeginFrame();
        capture.Update(Pad(Back)).Should().Be(GamepadCaptureResult.Cancelled);
        MenuNavigator.InputSuspended.Should().BeTrue();
        MenuNavigator.BeginFrame();
        MenuNavigator.InputSuspended.Should().BeFalse();
        capture.Update(Pad()).Should().Be(GamepadCaptureResult.Cancelled, "an inactive capture doesn't renew");
        MenuNavigator.InputSuspended.Should().BeFalse();
    }

    [Fact]
    public void Reset_restores_defaults_in_place()
    {
        var scheme = new ControlScheme();
        var map = scheme.GamepadMap;
        map[GameAction.Attack] = GamepadButton.LeftBumper;
        map[GameAction.Ability1] = GamepadButton.RightTrigger;
        map.Remove(GameAction.Interact);

        scheme.ResetGamepadMap();

        scheme.GamepadMap.Should().BeSameAs(map, "ActiveControlScheme and the settings share it");
        var defaults = new ControlScheme().GamepadMap;
        map.Should().BeEquivalentTo(defaults);
        map.Keys.Should().Equal(defaults.Keys, "the settings rows keep their order");
    }

    [Fact]
    public void Rebound_map_round_trips_through_settings_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mir-gamepad-capture-test-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new Settings();
            settings.Controls.GamepadMap[GameAction.Attack] = GamepadButton.LeftBumper;
            settings.Controls.GamepadMap[GameAction.Ability1] = GamepadButton.RightTrigger;
            settings.Controls.GamepadMap[GameAction.Left] = GamepadButton.DPadLeft;
            settings.Controls.GamepadMap[GameAction.Throw] = GamepadButton.RightStickDown;
            Settings.WriteToFile(path, settings);

            var read = new Settings();
            Settings.TryReadFromFile(path, ref read).Should().BeTrue();

            read.Should().NotBeSameAs(settings);
            read.Controls.GamepadMap.Should().BeEquivalentTo(settings.Controls.GamepadMap);
            read.Controls.GamepadMap[GameAction.Throw].Should().Be(GamepadButton.RightStickDown);

            read.Controls.ResetGamepadMap();
            read.Controls.GamepadMap.Should().BeEquivalentTo(new ControlScheme().GamepadMap);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The rebind control on the real Onion tree, driven by the pad through ControllerLoopEvent.

    private static readonly TimeSpan OneFrame = TimeSpan.FromSeconds(ControllerTestHarness.Dt);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Draws one pad rebind button, then a menu back button, the way the settings' Input tab does.
    /// </summary>
    private sealed class RebindProbe : Walgelijk.System
    {
        public GamepadButton Binding = GamepadButton.Y;
        public bool Visible = true;
        public int Changes, BackPresses;

        public override void Update()
        {
            if (!Visible)
                return;

            Ui.Layout.Size(300, 40).Move(810, 520);
            if (MIR.Controls.GamepadSwitchButton.Start(ref Binding))
                Changes++;

            if (MenuUiUtils.BackButton())
                BackPresses++;
        }
    }

    private static Scene RebindScene(ControllerTestHarness h, RebindProbe probe)
    {
        var scene = h.Game.Scene = new Scene(h.Game, "gamepad_capture_onion_test");
        scene.AddSystem(new OnionSystem());
        scene.AddSystem(probe);
        h.Game.AdditionalLoopEvents.Add(h.Loop);
        h.Warper.OsMouse = new Vector2(960, 540); // over the rebind button
        h.SetPad();
        h.Step(Settle, render: true);
        return scene;
    }

    private static void Hold(ControllerTestHarness h, ushort buttons, TimeSpan span)
    {
        h.SetPad(buttons);
        h.Step(span, render: true);
    }

    private static void Tap(ControllerTestHarness h, ushort buttons)
    {
        Hold(h, buttons, OneFrame);
        Hold(h, 0, Settle);
    }

    /// <summary>
    /// Focuses the rebind button with the D-pad and clicks it with A.
    /// </summary>
    private static void OpenCapture(ControllerTestHarness h)
    {
        if (Onion.Navigator.FocusedControl == null)
            Tap(h, ControllerTestHarness.ButtonDPadUp);
        MenuNavigator.HighlightRect.Should().NotBeNull("the D-pad focused the rebind button");

        Hold(h, A, Settle); // the press that opens the capture, held for a while
        MenuNavigator.InputSuspended.Should().BeFalse("the click lands when A is let go");
        Hold(h, 0, Settle);
        MenuNavigator.InputSuspended.Should().BeTrue("clicking the button started a capture");
    }

    [Fact]
    public void A_on_a_rebind_button_binds_the_next_pad_button()
    {
        using var h = new ControllerTestHarness();
        var probe = new RebindProbe();
        var scene = RebindScene(h, probe);
        try
        {
            OpenCapture(h);
            probe.Binding.Should().Be(GamepadButton.Y, "the A that opened the capture didn't bind itself");

            Hold(h, X, Settle);
            probe.Binding.Should().Be(GamepadButton.X);
            probe.Changes.Should().Be(1);
            MenuNavigator.InputSuspended.Should().BeFalse("the menus get the pad back once it bound");
            Onion.Navigator.TriggeredControl.Should().BeNull();

            // A again opens a new capture, and A binds.
            Hold(h, 0, Settle);
            OpenCapture(h);
            Tap(h, A);
            probe.Binding.Should().Be(GamepadButton.A);
            probe.Changes.Should().Be(2);
            MenuNavigator.InputSuspended.Should().BeFalse();
            h.Input.MouseButtonsHeld.Should().NotContain(MouseButton.Left, "the A that bound didn't click anything");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void B_binds_without_going_back()
    {
        using var h = new ControllerTestHarness();
        var probe = new RebindProbe();
        var scene = RebindScene(h, probe);
        try
        {
            OpenCapture(h);
            Hold(h, B, Settle);
            probe.Binding.Should().Be(GamepadButton.B);
            probe.BackPresses.Should().Be(0, "the B that bound doesn't also leave the menu");
            h.Input.KeysDown.Should().NotContain(Key.Escape);

            Tap(h, 0);
            Tap(h, B);
            probe.BackPresses.Should().Be(1, "B goes back again once the capture is over");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Back_or_escape_cancels_a_rebind()
    {
        using var h = new ControllerTestHarness();
        var probe = new RebindProbe();
        var scene = RebindScene(h, probe);
        try
        {
            OpenCapture(h);
            Tap(h, Back);
            probe.Binding.Should().Be(GamepadButton.Y);
            probe.Changes.Should().Be(0);
            MenuNavigator.InputSuspended.Should().BeFalse();
            Onion.Navigator.TriggeredControl.Should().BeNull();
            Tap(h, X);
            probe.Binding.Should().Be(GamepadButton.Y, "the capture is over");

            OpenCapture(h);
            ref var input = ref h.Input;
            input.KeysDown!.Add(Key.Escape);
            input.KeysHeld!.Add(Key.Escape);
            input.AnyKey = true;
            h.Step(OneFrame, resetFirst: false, render: true);
            h.Input.KeysHeld!.Remove(Key.Escape);
            h.Step(Settle, render: true);
            MenuNavigator.InputSuspended.Should().BeFalse("Escape on the keyboard cancels too");
            Onion.Navigator.TriggeredControl.Should().BeNull();

            Gamepad.ActiveDevice = InputDevice.Gamepad;
            Tap(h, X);
            probe.Binding.Should().Be(GamepadButton.Y);
            probe.Changes.Should().Be(0);
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Capture_ends_when_its_button_is_not_drawn()
    {
        using var h = new ControllerTestHarness();
        var probe = new RebindProbe();
        var scene = RebindScene(h, probe);
        try
        {
            OpenCapture(h);

            probe.Visible = false; // the player left the Input tab
            h.Step(Settle, render: true);
            MenuNavigator.InputSuspended.Should().BeFalse("the lease lapses without its control");

            probe.Visible = true;
            h.Step(Settle, render: true);
            MenuNavigator.InputSuspended.Should().BeFalse("coming back doesn't resume the old capture");

            Gamepad.ActiveDevice = InputDevice.Gamepad;
            Hold(h, X, Settle);
            probe.Binding.Should().Be(GamepadButton.Y);
            probe.Changes.Should().Be(0);
        }
        finally
        {
            scene.Dispose();
        }
    }
}
