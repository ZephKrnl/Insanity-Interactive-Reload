using System;

namespace MIR;

public enum GamepadCaptureResult
{
    /// <summary>
    /// Still waiting for a button.
    /// </summary>
    Waiting,

    /// <summary>
    /// A button went down: <see cref="GamepadCapture.BoundButton"/>.
    /// </summary>
    Bound,

    /// <summary>
    /// Back was pressed, the pad went away, or the capture isn't running.
    /// </summary>
    Cancelled
}

/// <summary>
/// Waits for the next pad button to bind to an action, as the settings' rebind control does. While it waits, it renews
/// <see cref="MenuNavigator.RenewSuspension"/> so the menus leave the pad alone. Once it binds or cancels it stops, and the
/// menus get the pad back a frame later.
/// <list type="bullet">
/// <item>Each button arms on its own: it counts only once it has been seen up since <see cref="Begin"/>. So the A that clicked
/// the rebind button, or a trigger or stick still held, can't bind itself, and doesn't hold up any other button.</item>
/// <item>Back cancels on its own press, whatever else is held. A Back held when the capture began has to be let go first.</item>
/// <item>Otherwise the first armed bindable button down binds. The stick directions and triggers count, the way
/// <see cref="GamepadState.IsDown"/> reads them.</item>
/// <item>No pad (unplugged, disabled, or the window lost focus) cancels: there is nothing to bind from.</item>
/// <item>Start and Back can't be bound: they pause and cancel.</item>
/// </list>
/// </summary>
public sealed class GamepadCapture
{
    private static readonly GamepadButton[] buttons = Enum.GetValues<GamepadButton>();

    /// <summary>
    /// True from <see cref="Begin"/> until it binds or is cancelled.
    /// </summary>
    public bool Active { get; private set; }

    /// <summary>
    /// The button it bound, once <see cref="Update"/> returns <see cref="GamepadCaptureResult.Bound"/>. <see cref="GamepadButton.None"/> otherwise.
    /// </summary>
    public GamepadButton BoundButton { get; private set; }

    // Per button, indexed by its enum value: seen up since Begin, so going down now is a fresh press.
    private readonly bool[] armed = new bool[MaxButton() + 1];

    /// <summary>
    /// True for the buttons an action can be bound to: all but <see cref="GamepadButton.None"/>, Start and Back.
    /// </summary>
    public static bool IsBindable(GamepadButton b) => b is not (GamepadButton.None or GamepadButton.Start or GamepadButton.Back);

    /// <summary>
    /// Starts waiting for a button. The menus stop using the pad from now on. Nothing is armed yet: the first
    /// <see cref="Update"/> sees what is still held.
    /// </summary>
    public void Begin()
    {
        Active = true;
        Array.Clear(armed);
        BoundButton = GamepadButton.None;
        MenuNavigator.RenewSuspension();
    }

    /// <summary>
    /// Runs one frame of the capture on the pad's state this frame. Call it every frame while <see cref="Active"/>: each call
    /// that keeps waiting renews the menus' suspension, so a capture nobody updates any more ends on its own.
    /// </summary>
    public GamepadCaptureResult Update(in GamepadState state)
    {
        if (!Active)
            return GamepadCaptureResult.Cancelled;

        if (!state.Connected || Pressed(state, GamepadButton.Back))
        {
            Cancel();
            return GamepadCaptureResult.Cancelled;
        }

        foreach (var b in buttons)
            if (IsBindable(b) && Pressed(state, b))
            {
                Active = false;
                BoundButton = b;
                return GamepadCaptureResult.Bound;
            }

        // Whatever is up now is armed from the next frame.
        foreach (var b in buttons)
            if (!state.IsDown(b))
                armed[(int)b] = true;

        MenuNavigator.RenewSuspension();
        return GamepadCaptureResult.Waiting;
    }

    /// <summary>
    /// Stops waiting without binding. The menus get the pad back a frame after the last renewal.
    /// </summary>
    public void Cancel()
    {
        Active = false;
        Array.Clear(armed);
    }

    /// <summary>
    /// True when <paramref name="b"/> is down and was seen up since <see cref="Begin"/>.
    /// </summary>
    private bool Pressed(in GamepadState state, GamepadButton b) => armed[(int)b] && state.IsDown(b);

    private static int MaxButton()
    {
        int max = 0;
        foreach (var b in buttons)
            max = Math.Max(max, (int)b);
        return max;
    }
}
