using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MIR;

[JsonConverter(typeof(StringEnumConverter))]
public enum GamepadButton
{
    None,
    A, B, X, Y,
    LeftBumper, RightBumper,
    Back, Start,
    LeftStickClick, RightStickClick,
    DPadUp, DPadDown, DPadLeft, DPadRight,
    LeftTrigger, RightTrigger,
    LeftStickLeft, LeftStickRight, LeftStickUp, LeftStickDown,
    RightStickLeft, RightStickRight, RightStickUp, RightStickDown
}

public static class GamepadButtonExtensions
{
    public static string ToDisplayString(this GamepadButton b) => b switch
    {
        GamepadButton.A => "A",
        GamepadButton.B => "B",
        GamepadButton.X => "X",
        GamepadButton.Y => "Y",
        GamepadButton.LeftBumper => "LB",
        GamepadButton.RightBumper => "RB",
        GamepadButton.Back => "Back",
        GamepadButton.Start => "Start",
        GamepadButton.LeftStickClick => "LS",
        GamepadButton.RightStickClick => "RS",
        GamepadButton.DPadUp => "D-pad Up",
        GamepadButton.DPadDown => "D-pad Down",
        GamepadButton.DPadLeft => "D-pad Left",
        GamepadButton.DPadRight => "D-pad Right",
        GamepadButton.LeftTrigger => "LT",
        GamepadButton.RightTrigger => "RT",
        GamepadButton.LeftStickLeft => "L-stick Left",
        GamepadButton.LeftStickRight => "L-stick Right",
        GamepadButton.LeftStickUp => "L-stick Up",
        GamepadButton.LeftStickDown => "L-stick Down",
        GamepadButton.RightStickLeft => "R-stick Left",
        GamepadButton.RightStickRight => "R-stick Right",
        GamepadButton.RightStickUp => "R-stick Up",
        GamepadButton.RightStickDown => "R-stick Down",
        _ => string.Empty
    };
}
