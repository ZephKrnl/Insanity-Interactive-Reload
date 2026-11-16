using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.Localisation;
using Walgelijk.Onion;
using Walgelijk.Onion.Controls;
using Walgelijk.Onion.Layout;
using Walgelijk.SimpleDrawing;

namespace MIR.Controls;

/// <summary>
/// Shows a controller binding and rebinds it: click it, with the mouse or the pad's A, then press the new button.
/// Back on the pad or Escape on the keyboard cancels. The pad's buttons are read by a <see cref="GamepadCapture"/>, which
/// keeps the menus off the pad while it waits. <see cref="InputSwitchButton"/> is the keyboard and mouse version.
/// </summary>
public readonly struct GamepadSwitchButton : IControl
{
    // Buttons captured during Onion's processing, waiting for their control's next Start to hand them over.
    private static readonly Dictionary<int, GamepadButton> captured = new();

    // Only one control is triggered at a time, so one capture serves them all.
    private static readonly GamepadCapture capture = new();
    private static int? capturing; // the control the capture belongs to

    /// <summary>
    /// Draws the button for <paramref name="binding"/>. True on the frame the player rebound it to a different button.
    /// </summary>
    public static bool Start(ref GamepadButton binding, int identity = 0, [CallerLineNumber] int site = 0)
    {
        var (instance, node) = Onion.Tree.Start(IdGen.Create(nameof(GamepadSwitchButton).GetHashCode(), identity, site), new GamepadSwitchButton());

        bool changed = false;
        if (captured.Remove(instance.Identity, out var button) && button != binding)
        {
            binding = button;
            changed = true;
        }

        instance.RenderFocusBox = false;
        instance.Name = IsCapturing(instance) ? Localisation.Get("settings-controller-press-button") : binding.ToDisplayString();
        Onion.Tree.End();
        return changed;
    }

    private static bool IsCapturing(ControlInstance instance)
        => instance.IsTriggered && capturing == instance.Identity && capture.Active;

    internal static void ResetForTests()
    {
        captured.Clear();
        capture.Cancel();
        capturing = null;
    }

    public void OnAdd(in ControlParams p) { }

    public void OnRemove(in ControlParams p)
    {
        if (capturing == p.Identity)
            EndCapture(p.Identity);
    }

    public void OnStart(in ControlParams p) { }

    public void OnProcess(in ControlParams p)
    {
        p.Instance.CaptureFlags = CaptureFlags.Hover;
        p.Instance.Rects.Raycast = p.Instance.Rects.ComputedGlobal;
        p.Instance.Rects.DrawBounds = p.Instance.Rects.ComputedGlobal;

        // Onion keeps processing a control for a while after it stops being drawn, like when the player leaves the Input tab.
        // Its capture ends then rather than carry on unseen, so the menus get the pad back.
        if (!p.Node.AliveLastFrame)
        {
            EndCapture(p.Identity);
            return;
        }

        bool wasTriggered = p.Instance.IsTriggered;

        if (wasTriggered)
        {
            p.Instance.CaptureFlags = CaptureFlags.None;
            p.Instance.Rects.Raycast = null;
            ProcessCapture(p);
        }
        else if (capturing == p.Identity)
            EndCapture(p.Identity); // another control took the trigger, like a keyboard rebind button clicked meanwhile

        if (p.Instance.IsActive && !p.Input.MousePrimaryHeld)
            Onion.Navigator.ActiveControl = null;

        if (!wasTriggered && p.Instance.IsHover)
        {
            IControl.SetCursor(DefaultCursor.Pointer);
            if (p.Input.MousePrimaryRelease)
            {
                // A click, from the mouse or the pad's A. The A is let go by now, and the capture ignores whatever is
                // still held, so the press that opened it can't bind itself.
                Onion.Navigator.FocusedControl = p.Instance.Identity;
                Onion.Navigator.ActiveControl ??= p.Instance.Identity;
                Onion.Navigator.TriggeredControl = p.Instance.Identity;
                BeginCapture(p.Identity);
            }
        }
    }

    /// <summary>
    /// One frame of this control's capture, while it is triggered.
    /// </summary>
    private static void ProcessCapture(in ControlParams p)
    {
        // Triggered some other way than a click, like Onion's keyboard navigation: that is a rebind too.
        if (capturing != p.Identity || !capture.Active)
            BeginCapture(p.Identity);

        if (p.GameState.Input.IsKeyPressed(Key.Escape))
        {
            Game.Main.AudioRenderer.PlayOnce(Sounds.UiBad);
            EndCapture(p.Identity);
            return;
        }

        switch (capture.Update(Gamepad.Current))
        {
            case GamepadCaptureResult.Bound:
                captured[p.Identity] = capture.BoundButton;
                Game.Main.AudioRenderer.PlayOnce(Sounds.UiConfirm);
                EndCapture(p.Identity);
                break;
            case GamepadCaptureResult.Cancelled:
                Game.Main.AudioRenderer.PlayOnce(Sounds.UiBad);
                EndCapture(p.Identity);
                break;
        }
    }

    /// <summary>
    /// Starts a capture for <paramref name="identity"/>, taking it over from any other control.
    /// </summary>
    private static void BeginCapture(int identity)
    {
        capture.Begin();
        capturing = identity;
    }

    /// <summary>
    /// Stops <paramref name="identity"/>'s capture, if it has one, and untriggers it.
    /// </summary>
    private static void EndCapture(int identity)
    {
        if (capturing == identity)
        {
            capture.Cancel();
            capturing = null;
        }
        if (Onion.Navigator.TriggeredControl == identity)
            Onion.Navigator.TriggeredControl = null;
    }

    public void OnRender(in ControlParams p)
    {
        (ControlTree tree, LayoutQueue layout, Input input, GameState state, Node node, ControlInstance instance) = p;

        var t = node.GetAnimationTime();
        var anim = instance.Animations;

        bool triggered = instance.IsTriggered;
        var rect = instance.Rects.Rendered;

        Draw.Font = Assets.Load<Font>("fonts/cascadia-mono.wf");
        Draw.FontSize = p.Theme.FontSize[instance.State];

        if (anim.ShouldRenderText(t))
        {
            // The prompt is longer than a button name: shrink it to fit rather than spill out of the row.
            float room = MathF.Max(rect.Width - rect.Height + 10, 1);
            float textWidth = Draw.CalculateTextWidth(instance.Name);
            if (textWidth > room)
            {
                Draw.FontSize *= MathF.Max(room / textWidth, 0.5f);
                textWidth = Draw.CalculateTextWidth(instance.Name);
            }

            var w = MathF.Min(textWidth + rect.Height - 10, rect.Width);
            var keyRect = rect with { Width = w };
            keyRect = keyRect.Translate(w * -0.5f + rect.Width * 0.5f, 0);

            Draw.Colour = triggered ? Colors.White : Colors.Red.WithAlpha(0.1f);
            Draw.OutlineColour = Colors.Red.WithAlpha(instance.IsHover ? 0.8f : 0.7f);
            Draw.OutlineWidth = 4;
            Draw.Quad(keyRect.Expand(-2), 0, 8);

            Draw.Colour = p.Theme.Text[triggered ? ControlState.Hover : instance.State];
            anim.AnimateColour(ref Draw.Colour, t);
            var ratio = rect.Area / instance.Rects.ComputedGlobal.Area;
            Draw.Text(instance.Name, rect.GetCenter(), new Vector2(ratio),
                HorizontalTextAlign.Center, VerticalTextAlign.Middle, instance.Rects.ComputedGlobal.Width);
        }
    }

    public void OnEnd(in ControlParams p)
    {
    }
}
