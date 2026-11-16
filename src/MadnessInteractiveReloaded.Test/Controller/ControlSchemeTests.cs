using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Walgelijk;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class ControlSchemeTests : IDisposable
{
    private readonly FakeGamepadSource fake = new();

    public ControlSchemeTests() => Gamepad.ResetForTests(fake);

    public void Dispose()
    {
        Gamepad.ResetForTests(new NullGamepadSource());
        ControlScheme.ActiveControlScheme = new ControlScheme();
    }

    [Fact]
    public void Default_gamepad_map_matches_layout()
    {
        var map = new ControlScheme().GamepadMap;
        var expected = new Dictionary<GameAction, GamepadButton>
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
        map.Should().BeEquivalentTo(expected);
        map.Count.Should().Be(12);
    }

    [Fact]
    public void Pad_triggers_action_without_keyboard()
    {
        var scheme = ControlScheme.ActiveControlScheme;
        fake.State.RightTrigger = 255;
        Gamepad.Update(0.016f);
        scheme.Held(GameAction.Attack, new InputState()).Should().BeTrue();
        fake.State.RightTrigger = 0;
        Gamepad.Update(0.016f);
        scheme.Held(GameAction.Attack, new InputState()).Should().BeFalse();
    }

    [Fact]
    public void Keyboard_still_works_with_pad_connected()
    {
        Gamepad.Update(0.016f);
        var input = new InputState();
        input.KeysHeld.Add(Key.D);
        new ControlScheme().Held(GameAction.Right, input).Should().BeTrue();
    }

    [Fact]
    public void Display_name_follows_active_device()
    {
        var scheme = new ControlScheme();
        Gamepad.Update(0.016f);
        Gamepad.ActiveDevice = InputDevice.Gamepad;
        scheme.GetDisplayName(GameAction.Attack).Should().Be("RT");
        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        scheme.GetDisplayName(GameAction.Attack).Should().Be("LMB");
    }

    [Fact]
    public void Display_strings_are_plain_ascii()
    {
        GamepadButton.DPadLeft.ToDisplayString().Should().Be("D-pad Left");
        GamepadButton.LeftStickRight.ToDisplayString().Should().Be("L-stick Right");

        // The UI fonts (Oxanium, Cascadia Mono) have no arrow glyphs.
        foreach (var b in Enum.GetValues<GamepadButton>())
            b.ToDisplayString().Should().MatchRegex("^[ -~]*$", "{0} must render in the UI fonts", b);
    }

    [Fact]
    public void Old_settings_json_gets_default_gamepad_map()
    {
        var s = JsonConvert.DeserializeObject<Settings>("{\"Controls\":{\"InputMap\":{}}}");
        s.Controls.GamepadMap[GameAction.JumpDodge].Should().Be(GamepadButton.A);
        s.Controller.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Gamepad_map_round_trips()
    {
        var s = new Settings();
        s.Controls.GamepadMap[GameAction.Throw] = GamepadButton.DPadDown;
        var json = JsonConvert.SerializeObject(s, Formatting.Indented);
        json.Should().Contain("\"DPadDown\"");
        var back = JsonConvert.DeserializeObject<Settings>(json);
        back.Controls.GamepadMap[GameAction.Throw].Should().Be(GamepadButton.DPadDown);
    }
}
