using System;
namespace MIR;

public enum InputDevice
{
    KeyboardMouse,
    Gamepad
}

/// <summary>
/// Static facade over the controller. Call <see cref="Update"/> once per frame.
/// </summary>
public static class Gamepad
{
    public static IGamepadSource Source { get; set; } = new XInputGamepadSource();
    public static bool Enabled = true;
    public static bool VibrationEnabled = true;

    public static GamepadState Current { get; private set; } = GamepadState.Disconnected;
    public static GamepadState Previous { get; private set; } = GamepadState.Disconnected;
    public static bool IsConnected => Current.Connected;
    public static InputDevice ActiveDevice { get; set; } = InputDevice.KeyboardMouse;
    public static bool IsActiveDevice => ActiveDevice == InputDevice.Gamepad;
    public static bool HadActivityThisFrame { get; private set; }

    /// <summary>
    /// Whether pad buttons may trigger gameplay actions through <see cref="ControlScheme"/> this frame.
    /// <see cref="ControllerLoopEvent"/> sets it at frame start: true only in gameplay. A button a menu
    /// consumed (B or Start closing the pause menu, A clicking Resume) then can't also fire Melee or JumpDodge when the
    /// menu closes partway through the frame.
    /// </summary>
    public static bool GameplayInputAllowed { get; set; } = true;

    private static float rumbleLow, rumbleHigh, rumbleTimeLeft;
    private static float sentLow, sentHigh;

    public static void Update(float dt, bool hasFocus = true)
    {
        Previous = Current;

        if (Enabled && hasFocus && Source.TryGetState(out var raw))
            Current = GamepadState.FromRaw(raw);
        else
            Current = GamepadState.Disconnected;

        HadActivityThisFrame = DetectActivity(Previous, Current);
        if (HadActivityThisFrame)
            ActiveDevice = InputDevice.Gamepad;

        UpdateRumble(dt, hasFocus);
    }

    public static bool Held(GamepadButton b) => Current.IsDown(b);
    public static bool Pressed(GamepadButton b) => Current.IsDown(b) && !Previous.IsDown(b);
    public static bool Released(GamepadButton b) => !Current.IsDown(b) && Previous.IsDown(b);

    /// <summary>
    /// Requests vibration. While a request is pending the strongest value of each motor wins.
    /// </summary>
    public static void Rumble(float low, float high, float seconds)
    {
        if (!VibrationEnabled || seconds <= 0)
            return;

        if (rumbleTimeLeft <= 0)
        {
            rumbleLow = low;
            rumbleHigh = high;
        }
        else
        {
            rumbleLow = Math.Max(rumbleLow, low);
            rumbleHigh = Math.Max(rumbleHigh, high);
        }
        rumbleTimeLeft = Math.Max(rumbleTimeLeft, seconds);
    }

    /// <summary>
    /// Cancels any pending vibration and turns running motors off right away, without waiting for the next <see cref="Update"/>
    /// (there is none on shutdown).
    /// </summary>
    public static void StopRumble()
    {
        rumbleLow = rumbleHigh = rumbleTimeLeft = 0;
        if (sentLow != 0 || sentHigh != 0)
        {
            sentLow = sentHigh = 0;
            Source.SetVibration(0, 0);
        }
    }

    public static void ResetForTests(IGamepadSource source)
    {
        Source = source;
        Enabled = true;
        VibrationEnabled = true;
        Current = GamepadState.Disconnected;
        Previous = GamepadState.Disconnected;
        ActiveDevice = InputDevice.KeyboardMouse;
        HadActivityThisFrame = false;
        GameplayInputAllowed = true;
        rumbleLow = rumbleHigh = rumbleTimeLeft = 0;
        sentLow = sentHigh = 0;
    }

    private static void UpdateRumble(float dt, bool hasFocus)
    {
        float low = 0, high = 0;
        if (rumbleTimeLeft > 0)
        {
            rumbleTimeLeft -= dt;
            if (rumbleTimeLeft <= 0)
                StopRumble();
            else if (VibrationEnabled && Enabled && hasFocus)
            {
                low = rumbleLow;
                high = rumbleHigh;
            }
        }

        if (low != sentLow || high != sentHigh)
        {
            sentLow = low;
            sentHigh = high;
            Source.SetVibration(low, high);
        }
    }

    /// <summary>
    /// A stick's magnitude has to cross this, from below, to count as activity.
    /// </summary>
    public const float AnalogActivityThreshold = 0.25f;

    /// <summary>
    /// True when a button or trigger went down this frame, or a stick crossed <see cref="AnalogActivityThreshold"/> from below,
    /// so a worn stick or a resting thumb wobbling just outside the deadzone doesn't steal the active device. A trigger counts
    /// as soon as it is down, the same as a button: <see cref="GamepadState"/>'s trigger threshold already filters resting
    /// noise, and any lighter rule would let a light pull fire Attack or BlockAim while the mouse still aims. Never true on
    /// the frame the pad (re)connects: hot-plug, focus regain and re-enabling would otherwise count a held button as activity.
    /// </summary>
    private static bool DetectActivity(in GamepadState prev, in GamepadState cur)
    {
        if (!cur.Connected || !prev.Connected)
            return false;

        return (cur.Buttons & ~prev.Buttons) != 0
            || WentDown(prev, cur, GamepadButton.LeftTrigger)
            || WentDown(prev, cur, GamepadButton.RightTrigger)
            || CrossedUp(prev.LeftStick.Length(), cur.LeftStick.Length())
            || CrossedUp(prev.RightStick.Length(), cur.RightStick.Length());
    }

    private static bool WentDown(in GamepadState prev, in GamepadState cur, GamepadButton b)
        => cur.IsDown(b) && !prev.IsDown(b);

    private static bool CrossedUp(float previous, float current)
        => previous < AnalogActivityThreshold && current >= AnalogActivityThreshold;
}
