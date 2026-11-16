using System;
using System.Numerics;

namespace MIR;

/// <summary>
/// Processed controller state: deadzones applied, sticks and triggers normalised, stick Y is up-positive.
/// </summary>
public readonly struct GamepadState
{
    public const float StickDirectionThreshold = 0.35f;

    private const short LeftDeadzone = 7849;
    private const short RightDeadzone = 8689;
    private const byte TriggerThreshold = 30;

    private const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
    private const ushort Start = 0x0010, Back = 0x0020, LeftStickClick = 0x0040, RightStickClick = 0x0080;
    private const ushort LeftBumper = 0x0100, RightBumper = 0x0200;
    private const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    public static readonly GamepadState Disconnected = default;

    public readonly bool Connected;
    public readonly ushort Buttons;
    public readonly Vector2 LeftStick;
    public readonly Vector2 RightStick;
    public readonly float LeftTrigger;
    public readonly float RightTrigger;

    public GamepadState(bool connected, ushort buttons, Vector2 leftStick, Vector2 rightStick, float leftTrigger, float rightTrigger)
    {
        Connected = connected;
        Buttons = buttons;
        LeftStick = leftStick;
        RightStick = rightStick;
        LeftTrigger = leftTrigger;
        RightTrigger = rightTrigger;
    }

    public static GamepadState FromRaw(in RawGamepadState raw) => new(
        true,
        raw.Buttons,
        ApplyRadialDeadzone(raw.ThumbLX, raw.ThumbLY, LeftDeadzone),
        ApplyRadialDeadzone(raw.ThumbRX, raw.ThumbRY, RightDeadzone),
        ApplyTriggerThreshold(raw.LeftTrigger),
        ApplyTriggerThreshold(raw.RightTrigger));

    /// <summary>
    /// Zeroes the stick inside the deadzone and rescales the magnitude outside it to 0..1. Output Y is up-positive.
    /// </summary>
    public static Vector2 ApplyRadialDeadzone(short x, short y, short deadzone)
    {
        var v = new Vector2(x, y);
        var len = v.Length();
        if (len <= deadzone)
            return Vector2.Zero;

        var scaled = Math.Min((len - deadzone) / (32767f - deadzone), 1f);
        return v / len * scaled;
    }

    public static float ApplyTriggerThreshold(byte v)
        => v <= TriggerThreshold ? 0 : (v - TriggerThreshold) / (255f - TriggerThreshold);

    public bool IsDown(GamepadButton b)
    {
        if (!Connected)
            return false;

        return b switch
        {
            GamepadButton.A => (Buttons & A) != 0,
            GamepadButton.B => (Buttons & B) != 0,
            GamepadButton.X => (Buttons & X) != 0,
            GamepadButton.Y => (Buttons & Y) != 0,
            GamepadButton.LeftBumper => (Buttons & LeftBumper) != 0,
            GamepadButton.RightBumper => (Buttons & RightBumper) != 0,
            GamepadButton.Back => (Buttons & Back) != 0,
            GamepadButton.Start => (Buttons & Start) != 0,
            GamepadButton.LeftStickClick => (Buttons & LeftStickClick) != 0,
            GamepadButton.RightStickClick => (Buttons & RightStickClick) != 0,
            GamepadButton.DPadUp => (Buttons & DPadUp) != 0,
            GamepadButton.DPadDown => (Buttons & DPadDown) != 0,
            GamepadButton.DPadLeft => (Buttons & DPadLeft) != 0,
            GamepadButton.DPadRight => (Buttons & DPadRight) != 0,
            GamepadButton.LeftTrigger => LeftTrigger > 0,
            GamepadButton.RightTrigger => RightTrigger > 0,
            GamepadButton.LeftStickLeft => LeftStick.X < -StickDirectionThreshold,
            GamepadButton.LeftStickRight => LeftStick.X > StickDirectionThreshold,
            GamepadButton.LeftStickUp => LeftStick.Y > StickDirectionThreshold,
            GamepadButton.LeftStickDown => LeftStick.Y < -StickDirectionThreshold,
            GamepadButton.RightStickLeft => RightStick.X < -StickDirectionThreshold,
            GamepadButton.RightStickRight => RightStick.X > StickDirectionThreshold,
            GamepadButton.RightStickUp => RightStick.Y > StickDirectionThreshold,
            GamepadButton.RightStickDown => RightStick.Y < -StickDirectionThreshold,
            _ => false
        };
    }
}
