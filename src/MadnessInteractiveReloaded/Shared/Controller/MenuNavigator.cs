using System;
using System.Collections.Generic;
using System.Numerics;
using Walgelijk;
using Walgelijk.SimpleDrawing;

namespace MIR;

/// <summary>
/// The menu controls <see cref="MenuNavigator"/> moves between. <see cref="OnionMenuTargets"/> is the real one.
/// Rects are in window coordinates, Y down. Directions are up-positive, like the sticks.
/// </summary>
public interface IMenuTargets
{
    /// <summary>
    /// Every control that can be focused right now, with its rect.
    /// </summary>
    IEnumerable<(int Id, Rect Rect)> Live();

    /// <summary>
    /// The control to move to from <paramref name="from"/> in <paramref name="dir"/>, or null when there is none.
    /// </summary>
    int? FindInDirection(int from, Vector2 dir);

    /// <summary>
    /// The focused control, shared with the mouse and keyboard.
    /// </summary>
    int? Focused { get; set; }

    /// <summary>
    /// Flashes the control the focus just moved to.
    /// </summary>
    void Highlight(int id);

    /// <summary>
    /// The visible rect of the scroll view the control sits in, or the window.
    /// </summary>
    Rect? ViewportOf(int id);

    bool IsSlider(int id);

    /// <summary>
    /// How far along its range the slider's value is, from 0 to 1, or null when it can't be read.
    /// </summary>
    float? SliderFraction(int id);

    /// <summary>
    /// The nearest ancestor of <paramref name="id"/> that is in <see cref="Live"/>, or null. Where focus goes back to when
    /// a control dies, like a dropdown's header once an option closes it.
    /// </summary>
    int? LiveAncestorOf(int id);

    /// <summary>
    /// Closes the open popup, an open dropdown, the way Escape would, and returns the control that opened it (the dropdown's
    /// header) with its rect. Null when no popup is open. A key capture is not a popup, and neither is a confirmation dialog.
    /// </summary>
    (int Id, Rect Rect)? ClosePopup();

    /// <summary>
    /// Where focus starts in an open confirmation dialog: its cancel button when that can be told, else its first target.
    /// Null when no dialog is open, or its controls aren't live yet.
    /// </summary>
    int? DialogFocus();

    /// <summary>
    /// True when the mouse wheel at <paramref name="point"/> would scroll something, like a scroll view.
    /// </summary>
    bool ScrollsAt(Vector2 point);

    /// <summary>
    /// The visible rect of the biggest scroll view on screen, or null when there is none. While a dropdown or dialog is
    /// open, only one inside it counts.
    /// </summary>
    Rect? LargestScrollView();
}

/// <summary>
/// What <see cref="ControllerLoopEvent"/> should do to the OS cursor and the input this frame.
/// </summary>
public readonly struct MenuNavResult
{
    /// <summary>
    /// Put the cursor here (window coordinates): on the newly focused control, or following it as it moves.
    /// </summary>
    public Vector2? WarpTo { get; init; }

    /// <summary>
    /// Put the cursor here and click: a slider nudge.
    /// </summary>
    public Vector2? ClickAt { get; init; }

    /// <summary>
    /// Scroll to add this frame, up-positive like the mouse wheel: auto-scroll toward a focused control out of view.
    /// </summary>
    public float ScrollDelta { get; init; }
}

/// <summary>
/// Console-style menu navigation for the pad: the D-pad or a left stick flick moves the focus between controls, the
/// cursor follows the focus so A clicks it, and a highlight shows where it is. Works on any <see cref="IMenuTargets"/>;
/// <see cref="ControllerLoopEvent"/> drives it in menu mode.
/// </summary>
public sealed class MenuNavigator
{
    /// <summary>
    /// Seconds a direction is held before it repeats.
    /// </summary>
    public const float RepeatDelay = 0.35f;

    /// <summary>
    /// Seconds between repeats after the first.
    /// </summary>
    public const float RepeatInterval = 0.12f;

    /// <summary>
    /// A left stick flick counts from this far along its dominant axis.
    /// </summary>
    public const float StickThreshold = 0.5f;

    /// <summary>
    /// Fraction of a slider's width that one left or right press moves it.
    /// </summary>
    public const float SliderNudge = 0.05f;

    /// <summary>
    /// Auto-scroll speed, in mouse wheel notches per second.
    /// </summary>
    public const float AutoScrollSpeed = 8;

    /// <summary>
    /// Auto-scroll stops after this many seconds for one focus, in case the view can't scroll any further.
    /// </summary>
    public const float MaxAutoScrollTime = 0.5f;

    /// <summary>
    /// The highlight outline's colour and width in pixels.
    /// </summary>
    public static readonly Color HighlightColour = Colors.Red;
    public const float HighlightWidth = 3;

    /// <summary>
    /// Pixels a warped cursor stays inside a scroll view's edge, so the scroll wheel reaches the view.
    /// </summary>
    private const float ViewportInset = 2;

    /// <summary>
    /// Pixels a slider click stays inside the slider's edge: Onion's hit test leaves the edges out.
    /// </summary>
    private const float SliderEdgeInset = 0.1f;

    /// <summary>
    /// How far, in pixels, the cursor can be from where this put it and still count as being there (warps land on whole pixels).
    /// </summary>
    private const float CursorTolerance = 2;

    /// <summary>
    /// Tolerance for the repeat timer, so a hold of exactly <see cref="RepeatDelay"/> repeats despite rounding.
    /// </summary>
    private const float TimeEpsilon = 0.0001f;

    private static bool renewedThisFrame, renewedLastFrame;

    /// <summary>
    /// Keeps <see cref="InputSuspended"/> on through the next frame. Something that owns the pad, like a button capture in
    /// the settings, calls it every frame it is active. Once it stops (it finished, or its control died), suspension ends
    /// a frame later, so a pad can't stay locked out of the menus.
    /// </summary>
    public static void RenewSuspension() => renewedThisFrame = true;

    /// <summary>
    /// True when <see cref="RenewSuspension"/> was called during the previous frame or this one. It holds for the whole
    /// frame a capture binds in, so the button it binds can't also act later that frame. While it is true nothing
    /// menu-related runs: navigation, clicks, Escape, back, scroll and both sticks. A direction held when it ends has to be
    /// let go first.
    /// </summary>
    public static bool InputSuspended => renewedLastFrame || renewedThisFrame;

    /// <summary>
    /// True for the rest of the frame in which the pad's B closed a popup (see <see cref="ClosePopup"/>), so the menus don't
    /// also take that B as going back.
    /// </summary>
    public static bool BackConsumedThisFrame { get; private set; }

    /// <summary>
    /// Starts a frame of the <see cref="RenewSuspension"/> lease and of <see cref="BackConsumedThisFrame"/>.
    /// <see cref="ControllerLoopEvent"/> calls it first thing.
    /// </summary>
    internal static void BeginFrame()
    {
        renewedLastFrame = renewedThisFrame;
        renewedThisFrame = false;
        BackConsumedThisFrame = false;
    }

    internal static void ResetSuspensionForTests()
    {
        renewedLastFrame = renewedThisFrame = false;
        BackConsumedThisFrame = false;
    }

    /// <summary>
    /// The focused control's visible rect while the pad is the active device, for the highlight. Null when the keyboard or
    /// mouse is active, nothing is focused, or navigation isn't running.
    /// </summary>
    public static Rect? HighlightRect { get; private set; }

    private readonly IMenuTargets targets;

    private Vector2 heldDirection;
    private float heldTime;
    private float nextRepeat;
    private bool waitForRelease;     // a direction was held while navigation couldn't act: it has to be let go first

    private int? ownFocus;           // the control this last focused
    private int? lastFocus;          // the focused control as of the last frame something was, kept after Onion drops it
    private int? cursorFor;          // the control the cursor was last put on, with
    private Vector2 cursorAt;        // where it was put
    private Vector2 cursorAnchor;    // and where the control's focus point was then
    private float autoScrollTime;
    private int? lastDialogFocus;    // IMenuTargets.DialogFocus as of the last frame navigation ran

    public MenuNavigator(IMenuTargets targets)
    {
        this.targets = targets;
        HighlightRect = null;
    }

    /// <summary>
    /// The focused control: the one Onion has, whether this, a click or the keyboard focused it.
    /// </summary>
    public int? Focused => targets.Focused;

    /// <summary>
    /// Runs one menu frame.
    /// </summary>
    /// <param name="dpadOrStickDir">The D-pad direction, or the left stick when the D-pad is idle. Up-positive.</param>
    /// <param name="dt">Seconds since the last frame, unscaled.</param>
    /// <param name="cursor">The cursor in window coordinates.</param>
    public MenuNavResult Update(Vector2 dpadOrStickDir, float dt, Vector2 cursor)
    {
        var dir = Quantise(dpadOrStickDir);

        if (InputSuspended || !Gamepad.IsActiveDevice)
        {
            if (!Gamepad.IsActiveDevice)
                ReleaseOwnFocus(); // the keyboard or mouse took over
            // Only a direction held now has to be let go. A D-pad press selects the pad, so the press frame is the first
            // one that acts: a latch left over from gameplay or an unfocused window would swallow it.
            waitForRelease = dir != Vector2.Zero;
            if (dir == Vector2.Zero)
                heldDirection = Vector2.Zero;
            HighlightRect = null;
            lastDialogFocus = null; // a dialog still open when the pad is back gets the focus then
            return default;
        }

        Vector2? warpTo = null, clickAt = null;

        // A confirmation dialog that just opened takes the focus, so A answers it rather than clicking what is behind it.
        // The press that brings the pad back to a dialog only does that.
        bool jumped = false;
        var dialogFocus = targets.DialogFocus();
        if (dialogFocus != lastDialogFocus)
        {
            lastDialogFocus = dialogFocus;
            if (dialogFocus is int d && (targets.Focused is not int was || FindLive(was) == null))
            {
                warpTo = FocusOn(d);
                jumped = warpTo != null;
            }
        }

        if (ShouldMove(dir, dt) && !jumped)
        {
            var focused = targets.Focused;
            if (focused is not int id || FindLive(id) is not Rect rect)
            {
                // A focused control that died (Onion drops the focus a frame later) goes back to its nearest live
                // ancestor, so a closed dropdown returns to its header. Otherwise, the control nearest the cursor.
                var lost = focused ?? lastFocus;
                if (lost is int gone && FindLive(gone) == null && targets.LiveAncestorOf(gone) is int ancestor)
                    warpTo = FocusOn(ancestor);
                else if (NearestTo(cursor) is int nearest)
                    warpTo = FocusOn(nearest);
            }
            else if (dir.Y == 0 && targets.IsSlider(id))
                clickAt = cursorAt = Nudge(id, rect, cursor, dir.X);
            else if (targets.FindInDirection(id, dir) is int next && FindLive(next) is Rect)
                warpTo = FocusOn(next);
        }

        float scroll = 0;
        HighlightRect = null;
        if (targets.Focused is int f)
            lastFocus = f;
        if (targets.Focused is int current && FindLive(current) is Rect focusedRect)
        {
            var viewport = targets.ViewportOf(current) ?? focusedRect;
            var point = ControlPoint(focusedRect, viewport);

            if (warpTo == null && clickAt == null && cursorFor == current)
            {
                // The control moved (scrolled, or the layout changed) under a cursor still where this put it: follow it.
                if (Vector2.Distance(point, cursorAnchor) > 0.5f && Vector2.Distance(cursor, cursorAt) <= CursorTolerance)
                    warpTo = cursorAt + (point - cursorAnchor);
            }
            if (warpTo is Vector2 w)
                cursorAt = w;
            if (warpTo != null || clickAt != null)
            {
                if (cursorFor != current)
                    autoScrollTime = 0;
                cursorFor = current;
                cursorAnchor = point;
            }

            // Only for focus this placed: a control clicked with the free cursor stays put under it.
            if (cursorFor == current)
                scroll = AutoScroll(focusedRect, viewport, dt);

            var visible = focusedRect.Intersect(viewport);
            if (visible.Width > 0 && visible.Height > 0)
                HighlightRect = visible;
        }

        return new MenuNavResult { WarpTo = warpTo, ClickAt = clickAt, ScrollDelta = scroll };
    }

    /// <summary>
    /// The right stick moved the cursor to <paramref name="cursor"/>. Once it is off the focused control, focus clears, so
    /// the highlight never shows one control while A would click another. A stick drifting just outside its deadzone
    /// doesn't get that far.
    /// </summary>
    public void FreeCursorMoved(Vector2 cursor)
    {
        if (targets.Focused is int id && FindLive(id) is Rect rect && rect.ContainsPoint(cursor))
            return;
        if (targets.Focused != null)
            targets.Focused = null;
        ownFocus = cursorFor = lastFocus = null;
    }

    /// <summary>
    /// The pad's B, before Onion runs this frame. When a popup (an open dropdown) is open, closes it, focuses the control
    /// that opened it, and sets <see cref="BackConsumedThisFrame"/>, so this B doesn't also leave the menu. Escape would
    /// close the popup too, but the menu's back button and the pause menu would then take that Escape as well.
    /// </summary>
    /// <param name="cursor">Where to put the cursor when a popup was closed: on the header, so A opens it again rather than
    /// clicking what the list covered.</param>
    /// <returns>True when a popup was closed. False when none is open: B goes back as usual.</returns>
    public bool ClosePopup(out Vector2 cursor)
    {
        cursor = default;
        if (targets.ClosePopup() is not (int header, Rect rect))
            return false;

        BackConsumedThisFrame = true;

        // The open dropdown has no hit test rect (Onion takes it away while the list shows), so it isn't live yet and
        // LiveAncestorOf can't find it this frame. It is the header itself, so focus goes straight to it.
        var viewport = targets.ViewportOf(header) ?? rect;
        cursor = cursorAt = FocusOn(header, rect);
        cursorFor = header;
        cursorAnchor = ControlPoint(rect, viewport);

        var visible = rect.Intersect(viewport);
        HighlightRect = visible.Width > 0 && visible.Height > 0 ? visible : null;
        return true;
    }

    /// <summary>
    /// Where this frame's mouse has to be for the triggers' scroll to land, when nothing under <paramref name="cursor"/>
    /// takes the scroll wheel: the centre of the biggest scroll view on screen. On the credits, the only buttons sit
    /// outside the scroll view, and the D-pad always puts the cursor on a button. Null to leave the mouse where it is.
    /// </summary>
    public Vector2? ScrollPointFor(Vector2 cursor)
        => targets.ScrollsAt(cursor) ? null : targets.LargestScrollView()?.GetCenter();

    /// <summary>
    /// Navigation isn't running this frame (gameplay, or the pad is unusable): hides the highlight, and a direction still
    /// held when it runs again has to be let go first.
    /// </summary>
    public void Reset()
    {
        HighlightRect = null;
        waitForRelease = true;
        lastDialogFocus = null;
    }

    /// <summary>
    /// Draws the highlight around <see cref="HighlightRect"/>, over all UI, in screen space.
    /// </summary>
    public static void DrawHighlight()
    {
        if (HighlightRect is not Rect rect)
            return;

        Draw.Reset();
        Draw.ScreenSpace = true;
        Draw.Order = RenderOrders.UserInterfaceTop;
        Draw.Colour = Colors.Transparent;
        Draw.OutlineColour = HighlightColour;
        Draw.OutlineWidth = HighlightWidth;
        Draw.Quad(rect.Expand(HighlightWidth + 1));
        Draw.Reset();
    }

    /// <summary>
    /// Onion's rule for directional focus (<c>Navigator.FindInDirection</c>): of the candidates whose centre lies at least
    /// half a pixel along <paramref name="dir"/> from <paramref name="fromCentre"/>, the nearest. <paramref name="dir"/> is
    /// up-positive and rects are Y down.
    /// </summary>
    public static int? NearestInDirection(IEnumerable<(int Id, Rect Rect)> candidates, int from, Vector2 fromCentre, Vector2 dir)
    {
        if (dir == Vector2.Zero)
            return null;

        var d = Vector2.Normalize(dir);
        d.Y = -d.Y;

        int? best = null;
        float bestDistance = float.MaxValue;
        foreach (var (id, rect) in candidates)
        {
            if (id == from)
                continue;

            var offset = rect.GetCenter() - fromCentre;
            if (Vector2.Dot(offset, d) < 0.5f)
                continue;

            var distance = offset.LengthSquared();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = id;
            }
        }

        return best;
    }

    /// <summary>
    /// The direction as one of the four unit vectors, or zero. The dominant axis wins, vertical on a tie.
    /// </summary>
    private static Vector2 Quantise(Vector2 v)
    {
        float x = MathF.Abs(v.X), y = MathF.Abs(v.Y);
        if (MathF.Max(x, y) < StickThreshold)
            return Vector2.Zero;
        return y >= x ? new Vector2(0, MathF.Sign(v.Y)) : new Vector2(MathF.Sign(v.X), 0);
    }

    /// <summary>
    /// True on the frame a direction is pressed, then after <see cref="RepeatDelay"/> and every <see cref="RepeatInterval"/>.
    /// Once per frame at most, so a slow frame doesn't fire a burst.
    /// </summary>
    private bool ShouldMove(Vector2 dir, float dt)
    {
        if (dir == Vector2.Zero)
        {
            heldDirection = Vector2.Zero;
            waitForRelease = false;
            return false;
        }

        if (waitForRelease)
            return false;

        if (dir != heldDirection)
        {
            heldDirection = dir;
            heldTime = 0;
            nextRepeat = RepeatDelay;
            return true;
        }

        heldTime += dt;
        if (heldTime < nextRepeat - TimeEpsilon)
            return false;

        nextRepeat += RepeatInterval;
        if (nextRepeat <= heldTime)
            nextRepeat = heldTime + RepeatInterval;
        return true;
    }

    private Rect? FindLive(int id)
    {
        foreach (var (liveId, rect) in targets.Live())
            if (liveId == id)
                return rect;
        return null;
    }

    /// <summary>
    /// The live control nearest the cursor, measured to its edge so the control under the cursor wins, then centre to centre.
    /// </summary>
    private int? NearestTo(Vector2 cursor)
    {
        int? best = null;
        float bestEdge = float.MaxValue, bestCentre = float.MaxValue;
        foreach (var (id, rect) in targets.Live())
        {
            var closest = Vector2.Clamp(cursor, new Vector2(rect.MinX, rect.MinY), new Vector2(rect.MaxX, rect.MaxY));
            float edge = Vector2.DistanceSquared(closest, cursor);
            float centre = Vector2.DistanceSquared(rect.GetCenter(), cursor);
            if (edge < bestEdge || (edge == bestEdge && centre < bestCentre))
            {
                best = id;
                bestEdge = edge;
                bestCentre = centre;
            }
        }
        return best;
    }

    /// <summary>
    /// Focuses <paramref name="id"/> and returns where the cursor goes.
    /// </summary>
    private Vector2? FocusOn(int id) => FindLive(id) is Rect rect ? FocusOn(id, rect) : null;

    /// <summary>
    /// Focuses <paramref name="id"/>, whose rect is <paramref name="rect"/>, and returns where the cursor goes.
    /// </summary>
    private Vector2 FocusOn(int id, Rect rect)
    {
        targets.Focused = ownFocus = id;
        targets.Highlight(id);
        autoScrollTime = 0;

        var point = ControlPoint(rect, targets.ViewportOf(id) ?? rect);
        // Onion sets a slider from where a press lands, so on a slider the cursor goes to its value: A there leaves it be.
        if (targets.IsSlider(id) && targets.SliderFraction(id) is float f)
            point.X = SliderX(rect, rect.MinX + f * rect.Width);
        return point;
    }

    /// <summary>
    /// Where a control is, for putting the cursor on it and following it: its centre, kept inside the viewport so the scroll
    /// wheel reaches the scroll view while the control is still scrolled out of view.
    /// </summary>
    private static Vector2 ControlPoint(Rect rect, Rect viewport)
    {
        var min = new Vector2(viewport.MinX + ViewportInset, viewport.MinY + ViewportInset);
        var max = new Vector2(viewport.MaxX - ViewportInset, viewport.MaxY - ViewportInset);
        return Vector2.Clamp(rect.GetCenter(), min, Vector2.Max(min, max));
    }

    /// <summary>
    /// Where to click to move the slider 5% of its width in <paramref name="sign"/>: from its value when it can be read,
    /// else from the cursor, kept inside the slider.
    /// </summary>
    private Vector2 Nudge(int id, Rect rect, Vector2 cursor, float sign)
    {
        float from = targets.SliderFraction(id) is float f ? rect.MinX + f * rect.Width : cursor.X;
        return new Vector2(
            SliderX(rect, from + sign * SliderNudge * rect.Width),
            Math.Clamp(cursor.Y, rect.MinY + SliderEdgeInset, rect.MaxY - SliderEdgeInset));
    }

    /// <summary>
    /// <paramref name="x"/> on a whole pixel inside the slider. Warps land on whole pixels and the cursor then sits there,
    /// so a later A press reads the same value. At the right end it stays just inside Onion's hit test, so a nudge (which
    /// presses at the exact point) still reaches the maximum.
    /// </summary>
    private static float SliderX(Rect rect, float x)
    {
        float lo = MathF.Floor(rect.MinX) + 1, hi = rect.MaxX - SliderEdgeInset;
        return hi < lo ? rect.GetCenter().X : Math.Clamp(MathF.Round(x), lo, hi);
    }

    /// <summary>
    /// Scroll toward a focused control that sticks out of its viewport: up when it sticks out above, down when below, and for
    /// one taller than the view, until its top shows. Gives up after <see cref="MaxAutoScrollTime"/> for one focus.
    /// </summary>
    private float AutoScroll(Rect rect, Rect viewport, float dt)
    {
        const float tolerance = 1;
        float above = viewport.MinY - rect.MinY; // > 0: sticks out above
        float below = rect.MaxY - viewport.MaxY; // > 0: sticks out below

        float direction = 0;
        if (above > tolerance)
            direction = 1;
        else if (below > tolerance && above < -tolerance)
            direction = -1;

        if (direction == 0 || autoScrollTime >= MaxAutoScrollTime)
            return 0;

        autoScrollTime += dt;
        return direction * AutoScrollSpeed * dt;
    }

    /// <summary>
    /// Clears the focus this set, if nothing else has changed it since, so mouse and keyboard menus look as they did.
    /// </summary>
    private void ReleaseOwnFocus()
    {
        if (ownFocus != null && targets.Focused == ownFocus)
            targets.Focused = null;
        ownFocus = cursorFor = lastFocus = null;
    }
}
