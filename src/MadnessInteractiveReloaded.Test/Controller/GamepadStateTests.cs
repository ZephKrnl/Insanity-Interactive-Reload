using Xunit;
using System.Numerics;
using FluentAssertions;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class GamepadStateTests
{
    [Fact]
    public void Inside_deadzone_is_zero()
    {
        GamepadState.ApplyRadialDeadzone(7849, 0, 7849).Should().Be(Vector2.Zero);
        GamepadState.ApplyRadialDeadzone(5000, 5000, 7849).Should().Be(Vector2.Zero);
    }

    [Fact]
    public void Full_tilt_is_unit()
    {
        GamepadState.ApplyRadialDeadzone(32767, 0, 7849).X.Should().BeApproximately(1f, 0.001f);
        var v = GamepadState.ApplyRadialDeadzone(-32768, 0, 7849);
        v.X.Should().BeApproximately(-1f, 0.001f);
        v.Length().Should().BeLessThanOrEqualTo(1f);
    }

    [Fact]
    public void Just_outside_deadzone_is_small()
    {
        GamepadState.ApplyRadialDeadzone(7849 + 2490, 0, 7849).X.Should().BeApproximately(0.1f, 0.01f);
    }

    [Fact]
    public void Stick_y_is_up_positive()
    {
        GamepadState.FromRaw(new RawGamepadState { ThumbLY = 32767 }).LeftStick.Y.Should().BeGreaterThan(0.99f);
    }

    [Fact]
    public void Trigger_threshold()
    {
        GamepadState.ApplyTriggerThreshold(30).Should().Be(0f);
        GamepadState.ApplyTriggerThreshold(255).Should().Be(1f);
        GamepadState.ApplyTriggerThreshold(142).Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact]
    public void IsDown_virtual_buttons()
    {
        GamepadState.FromRaw(new RawGamepadState { RightTrigger = 255 }).IsDown(GamepadButton.RightTrigger).Should().BeTrue();
        GamepadState.FromRaw(new RawGamepadState { RightTrigger = 30 }).IsDown(GamepadButton.RightTrigger).Should().BeFalse();

        var left = GamepadState.FromRaw(new RawGamepadState { ThumbLX = -32768 });
        left.IsDown(GamepadButton.LeftStickLeft).Should().BeTrue();
        left.IsDown(GamepadButton.LeftStickRight).Should().BeFalse();

        GamepadState.FromRaw(new RawGamepadState { ThumbLX = (short)(0.3f * 32767) })
            .IsDown(GamepadButton.LeftStickRight).Should().BeFalse();

        GamepadState.FromRaw(new RawGamepadState { Buttons = 0x1000 }).IsDown(GamepadButton.A).Should().BeTrue();
    }
}
