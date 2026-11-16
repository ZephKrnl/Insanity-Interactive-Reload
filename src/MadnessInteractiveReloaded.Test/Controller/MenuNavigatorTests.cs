using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Walgelijk;
using Walgelijk.Onion;
using Walgelijk.Onion.Controls;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class MenuNavigatorTests : IDisposable
{
    private const float Dt = ControllerTestHarness.Dt;
    private static readonly Vector2 Up = new(0, 1), Down = new(0, -1), Left = new(-1, 0), Right = new(1, 0);

    public MenuNavigatorTests()
    {
        ResetStatics();
        Gamepad.ActiveDevice = InputDevice.Gamepad; // the navigator only acts while the pad is the active device
    }

    public void Dispose() => ResetStatics();

    private static void ResetStatics()
    {
        Gamepad.ResetForTests(new FakeGamepadSource());
        MenuNavigator.ResetSuspensionForTests();
    }

    /// <summary>
    /// Presses <paramref name="dir"/> for one frame, then lets go.
    /// </summary>
    private static MenuNavResult Press(MenuNavigator nav, Vector2 dir, Vector2 cursor = default)
    {
        var result = nav.Update(dir, Dt, cursor);
        nav.Update(Vector2.Zero, Dt, cursor);
        return result;
    }

    [Fact]
    public void First_press_focuses_nearest_to_cursor()
    {
        var targets = FakeMenuTargets.Grid();
        var nav = new MenuNavigator(targets);

        var result = nav.Update(Right, Dt, new Vector2(820, 380)); // over cell 6, bottom right

        nav.Focused.Should().Be(6);
        targets.Focused.Should().Be(6);
        result.WarpTo.Should().Be(targets.Rects[6].GetCenter());
        targets.Highlights.Should().Equal(6);
        targets.FindCalls.Should().Be(0, "with nothing focused the first press picks a control rather than moving");
    }

    [Fact]
    public void First_press_prefers_the_control_under_the_cursor()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Rects[7] = new Rect(1000, 100, 1900, 400); // a wide control whose centre is far from the cursor
        var nav = new MenuNavigator(targets);

        nav.Update(Down, Dt, new Vector2(1010, 350));

        nav.Focused.Should().Be(7);
    }

    [Fact]
    public void Dpad_moves_focus_in_direction()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        Press(nav, Right);
        nav.Focused.Should().Be(2);
        Press(nav, Down);
        nav.Focused.Should().Be(5);
        Press(nav, Left);
        nav.Focused.Should().Be(4);
        Press(nav, Up);
        nav.Focused.Should().Be(1);

        var result = Press(nav, Up);
        nav.Focused.Should().Be(1, "nothing is above the top row, so focus stays");
        result.WarpTo.Should().BeNull();
    }

    [Fact]
    public void Holding_repeats_after_delay()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);
        var cursor = targets.Rects[1].GetCenter();

        nav.Update(Right, Dt, cursor);
        targets.FindCalls.Should().Be(1, "the press moves straight away");

        nav.Update(Right, 0.34f, cursor);
        targets.FindCalls.Should().Be(1, "no repeat before 0.35 s");
        nav.Update(Right, 0.01f, cursor);
        targets.FindCalls.Should().Be(2, "the first repeat comes at 0.35 s");

        nav.Update(Right, 0.11f, cursor);
        targets.FindCalls.Should().Be(2);
        nav.Update(Right, 0.01f, cursor);
        targets.FindCalls.Should().Be(3, "then every 0.12 s");
        nav.Update(Right, 0.12f, cursor);
        targets.FindCalls.Should().Be(4);

        nav.Update(Vector2.Zero, Dt, cursor);
        nav.Update(Right, Dt, cursor);
        targets.FindCalls.Should().Be(5, "letting go and pressing again moves straight away");
    }

    [Fact]
    public void Holding_moves_once_per_frame_after_a_long_frame()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        nav.Update(Right, Dt, default);
        nav.Update(Right, 2, default); // a hitch
        nav.Update(Right, Dt, default);

        targets.FindCalls.Should().Be(2, "a slow frame doesn't fire a burst of repeats");
    }

    [Fact]
    public void Stick_flick_moves_like_dpad()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        nav.Update(new Vector2(0.4f, 0), Dt, default);
        nav.Focused.Should().Be(1, "a push under 0.5 is not a flick");
        targets.FindCalls.Should().Be(0);

        nav.Update(new Vector2(0.8f, 0.3f), Dt, default);
        nav.Focused.Should().Be(2, "right is the dominant axis");

        nav.Update(new Vector2(0.3f, 0), Dt, default); // back under the threshold: let go
        nav.Update(new Vector2(0.2f, -0.9f), Dt, default);
        nav.Focused.Should().Be(5, "down is the dominant axis");
    }

    [Fact]
    public void Focus_recovers_after_control_dies()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 2;
        var nav = new MenuNavigator(targets);
        var cursor = targets.Rects[2].GetCenter();

        targets.Rects.Remove(2); // a screen change: the focused control isn't drawn any more
        nav.Update(Vector2.Zero, Dt, cursor);
        MenuNavigator.HighlightRect.Should().BeNull();

        var result = nav.Update(Down, Dt, cursor);

        nav.Focused.Should().Be(5, "the live control nearest the cursor");
        result.WarpTo.Should().Be(targets.Rects[5].GetCenter());
        targets.FindCalls.Should().Be(0);
    }

    [Fact]
    public void Dead_focus_returns_to_its_live_ancestor()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Rects[7] = new Rect(1000, 100, 1200, 140); // a dropdown's header
        targets.Rects[8] = new Rect(1000, 140, 1200, 180); // one of its options, open
        targets.Parents[8] = 7;
        targets.Focused = 8;
        var nav = new MenuNavigator(targets);
        nav.Update(Vector2.Zero, Dt, new Vector2(1100, 160));

        // Choosing the option closes the list. Onion drops the dead focus before the navigator sees it.
        targets.Rects.Remove(8);
        targets.Focused = null;
        nav.Update(Down, Dt, new Vector2(800, 350)); // the cursor is over cell 6 by now

        nav.Focused.Should().Be(7, "focus goes back to the header rather than whatever is under the cursor");
    }

    [Fact]
    public void First_press_after_keyboard_and_mouse_moves()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        nav.Reset(); // gameplay, or the window lost focus
        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        nav.Update(Vector2.Zero, Dt, default);
        nav.Update(Vector2.Zero, Dt, default);

        Gamepad.ActiveDevice = InputDevice.Gamepad; // the D-pad press itself selects the pad
        nav.Update(Right, Dt, default);

        nav.Focused.Should().Be(2, "the press that brings the pad back moves straight away");
    }

    [Fact]
    public void Focusing_a_slider_puts_the_cursor_on_its_value()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Sliders.Add(2);
        targets.SliderFractions[2] = 0.2f;
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        var result = nav.Update(Right, Dt, default);

        nav.Focused.Should().Be(2);
        result.WarpTo.Should().Be(new Vector2(440, 150), "A presses where the cursor is, and a slider takes its value from there");
    }

    [Fact]
    public void Suspension_lasts_one_frame_after_the_last_renewal()
    {
        MenuNavigator.BeginFrame();
        MenuNavigator.InputSuspended.Should().BeFalse();

        MenuNavigator.RenewSuspension(); // a capture, during this frame
        MenuNavigator.InputSuspended.Should().BeTrue("it holds for the rest of the frame it was renewed in");

        MenuNavigator.BeginFrame(); // the capture stops renewing
        MenuNavigator.InputSuspended.Should().BeTrue("renewed during the previous frame");

        MenuNavigator.BeginFrame();
        MenuNavigator.InputSuspended.Should().BeFalse("a frame without renewal ends it");
    }

    [Fact]
    public void Warp_goes_to_focused_centre()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        var result = nav.Update(Right, Dt, targets.Rects[1].GetCenter());

        result.WarpTo.Should().Be(new Vector2(500, 150));
        result.ClickAt.Should().BeNull();
        targets.Highlights.Should().Equal(2);
        MenuNavigator.HighlightRect.Should().Be(targets.Rects[2]);
    }

    [Fact]
    public void Warp_follows_a_focused_control_that_moves()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);
        var warp = nav.Update(Right, Dt, default).WarpTo!.Value;

        // The control scrolls up 30 px while the cursor still sits where the warp put it (truncated to whole pixels).
        targets.Rects[2] = targets.Rects[2].Translate(0, -30);
        var result = nav.Update(Vector2.Zero, Dt, new Vector2(MathF.Truncate(warp.X), MathF.Truncate(warp.Y)));
        result.WarpTo.Should().Be(warp + new Vector2(0, -30));

        // Once the cursor is somewhere else, it stays there.
        targets.Rects[2] = targets.Rects[2].Translate(0, -30);
        nav.Update(Vector2.Zero, Dt, new Vector2(100, 900)).WarpTo.Should().BeNull();
    }

    [Fact]
    public void Slider_nudge_clicks_five_percent()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Sliders.Add(2);
        targets.Focused = 2;
        var nav = new MenuNavigator(targets);
        var centre = targets.Rects[2].GetCenter(); // (500, 150), 200 px wide

        var result = Press(nav, Right, centre);
        result.ClickAt.Should().Be(centre + new Vector2(10, 0));
        result.WarpTo.Should().BeNull();
        nav.Focused.Should().Be(2, "left and right adjust a slider instead of leaving it");
        targets.FindCalls.Should().Be(0);

        Press(nav, Left, centre).ClickAt.Should().Be(centre - new Vector2(10, 0));

        var nearEdge = Press(nav, Right, new Vector2(595, 150)).ClickAt!.Value;
        nearEdge.X.Should().BeInRange(599, 600, "the click stays inside the slider");
        new Rect(400, 100, 600, 200).ContainsPoint(nearEdge).Should().BeTrue("Onion's hit test excludes the edges");

        Press(nav, Down, centre);
        nav.Focused.Should().Be(5, "up and down still leave the slider");
    }

    [Fact]
    public void Slider_nudge_starts_from_the_slider_value()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Sliders.Add(2);
        targets.SliderFractions[2] = 0.2f; // 20% along: x = 440
        targets.Focused = 2;
        var nav = new MenuNavigator(targets);

        var click = Press(nav, Right, targets.Rects[2].GetCenter()).ClickAt;

        click.Should().Be(new Vector2(450, 150), "a nudge moves the value 5% from where it is, not from where the cursor is");
    }

    [Fact]
    public void Offscreen_focus_requests_scroll()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Viewport = new Rect(0, 0, 1920, 250); // the top row shows, the bottom row (y 300 to 400) is scrolled out of view
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        var result = nav.Update(Down, Dt, default);
        nav.Focused.Should().Be(4);
        result.ScrollDelta.Should().BeLessThan(0, "a control below the view scrolls down");
        result.WarpTo!.Value.Y.Should().BeLessThan(250, "the cursor stays in the view, where scrolling reaches the scroll view");
        nav.Update(Down, Dt, default).ScrollDelta.Should().BeLessThan(0, "it keeps scrolling while the control is out of view");

        targets.Viewport = new Rect(0, 280, 1920, 1080); // scrolled: the bottom row shows, the top row is above the view
        nav.Update(Vector2.Zero, Dt, default).ScrollDelta.Should().Be(0, "nothing to do once it is in view");

        result = nav.Update(Up, Dt, default);
        nav.Focused.Should().Be(1);
        result.ScrollDelta.Should().BeGreaterThan(0, "a control above the view scrolls up");
    }

    [Fact]
    public void Clicked_focus_does_not_auto_scroll()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Viewport = new Rect(0, 0, 1920, 350); // the bottom row is half out of view
        targets.Focused = 4; // the free cursor clicked it: the navigator didn't put the cursor there
        var nav = new MenuNavigator(targets);

        nav.Update(Vector2.Zero, Dt, new Vector2(200, 340)).ScrollDelta.Should().Be(0, "the view doesn't slide a clicked control from under the cursor");
        MenuNavigator.HighlightRect.Should().Be(new Rect(100, 300, 300, 350), "the highlight shows the part in view");
    }

    [Fact]
    public void Auto_scroll_gives_up_when_the_view_cannot_scroll()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Viewport = new Rect(0, 0, 1920, 250);
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);
        nav.Update(Down, Dt, default);

        float scrolled = 0;
        for (int i = 0; i < 120; i++)
            scrolled += nav.Update(Vector2.Zero, Dt, default).ScrollDelta;
        var last = nav.Update(Vector2.Zero, Dt, default).ScrollDelta;

        scrolled.Should().BeLessThan(0);
        last.Should().Be(0, "a view at the end of its range never brings the control in, so the scroll stops");
    }

    [Fact]
    public void Keyboard_and_mouse_hide_highlight_and_stop_navigation()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);
        nav.Update(Vector2.Zero, Dt, default);
        MenuNavigator.HighlightRect.Should().Be(targets.Rects[1]);

        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        var result = nav.Update(Right, Dt, default);

        result.Should().Be(default(MenuNavResult));
        MenuNavigator.HighlightRect.Should().BeNull();
        targets.FindCalls.Should().Be(0);
    }

    [Fact]
    public void Free_cursor_leaving_the_control_clears_focus()
    {
        var targets = FakeMenuTargets.Grid();
        targets.Focused = 1;
        var nav = new MenuNavigator(targets);

        nav.FreeCursorMoved(new Vector2(250, 160)); // a drifting stick nudged it, still on the control
        targets.Focused.Should().Be(1);

        nav.FreeCursorMoved(new Vector2(350, 160));
        targets.Focused.Should().BeNull();
        nav.Update(Vector2.Zero, Dt, new Vector2(350, 160));
        MenuNavigator.HighlightRect.Should().BeNull();
    }

    [Fact]
    public void New_dialog_takes_focus_once()
    {
        var targets = FakeMenuTargets.Grid();
        var nav = new MenuNavigator(targets);
        nav.Update(Vector2.Zero, Dt, default);

        targets.Dialog = 4; // a dialog opened, and nothing is focused
        var result = nav.Update(Right, Dt, default);
        nav.Focused.Should().Be(4, "the press that meets a new dialog only puts the focus in it");
        result.WarpTo.Should().Be(targets.Rects[4].GetCenter());

        nav.Update(Vector2.Zero, Dt, default);
        Press(nav, Right);
        nav.Focused.Should().Be(5, "then the D-pad moves as usual");
        nav.Update(Vector2.Zero, Dt, default);
        nav.Focused.Should().Be(5, "the open dialog doesn't pull the focus back");
    }

    [Fact]
    public void Nearest_in_direction_follows_onion_rules()
    {
        var targets = FakeMenuTargets.Grid();
        var from = targets.Rects[5].GetCenter();

        MenuNavigator.NearestInDirection(targets.Live(), 5, from, Up).Should().Be(2, "up is +Y");
        MenuNavigator.NearestInDirection(targets.Live(), 5, from, Right).Should().Be(6);
        MenuNavigator.NearestInDirection(targets.Live(), 5, from, Left).Should().Be(4);
        MenuNavigator.NearestInDirection(targets.Live(), 5, from, Down).Should().BeNull();
    }

    // Through the controller loop event.

    private static ushort DPadRight => ControllerTestHarness.ButtonDPadRight;

    [Fact]
    public void Mouse_use_hides_highlight()
    {
        var targets = FakeMenuTargets.Grid();
        using var h = new ControllerTestHarness(targets);

        h.Frame();
        h.Frame(DPadRight);
        targets.Focused.Should().Be(6, "cell 6 is nearest the cursor at the centre of the window");
        MenuNavigator.HighlightRect.Should().Be(targets.Rects[6]);
        h.Warper.Positions.Should().Equal(targets.Rects[6].GetCenter());

        h.Frame();
        MenuNavigator.HighlightRect.Should().NotBeNull("the warp's echo is not the user moving the mouse");

        h.Warper.OsMouse += new Vector2(50, 0);
        h.Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
        MenuNavigator.HighlightRect.Should().BeNull();
        targets.Focused.Should().BeNull("the pad's focus goes when the mouse takes over");
    }

    [Fact]
    public void Suspended_navigator_does_nothing()
    {
        var targets = FakeMenuTargets.Grid();
        using var h = new ControllerTestHarness(targets);
        h.Frame();

        const ushort everything = ControllerTestHarness.ButtonA | ControllerTestHarness.ButtonB | ControllerTestHarness.ButtonStart | 0x0008;
        for (int i = 0; i < 3; i++)
        {
            MenuNavigator.RenewSuspension(); // a capture, renewing during the frame before
            h.Frame(everything, left: new Vector2(1, 0), right: new Vector2(1, 0), leftTrigger: ControllerTestHarness.TriggerFull);
            MenuNavigator.InputSuspended.Should().BeTrue();

            h.Warper.Positions.Should().BeEmpty();
            h.Input.MouseButtonsDown.Should().BeEmpty();
            h.Input.MouseButtonsHeld.Should().BeEmpty();
            h.Input.KeysDown.Should().BeEmpty("neither B nor Start sends Escape while a capture owns the pad");
            if (i == 0)
                MenuUiUtils.PadBackRequested(null).Should().BeFalse("B doesn't press the menu's back button either");
            h.Input.MouseScrollDelta.Should().Be(0);
            targets.Focused.Should().BeNull();
            MenuNavigator.HighlightRect.Should().BeNull();
        }

        // The capture stops renewing with the D-pad still held: that is not a fresh press.
        h.Frame(DPadRight);
        MenuNavigator.InputSuspended.Should().BeFalse();
        targets.Focused.Should().BeNull("a direction held through the capture has to be let go first");
        h.Warper.Positions.Should().BeEmpty();

        h.Frame();
        h.Frame(DPadRight);
        targets.Focused.Should().NotBeNull();
    }

    [Fact]
    public void Right_stick_moves_free_cursor_in_menus()
    {
        var targets = FakeMenuTargets.Grid();
        using var h = new ControllerTestHarness(targets);
        h.Frame();
        h.Frame(DPadRight);
        h.Frame();
        targets.Focused.Should().Be(6);
        var start = h.Loop.VirtualCursor;
        int warps = h.Warper.Positions.Count;

        for (int i = 0; i < 10; i++)
            h.Frame(right: new Vector2(1, 0));

        h.Loop.VirtualCursor.X.Should().BeGreaterThan(start.X + 200);
        h.Warper.Positions.Should().HaveCount(warps + 10);
        h.Input.WindowMousePosition.Should().Be(h.Loop.VirtualCursor);
        targets.Focused.Should().BeNull("the cursor left the focused control");
        MenuNavigator.HighlightRect.Should().BeNull();

        // A left stick push short of a flick neither navigates nor moves the cursor.
        h.Frame();
        warps = h.Warper.Positions.Count;
        h.Frame(left: new Vector2(0.4f, 0));
        h.Frame(left: new Vector2(0.4f, 0));
        h.Warper.Positions.Should().HaveCount(warps);
        targets.Focused.Should().BeNull();
    }

    [Fact]
    public void Triggers_scroll_in_menus()
    {
        using var h = new ControllerTestHarness(FakeMenuTargets.Grid());
        h.Frame();

        h.Frame(leftTrigger: ControllerTestHarness.TriggerFull);
        h.Input.MouseScrollDelta.Should().BeApproximately(ControllerLoopEvent.ScrollSpeed * Dt, 0.0001f, "LT scrolls up");

        h.Frame(rightTrigger: ControllerTestHarness.TriggerFull);
        h.Input.MouseScrollDelta.Should().BeApproximately(-ControllerLoopEvent.ScrollSpeed * Dt, 0.0001f, "RT scrolls down");

        h.Frame(right: new Vector2(0, 1));
        h.Input.MouseScrollDelta.Should().Be(0, "the right stick moves the cursor now");
    }


    [Fact]
    public void First_dpad_press_after_the_window_regains_focus_moves()
    {
        var targets = FakeMenuTargets.Grid();
        using var h = new ControllerTestHarness(targets);
        h.Frame();

        h.Window.Focused = false;
        h.Frame(); // navigation stops while the window is unfocused
        h.Window.Focused = true;
        h.Warper.OsMouse += new Vector2(50, 0); // the mouse brings the window back
        h.Frame();
        h.Frame();
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);

        h.Frame(DPadRight);

        targets.Focused.Should().NotBeNull("the press that brings the pad back navigates straight away");
    }

    /// <summary>
    /// Stands in for a pad button capture: renews the suspension every frame it is active and binds B when it is pressed,
    /// like the settings' rebind control.
    /// </summary>
    private sealed class CaptureProbe : Walgelijk.System
    {
        public bool Active;
        public GamepadButton? Bound;

        public override void Update()
        {
            if (!Active)
                return;
            if (Gamepad.Pressed(GamepadButton.B))
            {
                Bound = GamepadButton.B;
                Active = false;
                return;
            }
            MenuNavigator.RenewSuspension();
        }
    }

    /// <summary>
    /// Draws a menu back button every frame and counts its presses.
    /// </summary>
    private sealed class BackProbe : Walgelijk.System
    {
        public int Presses;

        public override void Update()
        {
            if (MenuUiUtils.BackButton())
                Presses++;
        }
    }

    [Fact]
    public void B_bound_by_a_capture_does_not_press_back()
    {
        using var h = new ControllerTestHarness(FakeMenuTargets.Grid());
        var scene = h.PrepareMenuSceneWithDisplayPlayer();
        try
        {
            // The capture is drawn before the back button, as the settings rows are.
            var capture = new CaptureProbe { ExecutionOrder = 0 };
            var back = new BackProbe { ExecutionOrder = 1 };
            scene.AddSystem(capture);
            scene.AddSystem(back);

            capture.Active = true;
            h.SetPad();
            h.Step(Settle);
            MenuNavigator.InputSuspended.Should().BeTrue();

            h.SetPad(ControllerTestHarness.ButtonB);
            h.Step(OneFrame);
            capture.Bound.Should().Be(GamepadButton.B);
            back.Presses.Should().Be(0, "the suspension lasts the whole frame the capture binds in");
            h.Input.KeysDown.Should().NotContain(Key.Escape);

            h.Step(Settle); // B still held
            back.Presses.Should().Be(0);
            MenuNavigator.InputSuspended.Should().BeFalse("a frame without renewal ends it");

            h.SetPad();
            h.Step(OneFrame);
            h.SetPad(ControllerTestHarness.ButtonB);
            h.Step(OneFrame);
            back.Presses.Should().Be(1, "B goes back again once the capture is over");
        }
        finally
        {
            scene.Dispose();
        }
    }

    // On the real Onion tree.

    private static readonly TimeSpan OneFrame = TimeSpan.FromSeconds(Dt);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// A scene with Onion and <paramref name="systems"/>, with the harness's loop event added.
    /// </summary>
    private static Scene OnionScene(ControllerTestHarness h, params Walgelijk.System[] systems)
    {
        var scene = h.Game.Scene = new Scene(h.Game, "menu_navigator_onion_test");
        scene.AddSystem(new OnionSystem());
        foreach (var system in systems)
            scene.AddSystem(system);
        h.Game.AdditionalLoopEvents.Add(h.Loop);
        return scene;
    }

    /// <summary>
    /// Presses <paramref name="button"/> for a frame, then lets go and lets Onion catch up.
    /// </summary>
    private static void Tap(ControllerTestHarness h, ushort button, TimeSpan? settle = null)
    {
        h.SetPad(button);
        h.Step(OneFrame, render: true);
        h.SetPad();
        h.Step(settle ?? Settle, render: true);
    }

    private static Node FocusedNode() => Onion.Tree.Nodes[Onion.Navigator.FocusedControl!.Value];

    private static bool IsInside(Node node, int ancestor)
    {
        for (var n = node.Parent; n != null; n = n.Parent)
            if (n.Identity == ancestor)
                return true;
        return false;
    }

    /// <summary>
    /// Draws one horizontal Onion slider, the way the settings menu does.
    /// </summary>
    private sealed class SliderProbe : Walgelijk.System
    {
        public float Value = 0.2f; // away from the middle, where the cursor would go for another control

        public override void Update()
        {
            Ui.Layout.Size(400, 40).Move(760, 520);
            Ui.FloatSlider(ref Value, Direction.Horizontal, (0, 1), 0.01f);
        }
    }

    [Fact]
    public void Slider_nudge_moves_an_onion_slider()
    {
        using var h = new ControllerTestHarness(); // the real Onion tree
        var probe = new SliderProbe();
        var scene = OnionScene(h, probe);
        try
        {
            h.SetPad();
            h.Step(Settle, render: true);

            h.SetPad(DPadRight);
            h.Step(OneFrame, render: true);
            MenuNavigator.HighlightRect.Should().NotBeNull("the first press focuses the slider, the only control");
            h.SetPad();
            h.Step(Settle, render: true);
            probe.Value.Should().Be(0.2f, "focusing a slider leaves its value alone");

            Tap(h, DPadRight);
            probe.Value.Should().BeApproximately(0.25f, 0.001f, "the nudge moves 5% from the value");

            Tap(h, ControllerTestHarness.ButtonDPadLeft);
            Tap(h, ControllerTestHarness.ButtonDPadLeft);
            probe.Value.Should().BeApproximately(0.15f, 0.001f);
            h.Input.MouseButtonsHeld.Should().NotContain(MouseButton.Left, "every nudge's click is released");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void A_on_a_focused_slider_keeps_its_value()
    {
        using var h = new ControllerTestHarness();
        var probe = new SliderProbe();
        var scene = OnionScene(h, probe);
        try
        {
            h.SetPad();
            h.Step(Settle, render: true);
            Tap(h, DPadRight); // focus the slider

            Tap(h, ControllerTestHarness.ButtonA);

            probe.Value.Should().BeApproximately(0.2f, 0.001f, "A presses on the slider's value, which sets it to what it was");
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <summary>
    /// A dropdown with a slider right under it, behind its open list, the way the settings' language and screenshake rows
    /// sit.
    /// </summary>
    private sealed class DropdownProbe : Walgelijk.System
    {
        public static readonly string[] Options = ["One", "Two", "Three"];
        public int Selected;
        public float Value = 0.2f;

        public override void Update()
        {
            Ui.Layout.Size(400, 40).Move(760, 400);
            Ui.Dropdown(Options, ref Selected);
            Ui.Layout.Size(400, 40).Move(760, 443);
            Ui.FloatSlider(ref Value, Direction.Horizontal, (0, 1), 0.01f);
        }
    }

    [Fact]
    public void Open_dropdown_keeps_navigation_inside_its_options()
    {
        using var h = new ControllerTestHarness();
        var probe = new DropdownProbe();
        var scene = OnionScene(h, probe);
        try
        {
            h.Warper.OsMouse = new Vector2(960, 420); // over the dropdown
            h.SetPad();
            h.Step(Settle, render: true);
            Tap(h, ControllerTestHarness.ButtonDPadDown);
            var dropdown = FocusedNode();
            dropdown.Behaviour.GetType().GetGenericTypeDefinition().Should().Be(typeof(Dropdown<>));

            Tap(h, ControllerTestHarness.ButtonA, TimeSpan.FromSeconds(0.5));
            Onion.Navigator.TriggeredControl.Should().Be(dropdown.Identity, "A opens the dropdown");

            var visited = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Tap(h, ControllerTestHarness.ButtonDPadDown);
                var focused = FocusedNode();
                focused.Behaviour.Should().BeOfType<Button>("press {0} stays on the options, not the slider behind them", i);
                IsInside(focused, dropdown.Identity).Should().BeTrue();
                visited.Add(focused.Identity);
            }
            visited.Distinct().Should().HaveCount(3, "down walks the three options and stops at the last");

            Tap(h, ControllerTestHarness.ButtonDPadLeft);
            Tap(h, ControllerTestHarness.ButtonDPadRight);
            probe.Value.Should().Be(0.2f, "left and right on an option can't reach the slider behind the list");
            probe.Selected.Should().Be(0);

            Tap(h, ControllerTestHarness.ButtonA);
            probe.Selected.Should().Be(2, "A picks the focused option");
            Onion.Navigator.TriggeredControl.Should().BeNull("picking an option closes the list");

            Tap(h, ControllerTestHarness.ButtonDPadDown);
            Onion.Navigator.FocusedControl.Should().Be(dropdown.Identity, "focus goes back to the header, not the slider nearer the cursor");
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <summary>
    /// Counts the frames Escape reached the scene systems, the way the pause menu reads it.
    /// </summary>
    private sealed class EscapeProbe : Walgelijk.System
    {
        public int Presses;

        public override void Update()
        {
            if (Input.IsKeyPressed(Key.Escape))
                Presses++;
        }
    }

    /// <summary>
    /// Opens <see cref="DropdownProbe"/>'s dropdown with the pad and moves onto an option. Returns the dropdown's node.
    /// </summary>
    private static Node OpenDropdown(ControllerTestHarness h)
    {
        Tap(h, ControllerTestHarness.ButtonDPadDown);
        var dropdown = FocusedNode();
        dropdown.Behaviour.GetType().GetGenericTypeDefinition().Should().Be(typeof(Dropdown<>));

        Tap(h, ControllerTestHarness.ButtonA, TimeSpan.FromSeconds(0.5));
        Onion.Navigator.TriggeredControl.Should().Be(dropdown.Identity, "A opens the dropdown");
        Tap(h, ControllerTestHarness.ButtonDPadDown);
        IsInside(FocusedNode(), dropdown.Identity).Should().BeTrue("the focus is on an option");
        return dropdown;
    }

    [Fact]
    public void B_on_open_dropdown_closes_it_and_stays_in_settings()
    {
        using var h = new ControllerTestHarness();
        var probe = new DropdownProbe();
        var back = new BackProbe();
        var escape = new EscapeProbe();
        var scene = OnionScene(h, probe, back, escape);
        try
        {
            h.Warper.OsMouse = new Vector2(960, 420); // over the dropdown
            h.SetPad();
            h.Step(Settle, render: true);
            var dropdown = OpenDropdown(h);

            h.SetPad(ControllerTestHarness.ButtonB);
            h.Step(OneFrame, render: true);
            MenuUiUtils.PadBackRequested(null).Should().BeFalse("B closed the list, so it doesn't also go back");
            back.Presses.Should().Be(0);
            escape.Presses.Should().Be(0, "no Escape reaches the menu's systems, which would leave or unpause");
            Onion.Navigator.TriggeredControl.Should().BeNull("the list is closed");
            Onion.Navigator.FocusedControl.Should().Be(dropdown.Identity, "focus goes back to the dropdown's header");
            MenuNavigator.HighlightRect.Should().NotBeNull();

            h.SetPad();
            h.Step(Settle, render: true);
            back.Presses.Should().Be(0);
            escape.Presses.Should().Be(0);
            Onion.Navigator.FocusedControl.Should().Be(dropdown.Identity);
            probe.Selected.Should().Be(0, "closing with B doesn't pick an option");

            Tap(h, ControllerTestHarness.ButtonA, TimeSpan.FromSeconds(0.5));
            Onion.Navigator.TriggeredControl.Should().Be(dropdown.Identity, "the cursor is on the header, so A opens it again");
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void B_without_popup_still_goes_back()
    {
        using var h = new ControllerTestHarness();
        var probe = new DropdownProbe();
        var back = new BackProbe();
        var escape = new EscapeProbe();
        var scene = OnionScene(h, probe, back, escape);
        try
        {
            h.Warper.OsMouse = new Vector2(960, 420);
            h.SetPad();
            h.Step(Settle, render: true);
            Tap(h, ControllerTestHarness.ButtonDPadDown); // the dropdown is focused, but closed
            Onion.Navigator.TriggeredControl.Should().BeNull();

            h.SetPad(ControllerTestHarness.ButtonB);
            h.Step(OneFrame, render: true);
            back.Presses.Should().Be(1, "B goes back");
            escape.Presses.Should().Be(1, "B still presses Escape");
            h.SetPad();
            h.Step(Settle, render: true);

            // Once B has closed a list, the next B goes back again.
            OpenDropdown(h);
            Tap(h, ControllerTestHarness.ButtonB);
            back.Presses.Should().Be(1);
            Tap(h, ControllerTestHarness.ButtonB);
            back.Presses.Should().Be(2);
            escape.Presses.Should().Be(2);
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <summary>
    /// The credits' layout: a tall scroll view with nothing to focus in it, and a button outside it.
    /// </summary>
    private sealed class CreditsProbe : Walgelijk.System
    {
        public int? ScrollView;

        public override void Update()
        {
            Ui.Layout.Size(600, 600).Move(660, 100).VerticalLayout();
            Ui.StartScrollView();
            ScrollView = Onion.Tree.CurrentNode?.Identity;
            {
                for (int i = 0; i < 20; i++)
                {
                    Ui.Layout.FitWidth(true).Height(100);
                    Ui.TextRect("Credit " + i, HorizontalTextAlign.Center, VerticalTextAlign.Top, identity: i);
                }
            }
            Ui.End();

            Ui.Layout.Size(170, 40).Move(1700, 1000);
            Ui.ClickButton("Open source libraries");
        }
    }

    [Fact]
    public void Triggers_scroll_the_view_when_the_cursor_is_not_over_one()
    {
        using var h = new ControllerTestHarness();
        var probe = new CreditsProbe();
        var scene = OnionScene(h, probe);
        try
        {
            h.Warper.OsMouse = new Vector2(960, 400); // over the scroll view
            h.SetPad();
            h.Step(Settle, render: true);
            float Offset() => Onion.Tree.Instances[probe.ScrollView!.Value].InnerScrollOffset.Y;

            // With the cursor over it, RT scrolls it, the way the mouse wheel does.
            h.SetPad(rightTrigger: ControllerTestHarness.TriggerFull);
            h.Step(TimeSpan.FromSeconds(0.3), render: true);
            h.SetPad();
            h.Step(Settle, render: true);
            var scrolled = Offset();
            scrolled.Should().BeLessThan(0, "the view scrolls down with the cursor over it");

            // The D-pad focuses the button, which is outside the view, and puts the cursor on it.
            h.Warper.OsMouse = new Vector2(1785, 1020);
            h.Step(Settle, render: true);
            Tap(h, ControllerTestHarness.ButtonDPadUp);
            var button = Onion.Navigator.FocusedControl;
            button.Should().NotBeNull();
            FocusedNode().Behaviour.Should().BeOfType<Button>();
            int warps = h.Warper.Positions.Count;
            var cursor = h.Warper.OsMouse;

            h.SetPad(leftTrigger: ControllerTestHarness.TriggerFull);
            h.Step(TimeSpan.FromSeconds(0.3), render: true);
            h.SetPad();
            h.Step(Settle, render: true);

            Offset().Should().BeGreaterThan(scrolled, "LT scrolls the only view on screen back up, though the cursor is not over it");
            Onion.Navigator.FocusedControl.Should().Be(button, "scrolling keeps the focus");
            h.Warper.Positions.Should().HaveCount(warps, "the OS cursor isn't warped to scroll");
            h.Warper.OsMouse.Should().Be(cursor, "it stays on the button");
            MenuNavigator.HighlightRect.Should().NotBeNull();
        }
        finally
        {
            scene.Dispose();
        }
    }

    /// <summary>
    /// A button high above where a confirmation dialog opens.
    /// </summary>
    private sealed class ButtonProbe : Walgelijk.System
    {
        public int Clicks;

        public override void Update()
        {
            Ui.Layout.Size(300, 40).Move(810, 100);
            if (Ui.ClickButton("Behind"))
                Clicks++;
        }
    }

    [Fact]
    public void Confirmation_dialog_keeps_navigation_inside_it()
    {
        using var h = new ControllerTestHarness();
        var behind = new ButtonProbe();
        var scene = OnionScene(h, behind, new ConfirmationDialogSystem());
        try
        {
            h.SetPad();
            h.Step(Settle, render: true);
            var dialog = scene.AttachComponent(scene.CreateEntity(), new ConfirmationDialogComponent("Are you sure?", () => { }));
            h.Step(Settle, render: true);
            dialog.WindowNode.Should().NotBeNull();

            foreach (var direction in new[] { ControllerTestHarness.ButtonDPadUp, ControllerTestHarness.ButtonDPadUp,
                ControllerTestHarness.ButtonDPadUp, ControllerTestHarness.ButtonDPadLeft, ControllerTestHarness.ButtonDPadRight })
            {
                Tap(h, direction);
                IsInside(FocusedNode(), dialog.WindowNode!.Value).Should().BeTrue("navigation stays inside the open dialog");
            }
            behind.Clicks.Should().Be(0);
        }
        finally
        {
            scene.Dispose();
        }
    }

    [Fact]
    public void Pad_focus_jumps_into_new_dialog()
    {
        using var h = new ControllerTestHarness();
        var behind = new ButtonProbe();
        var scene = OnionScene(h, behind, new ConfirmationDialogSystem());
        try
        {
            h.Warper.OsMouse = new Vector2(960, 120); // over the button behind
            h.SetPad();
            h.Step(Settle, render: true);
            Tap(h, ControllerTestHarness.ButtonDPadUp);
            var behindId = Onion.Navigator.FocusedControl;
            behindId.Should().NotBeNull("the pad focused the button");

            bool agreed = false;
            var dialog = scene.AttachComponent(scene.CreateEntity(), new ConfirmationDialogComponent("Are you sure?", () => agreed = true));
            h.SetPad(); // no pad input while it opens
            h.Step(Settle, render: true);

            dialog.WindowNode.Should().NotBeNull();
            IsInside(FocusedNode(), dialog.WindowNode!.Value).Should().BeTrue("the focus jumps into the dialog as it opens");
            Onion.Navigator.FocusedControl.Should().Be(dialog.CancelNode, "it starts on No, not the window's close button");
            MenuNavigator.HighlightRect.Should().NotBeNull();

            Tap(h, ControllerTestHarness.ButtonA);
            behind.Clicks.Should().Be(0, "A answers the dialog, not the button behind it");
            agreed.Should().BeFalse("the focus started on No");
            scene.GetAllComponentsOfType<ConfirmationDialogComponent>().Should().BeEmpty("No closed the dialog");
        }
        finally
        {
            scene.Dispose();
        }
    }
}

/// <summary>
/// A 3 by 2 grid of controls, ids 1 to 6 row by row, each 200 by 100 px with 100 px gaps:
/// <code>
///  1 (100..300, 100..200)   2 (400..600)   3 (700..900)
///  4 (100..300, 300..400)   5              6
/// </code>
/// </summary>
internal sealed class FakeMenuTargets : IMenuTargets
{
    public readonly Dictionary<int, Rect> Rects = new();
    public readonly HashSet<int> Sliders = new();
    public readonly Dictionary<int, float> SliderFractions = new();
    public readonly List<int> Highlights = new();

    /// <summary>
    /// What <see cref="ViewportOf"/> returns, the window when null.
    /// </summary>
    public Rect? Viewport;

    public int FindCalls;

    public int? Focused { get; set; }

    public static FakeMenuTargets Grid()
    {
        var t = new FakeMenuTargets();
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
                t.Rects[row * 3 + col + 1] = new Rect(100 + col * 300, 100 + row * 200, 300 + col * 300, 200 + row * 200);
        return t;
    }

    public IEnumerable<(int Id, Rect Rect)> Live() => Rects.Select(static p => (p.Key, p.Value)).ToArray();

    public int? FindInDirection(int from, Vector2 dir)
    {
        FindCalls++;
        return MenuNavigator.NearestInDirection(Live(), from, Rects[from].GetCenter(), dir);
    }

    public void Highlight(int id) => Highlights.Add(id);

    public Rect? ViewportOf(int id) => Viewport ?? new Rect(0, 0, 1920, 1080);

    public bool IsSlider(int id) => Sliders.Contains(id);

    public float? SliderFraction(int id) => SliderFractions.TryGetValue(id, out var f) ? f : null;

    /// <summary>
    /// Child to parent. Controls without an entry have no parent.
    /// </summary>
    public readonly Dictionary<int, int> Parents = new();

    public int? LiveAncestorOf(int id)
    {
        for (var n = id; Parents.TryGetValue(n, out var parent); n = parent)
            if (Rects.ContainsKey(parent))
                return parent;
        return null;
    }

    /// <summary>
    /// The control whose popup is open, which <see cref="ClosePopup"/> closes. Null when none is open.
    /// </summary>
    public int? Popup;

    public (int Id, Rect Rect)? ClosePopup()
    {
        if (Popup is not int popup || !Rects.TryGetValue(popup, out var rect))
            return null;
        Popup = null;
        return (popup, rect);
    }

    /// <summary>
    /// What <see cref="DialogFocus"/> returns: null, no dialog, by default.
    /// </summary>
    public int? Dialog;

    public int? DialogFocus() => Dialog;

    /// <summary>
    /// Scroll views, for <see cref="ScrollsAt"/> and <see cref="LargestScrollView"/>. None by default: the grid is all buttons.
    /// </summary>
    public readonly List<Rect> ScrollViews = new();

    public bool ScrollsAt(Vector2 point) => ScrollViews.Any(v => v.ContainsPoint(point));

    public Rect? LargestScrollView() => ScrollViews.Count == 0 ? null : ScrollViews.MaxBy(static v => v.Area);
}
