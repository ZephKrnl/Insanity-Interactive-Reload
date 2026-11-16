using System.Collections.Generic;
using Walgelijk;

namespace MIR;

public class ControlScheme
{
    public static ControlScheme ActiveControlScheme { get; set; } = new();

    public readonly Dictionary<GameAction, UserInput> InputMap = new()
    {
        { GameAction.Right, new(Key.D) },
        { GameAction.Left, new(Key.A) },
        { GameAction.JumpDodge, new(Key.Space) },
        { GameAction.Interact, new(Key.E) },

        { GameAction.Attack, new(MouseButton.Left) },
        { GameAction.Melee, new(MouseButton.Middle) },
        { GameAction.BlockAim, new(MouseButton.Right) },
        { GameAction.Throw, new(Key.F) },

        { GameAction.Ability1, new(Key.LeftShift) },
        { GameAction.Ability2, new(Key.Q) },
        { GameAction.Ability3, new(Key.LeftControl) },
        { GameAction.Ability4, new(Key.R) },
    };

    /// <summary>
    /// Controller bindings, the defaults until the player rebinds them in the settings. Missing keys in saved settings keep
    /// the defaults.
    /// </summary>
    public readonly Dictionary<GameAction, GamepadButton> GamepadMap = DefaultGamepadMap();

    private static Dictionary<GameAction, GamepadButton> DefaultGamepadMap() => new()
    {
        { GameAction.Left, GamepadButton.LeftStickLeft },
        { GameAction.Right, GamepadButton.LeftStickRight },
        { GameAction.JumpDodge, GamepadButton.A },
        { GameAction.Interact, GamepadButton.Y },

        { GameAction.Attack, GamepadButton.RightTrigger },
        { GameAction.Melee, GamepadButton.B },
        { GameAction.BlockAim, GamepadButton.LeftTrigger },
        { GameAction.Throw, GamepadButton.X },

        { GameAction.Ability1, GamepadButton.LeftBumper },
        { GameAction.Ability2, GamepadButton.RightBumper },
        { GameAction.Ability3, GamepadButton.DPadLeft },
        { GameAction.Ability4, GamepadButton.DPadRight },
    };

    /// <summary>
    /// Puts <see cref="GamepadMap"/> back to the defaults, in place and in the default order, so everything holding the
    /// dictionary sees the change.
    /// </summary>
    public void ResetGamepadMap()
    {
        GamepadMap.Clear();
        foreach (var (action, button) in DefaultGamepadMap())
            GamepadMap.Add(action, button);
    }

    /// <summary>
    /// The label of the binding for the given action on the device the player is currently using:
    /// the controller button when the gamepad is the active device and connected, otherwise the keyboard/mouse input.
    /// Prompts (HUD, tutorial) should use this rather than formatting <see cref="InputMap"/> directly.
    /// </summary>
    public string GetDisplayName(GameAction action)
    {
        if (Gamepad.IsActiveDevice && Gamepad.IsConnected && GamepadMap.TryGetValue(action, out var button))
            return button.ToDisplayString();
        return InputMap.TryGetValue(action, out var input) ? input.ToString() : string.Empty;
    }

    // Pad terms count only while Gamepad.GameplayInputAllowed, so a button a menu used this frame can't also act in gameplay.

    public bool Held(GameAction action, in InputState input)
        => (InputMap.TryGetValue(action, out var userInput) && userInput.Held(input))
        || (Gamepad.GameplayInputAllowed && GamepadMap.TryGetValue(action, out var button) && Gamepad.Held(button));

    public bool Released(GameAction action, in InputState input)
        => (InputMap.TryGetValue(action, out var userInput) && userInput.Released(input))
        || (Gamepad.GameplayInputAllowed && GamepadMap.TryGetValue(action, out var button) && Gamepad.Released(button));

    public bool Pressed(GameAction action, in InputState input)
        => (InputMap.TryGetValue(action, out var userInput) && userInput.Pressed(input))
        || (Gamepad.GameplayInputAllowed && GamepadMap.TryGetValue(action, out var button) && Gamepad.Pressed(button));
}
