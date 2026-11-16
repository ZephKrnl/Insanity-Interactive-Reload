using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Walgelijk;

namespace MIR;

/// <summary>
/// Turns controller input into the mouse and keyboard input that the game and its UI already read.
/// Add it to <see cref="Game.AdditionalLoopEvents"/> so it runs before the scene systems every frame.
/// <list type="bullet">
/// <item>Picks the active device: pad activity selects the pad, keyboard or mouse use selects keyboard and mouse.</item>
/// <item>In gameplay the right stick aims. The mouse position is overridden for this frame only, so the real cursor stays put.</item>
/// <item>In menus the D-pad and left stick move a highlighted focus between controls (see <see cref="MenuNavigator"/>), the
/// right stick moves the OS cursor freely, A clicks, B presses Escape (or only closes an open dropdown), and LT and RT scroll.</item>
/// <item>Start presses Escape everywhere.</item>
/// <item>Nothing menu-related happens while <see cref="MenuNavigator.InputSuspended"/> is true.</item>
/// <item>The pad vibrates when the player loses health or dies.</item>
/// </list>
/// </summary>
public sealed class ControllerLoopEvent : IGameLoopEvent
{
    /// <summary>
    /// Menu cursor speed in pixels per second at full deflection, for a window 1080 pixels tall.
    /// </summary>
    public const float MenuCursorSpeed = 1600;

    /// <summary>
    /// The right stick changes the aim direction only when it is deflected further than this.
    /// </summary>
    public const float AimStickThreshold = 0.25f;

    /// <summary>
    /// Scroll per second with a trigger fully pulled in menus.
    /// </summary>
    public const float ScrollSpeed = 12;

    /// <summary>
    /// How far, in pixels, the mouse has to be from its anchor (where it was when the pad took over, where our last warp
    /// left it, or where it was when this last detected the mouse moving) to count as the player using it. Measured from
    /// the anchor rather than per frame, so slow movement counts at any frame rate.
    /// </summary>
    public const float MouseMoveThreshold = 4;

    /// <summary>
    /// How far, in pixels on each axis, the OS cursor can be from where the last warp should have landed it and still
    /// count as being there.
    /// </summary>
    private const float WarpLandingTolerance = 1;

    /// <summary>
    /// Distance from the player to the aim point, before character scale. Set by <see cref="Settings.Apply(Game)"/>.
    /// </summary>
    public static float AimDistance = 500;

    /// <summary>
    /// Multiplier for <see cref="MenuCursorSpeed"/>. Set by <see cref="Settings.Apply(Game)"/>.
    /// </summary>
    public static float CursorSpeed = 1;

    /// <summary>
    /// Unit vector the player aims along while the pad is the active device. It keeps its value when the right stick is let go.
    /// When pad aim (re)starts it starts from the character's current aim, or for a fresh spawn from the pad's last direction.
    /// </summary>
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;

    /// <summary>
    /// The menu cursor position in window coordinates.
    /// </summary>
    public Vector2 VirtualCursor { get; private set; }

    private readonly ICursorWarper warper;
    private readonly MenuNavigator navigator;

    private bool injectedLeftHold;              // this class put Left in MouseButtonsHeld
    private bool menuClickDown;                 // A sent a Left down in a menu and the matching up is still owed
    private bool wasGameplay;
    private bool cursorDriving;                 // the right stick moved VirtualCursor last frame
    private CharacterComponent? aimCharacter;   // the player that AimDirection belongs to, null when not aiming
    private CharacterComponent? lastAimCharacter; // the player that AimDirection last belonged to, kept while not aiming
    private Vector2? mouseAnchor;               // where the mouse was last accounted for, see MouseMoveThreshold
    private bool warpedLastFrame;               // this frame's fresh mouse is the OS echo of our warp
    private Vector2? warpLanding;               // where our last warp put the OS cursor, null since VirtualCursor last followed it
    private CharacterComponent? healthCharacter; // the player that trackedHealth and trackedAlive belong to
    private float trackedHealth;
    private bool trackedAlive;

    /// <param name="warper">Moves the OS cursor in menus.</param>
    /// <param name="menuTargets">What menu navigation moves between, the Onion UI when null.</param>
    public ControllerLoopEvent(ICursorWarper warper, IMenuTargets? menuTargets = null)
    {
        this.warper = warper;
        navigator = new MenuNavigator(menuTargets ?? new OnionMenuTargets());
    }

    /// <summary>
    /// True when the player is controlling a living character in a game scene: the player responds to user input, not paused,
    /// no cutscene, console closed and not editing in experiment mode.
    /// </summary>
    public static bool IsGameplay(Scene scene) => TryGetGameplayPlayer(scene, out _);

    public void Update(Game game, float dt)
    {
        var window = game.Window;
        ref var input = ref game.State.Input;

        // Latches MenuNavigator.InputSuspended for the frame: on when a capture renewed it during the last frame.
        MenuNavigator.BeginFrame();

        // Fresh values from the window, read before anything below overrides or injects input.
        var freshMouse = input.WindowMousePosition;
        bool padWasActive = Gamepad.IsActiveDevice;
        bool mouseMoved = UserMovedMouse(freshMouse); // always called: it keeps the anchor up to date
        bool keyboardMouseUsed = mouseMoved || input.AnyKey || input.AnyMouseButton || input.MouseScrollDelta != 0;

        // Damage from last frame's systems. Before Gamepad.Update, so the vibration reaches the motors this frame.
        TrackPlayerHealth(game.Scene);

        Gamepad.Update(dt, window.HasFocus);

        // Gamepad.Update already selected the pad if it had activity. Keyboard and mouse use wins over that.
        if (keyboardMouseUsed)
            Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        else if (Gamepad.IsActiveDevice && !padWasActive)
            mouseAnchor = freshMouse; // the pad took over: mouse use is measured from here

        var scene = game.Scene;
        CharacterComponent? player = null;
        bool gameplay = scene != null && TryGetGameplayPlayer(scene, out player);
        bool enteredGameplay = gameplay && !wasGameplay;
        wasGameplay = gameplay;

        // Decided once, here: systems later this frame may unpause, but a pad button the menu used must stay out of gameplay.
        // Only the mode counts. An unplugged, disabled or unfocused pad already reads as disconnected, so nothing is held,
        // and the frame it goes still has to report the release edges that end held abilities.
        Gamepad.GameplayInputAllowed = gameplay;

        // A click held from a menu must not leak into gameplay or outlive the pad.
        if (injectedLeftHold && (!Gamepad.Held(GamepadButton.A) || !Gamepad.IsConnected || !Gamepad.Enabled || enteredGameplay))
            ReleaseInjectedHold(ref input);
        if (enteredGameplay)
            menuClickDown = false;

        if (!Gamepad.IsConnected || !Gamepad.Enabled || !window.HasFocus)
        {
            cursorDriving = false;
            aimCharacter = null;
            navigator.Reset();
            return;
        }

        // The input sets are null in a default InputState (before the window's first loop cycle), hence the ?. below.
        if (Gamepad.Pressed(GamepadButton.Start) && (gameplay || !MenuNavigator.InputSuspended))
            input.KeysDown?.Add(Key.Escape);

        if (gameplay)
        {
            navigator.Reset();
            cursorDriving = false;
            if (Gamepad.IsActiveDevice)
                Aim(player!, window, ref input);
            else
                aimCharacter = null;
        }
        else
        {
            aimCharacter = null;
            DriveMenu(window, ref input, freshMouse, keyboardMouseUsed, dt);
        }
    }

    public void FixedUpdate(Game game, float interval)
    {
    }

    /// <remarks>
    /// The main, campaign and arena menus and character customisation spawn a living player for display. They are menus
    /// because they set <see cref="PlayerComponent.RespondToUserInput"/> to false and, like every
    /// <see cref="SceneUtils.PrepareMenuScene"/> scene, have no <see cref="PauseComponent"/>, which only game scenes get.
    /// </remarks>
    private static bool TryGetGameplayPlayer(Scene scene, [NotNullWhen(true)] out CharacterComponent? character)
    {
        if (MadnessUtils.FindPlayer(scene, out var player, out character)
            && player.RespondToUserInput
            && scene.FindAnyComponent<PauseComponent>(out _)
            && character.IsAlive
            && !MadnessUtils.IsPaused(scene)
            && !MadnessUtils.IsCutscenePlaying(scene)
            && !scene.Game.Console.IsActive
            && !MadnessUtils.EditingInExperimentMode(scene))
            return true;

        character = null;
        return false;
    }

    /// <summary>
    /// Rumbles when the player's head plus body health dropped since the last frame, or the player died. A different player
    /// (a new level or a respawn) or no player only resets the tracking, so a lower starting health isn't read as a hit.
    /// </summary>
    private void TrackPlayerHealth(Scene? scene)
    {
        if (scene == null || !MadnessUtils.FindPlayer(scene, out _, out var character))
        {
            healthCharacter = null;
            return;
        }

        float health = 0;
        if (scene.TryGetComponentFrom<BodyPartComponent>(character.Positioning.Head.Entity, out var head))
            health += head.Health;
        if (scene.TryGetComponentFrom<BodyPartComponent>(character.Positioning.Body.Entity, out var body))
            health += body.Health;
        bool alive = character.IsAlive;

        if (ReferenceEquals(character, healthCharacter) && Gamepad.IsActiveDevice)
        {
            if (trackedAlive && !alive)
                ControllerRumble.Death();
            else if (alive && health < trackedHealth)
                ControllerRumble.Hit();
        }

        healthCharacter = character;
        trackedHealth = health;
        trackedAlive = alive;
    }

    /// <summary>
    /// True when the window mouse is more than <see cref="MouseMoveThreshold"/> from its anchor, which then moves to it.
    /// The frame after a warp only re-anchors to where the OS put the cursor: in fullscreen and borderless modes the engine
    /// confines the cursor, so a warp near the right or bottom edge can land short of its target, and that is not the user.
    /// </summary>
    private bool UserMovedMouse(Vector2 freshMouse)
    {
        if (warpedLastFrame || mouseAnchor is not Vector2 anchor)
        {
            warpedLastFrame = false;
            mouseAnchor = freshMouse;
            return false;
        }

        if (Vector2.Distance(freshMouse, anchor) <= MouseMoveThreshold)
            return false;

        mouseAnchor = freshMouse;
        return true;
    }

    private void Aim(CharacterComponent character, Window window, ref InputState input)
    {
        var positioning = character.Positioning;
        if (!ReferenceEquals(aimCharacter, character))
        {
            AimDirection = StartingAimDirection(character);
            aimCharacter = lastAimCharacter = character;
        }

        var stick = Gamepad.Current.RightStick;
        if (stick.Length() > AimStickThreshold)
            AimDirection = Vector2.Normalize(stick);

        var aim = positioning.GlobalCenter + AimDirection * AimDistance * positioning.Scale;
        input.WorldMousePosition = aim;
        input.WindowMousePosition = window.WorldToWindowPoint(aim);
    }

    /// <summary>
    /// Where pad aim (re)starts. Once anything has aimed the character, from its current aim. That carries on from where the
    /// pad left it after a pause or cutscene (<see cref="PlayerCharacterSystem"/> returns early then) or with the console
    /// open (the engine doesn't update the scene's systems at all then), and from where the mouse aimed after the mouse was
    /// used or the window was unfocused. A fresh spawn that nothing has aimed yet (a new level, a door transition or a
    /// restart) still has its aim target at the world origin. It keeps the pad's direction instead, with X turned the way
    /// the new character faces, or aims horizontally the way it faces when the pad hasn't aimed before.
    /// </summary>
    private Vector2 StartingAimDirection(CharacterComponent character)
    {
        var positioning = character.Positioning;
        var offset = character.AimTargetPosition - positioning.GlobalCenter;
        if (character.AimTargetPosition != Vector2.Zero && float.IsFinite(offset.X) && float.IsFinite(offset.Y) && offset.LengthSquared() > 1)
            return Vector2.Normalize(offset);

        if (ReferenceEquals(character, lastAimCharacter))
            return AimDirection; // the pad aimed this character before, but nothing has set its aim target yet
        if (lastAimCharacter != null)
            return new Vector2(MathF.Abs(AimDirection.X) * positioning.FlipScaling, AimDirection.Y);
        return new Vector2(positioning.FlipScaling, 0);
    }

    private void DriveMenu(Window window, ref InputState input, Vector2 freshMouse, bool keyboardMouseUsed, float dt)
    {
        if (MenuNavigator.InputSuspended)
        {
            // Something else owns the pad. The navigator still sees the direction, so one held now isn't a press later.
            VirtualCursor = freshMouse;
            warpLanding = null;
            cursorDriving = false;
            menuClickDown = false; // a hold this put in is still released when A is (see Update)
            navigator.Update(NavigationInput(), dt, freshMouse);
            return;
        }

        DriveFreeCursor(window, ref input, freshMouse, keyboardMouseUsed, dt);

        var nav = navigator.Update(NavigationInput(), dt, VirtualCursor);
        if (nav.WarpTo is Vector2 warp)
            MoveCursor(window, ref input, warp);
        if (nav.ClickAt is Vector2 click)
        {
            // A slider nudge: Onion's slider takes its value from where the press lands. Update releases it next frame.
            MoveCursor(window, ref input, click);
            PressLeft(ref input);
        }

        // B on an open dropdown only closes it. Otherwise B is Escape, which goes back.
        if (Gamepad.Pressed(GamepadButton.B))
        {
            if (navigator.ClosePopup(out var header))
                MoveCursor(window, ref input, header);
            else
                input.KeysDown?.Add(Key.Escape);
        }

        MenuNavigator.DrawHighlight();

        if (Gamepad.Pressed(GamepadButton.A))
        {
            PressLeft(ref input);
            menuClickDown = true;
        }
        else if (Gamepad.Released(GamepadButton.A) && menuClickDown)
        {
            input.MouseButtonsUp?.Add(MouseButton.Left);
            ReleaseInjectedHold(ref input);
            menuClickDown = false;
        }

        // LT scrolls up, the way the mouse wheel's up is positive.
        var pad = Gamepad.Current;
        float triggerScroll = (pad.LeftTrigger - pad.RightTrigger) * ScrollSpeed * dt;
        input.MouseScrollDelta += triggerScroll + nav.ScrollDelta;

        // Onion scrolls what is under the mouse. With nothing there that scrolls, the triggers scroll the biggest view on
        // screen instead: this frame's mouse goes to it, the way Aim overrides it, so the OS cursor and the focus stay put.
        // Not while a mouse button is down, held or let go this frame, so no click or drag lands there.
        if (triggerScroll != 0 && Gamepad.IsActiveDevice && MouseButtonsIdle(input)
            && navigator.ScrollPointFor(input.WindowMousePosition) is Vector2 at)
        {
            input.WindowMousePosition = at;
            input.WorldMousePosition = window.WindowToWorldPoint(at);
        }
    }

    private static bool MouseButtonsIdle(in InputState input)
        => input.MouseButtonsDown is not { Count: > 0 }
        && input.MouseButtonsHeld is not { Count: > 0 }
        && input.MouseButtonsUp is not { Count: > 0 };

    /// <summary>
    /// The right stick moves the OS cursor freely, for screens that aren't grids of controls.
    /// </summary>
    private void DriveFreeCursor(Window window, ref InputState input, Vector2 freshMouse, bool keyboardMouseUsed, float dt)
    {
        var stick = Gamepad.Current.RightStick;

        // VirtualCursor keeps the sub-pixel part of the stick's movement from frame to frame, but only while the OS cursor
        // is where the last warp put it. Otherwise the real cursor wins: the stick is at rest, the keyboard or mouse was
        // used, or the cursor isn't there because the user moved the mouse right after a warp (which UserMovedMouse can't
        // tell from a clamped echo) or because the OS clamped the warp.
        if (stick == Vector2.Zero || !cursorDriving || keyboardMouseUsed || !IsCursorWhereWarped(freshMouse))
        {
            VirtualCursor = freshMouse;
            warpLanding = null;
        }

        cursorDriving = stick != Vector2.Zero;

        // The frame the keyboard or mouse is used, the cursor stays where the user put it.
        if (!cursorDriving || keyboardMouseUsed)
            return;

        var velocity = stick * stick.Length() * MenuCursorSpeed * CursorSpeed * (window.Height / 1080f);
        velocity.Y = -velocity.Y; // the stick is up-positive, window Y points down

        var max = Vector2.Max(window.Size - Vector2.One, Vector2.Zero);
        var next = Vector2.Clamp(VirtualCursor + velocity * dt, Vector2.Zero, max);
        if (next != VirtualCursor)
        {
            MoveCursor(window, ref input, next);
            navigator.FreeCursorMoved(next);
        }
        else
        {
            input.WindowMousePosition = VirtualCursor;
            input.WorldMousePosition = window.WindowToWorldPoint(VirtualCursor);
        }
    }

    /// <summary>
    /// Warps the OS cursor to <paramref name="target"/>, kept inside the window, and points this frame's mouse there.
    /// </summary>
    private void MoveCursor(Window window, ref InputState input, Vector2 target)
    {
        var max = Vector2.Max(window.Size - Vector2.One, Vector2.Zero);
        target = Vector2.Clamp(target, Vector2.Zero, max);

        VirtualCursor = target;
        warper.Warp(window, target);
        warpLanding = WarpLanding(target);
        warpedLastFrame = true;

        input.WindowMousePosition = target;
        input.WorldMousePosition = window.WindowToWorldPoint(target);
    }

    /// <summary>
    /// Sends a Left press. A hold this adds is released when A isn't held (see <see cref="Update"/>).
    /// </summary>
    private void PressLeft(ref InputState input)
    {
        input.MouseButtonsDown?.Add(MouseButton.Left);
        if (input.MouseButtonsHeld?.Add(MouseButton.Left) == true)
            injectedLeftHold = true;
    }

    /// <summary>
    /// The D-pad's direction, or the left stick's while the D-pad is idle. Up-positive.
    /// </summary>
    private static Vector2 NavigationInput()
    {
        var d = Vector2.Zero;
        if (Gamepad.Held(GamepadButton.DPadUp))
            d.Y += 1;
        if (Gamepad.Held(GamepadButton.DPadDown))
            d.Y -= 1;
        if (Gamepad.Held(GamepadButton.DPadLeft))
            d.X -= 1;
        if (Gamepad.Held(GamepadButton.DPadRight))
            d.X += 1;
        return d != Vector2.Zero ? d : Gamepad.Current.LeftStick;
    }

    /// <summary>
    /// Where a warp to <paramref name="target"/> puts the OS cursor: the engine's <c>WindowToScreenPoint</c> truncates X and Y
    /// to whole pixels before the warper sees them, and <paramref name="target"/> is never negative.
    /// </summary>
    private static Vector2 WarpLanding(Vector2 target) => new(MathF.Truncate(target.X), MathF.Truncate(target.Y));

    /// <summary>
    /// True when the OS cursor is where the last warp put it, within <see cref="WarpLandingTolerance"/> on each axis.
    /// </summary>
    private bool IsCursorWhereWarped(Vector2 freshMouse)
        => warpLanding is Vector2 landed
            && MathF.Abs(freshMouse.X - landed.X) <= WarpLandingTolerance
            && MathF.Abs(freshMouse.Y - landed.Y) <= WarpLandingTolerance;

    /// <summary>
    /// Removes the Left hold this class added, and only that one. A real mouse hold is never touched.
    /// </summary>
    private void ReleaseInjectedHold(ref InputState input)
    {
        if (!injectedLeftHold)
            return;

        input.MouseButtonsHeld?.Remove(MouseButton.Left);
        input.MouseButtonsUp?.Add(MouseButton.Left);
        injectedLeftHold = false;
    }
}
