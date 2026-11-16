using System;
using Xunit;
using FluentAssertions;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class GamepadTests : IDisposable
{
    private readonly FakeGamepadSource fake = new();

    public GamepadTests() => Gamepad.ResetForTests(fake);

    public void Dispose() => Gamepad.ResetForTests(new NullGamepadSource());

    private void A(bool down) => fake.State.Buttons = (ushort)(down ? 0x1000 : 0);

    [Fact]
    public void Pressed_only_on_first_frame()
    {
        A(true);
        Gamepad.Update(0.016f);
        Gamepad.Pressed(GamepadButton.A).Should().BeTrue();
        Gamepad.Held(GamepadButton.A).Should().BeTrue();
        Gamepad.Update(0.016f);
        Gamepad.Pressed(GamepadButton.A).Should().BeFalse();
        Gamepad.Held(GamepadButton.A).Should().BeTrue();
    }

    [Fact]
    public void Released_on_frame_after_letting_go()
    {
        A(true);
        Gamepad.Update(0.016f);
        A(false);
        Gamepad.Update(0.016f);
        Gamepad.Released(GamepadButton.A).Should().BeTrue();
        Gamepad.Update(0.016f);
        Gamepad.Released(GamepadButton.A).Should().BeFalse();
    }

    [Fact]
    public void Disconnected_reports_nothing()
    {
        A(true);
        fake.Connected = false;
        Gamepad.Update(0.016f);
        Gamepad.IsConnected.Should().BeFalse();
        Gamepad.Held(GamepadButton.A).Should().BeFalse();
    }

    [Fact]
    public void Disabled_gamepad_reports_nothing()
    {
        Gamepad.Enabled = false;
        A(true);
        Gamepad.Update(0.016f);
        Gamepad.Held(GamepadButton.A).Should().BeFalse();
        Gamepad.HadActivityThisFrame.Should().BeFalse();
    }

    [Fact]
    public void Unfocused_reports_nothing()
    {
        A(true);
        Gamepad.Update(0.016f, hasFocus: false);
        Gamepad.Held(GamepadButton.A).Should().BeFalse();
    }

    [Fact]
    public void Activity_sets_active_device()
    {
        Gamepad.Update(0.016f);
        A(true);
        Gamepad.Update(0.016f);
        Gamepad.HadActivityThisFrame.Should().BeTrue();
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
    }

    [Fact]
    public void No_activity_on_connect_frame()
    {
        fake.Connected = false;
        Gamepad.Update(0.016f);

        fake.Connected = true;
        A(true);
        Gamepad.Update(0.016f);
        Gamepad.HadActivityThisFrame.Should().BeFalse();
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);

        Gamepad.Update(0.016f);
        Gamepad.HadActivityThisFrame.Should().BeFalse("A is still held, so there is no new press");
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
    }

    /// <summary>
    /// The raw left stick X that comes out of the radial deadzone with the given magnitude.
    /// </summary>
    private static short LeftStickRaw(float magnitude) => (short)MathF.Round(7849 + magnitude * (32767 - 7849));

    /// <summary>
    /// The highest raw trigger value that still reads as released, see <see cref="GamepadState.ApplyTriggerThreshold"/>.
    /// </summary>
    private const byte TriggerRest = 30;

    [Fact]
    public void Analog_jitter_is_not_activity()
    {
        Gamepad.Update(0.016f);
        for (int i = 0; i < 8; i++)
        {
            bool wobble = i % 2 == 0;
            fake.State.ThumbLX = wobble ? LeftStickRaw(0.1f) : (short)0;
            fake.State.ThumbRY = wobble ? (short)-LeftStickRaw(0.1f) : (short)0;
            fake.State.LeftTrigger = wobble ? TriggerRest : (byte)0;
            fake.State.RightTrigger = wobble ? TriggerRest : (byte)0;
            Gamepad.Update(0.016f);

            Gamepad.Current.LeftStick.Length().Should().BeApproximately(wobble ? 0.1f : 0, 0.01f, "the test drives the processed value");
            Gamepad.HadActivityThisFrame.Should().BeFalse("a stick or trigger wobbling near rest is not the player picking up the pad");
        }
        Gamepad.ActiveDevice.Should().Be(InputDevice.KeyboardMouse);
    }

    [Fact]
    public void Stick_push_is_activity()
    {
        Gamepad.Update(0.016f);
        fake.State.ThumbLX = LeftStickRaw(0.5f);
        Gamepad.Update(0.016f);

        Gamepad.HadActivityThisFrame.Should().BeTrue();
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);

        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        fake.State.ThumbLX = LeftStickRaw(0.6f);
        Gamepad.Update(0.016f);
        Gamepad.HadActivityThisFrame.Should().BeFalse("the stick was already past the threshold");
    }

    [Fact]
    public void Light_trigger_pull_switches_device()
    {
        Gamepad.Update(0.016f);
        fake.State.RightTrigger = 50; // a light pull, but already down: it fires Attack
        Gamepad.Update(0.016f);

        Gamepad.Held(GamepadButton.RightTrigger).Should().BeTrue();
        Gamepad.HadActivityThisFrame.Should().BeTrue("the pull that fires Attack also selects the pad");
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);

        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        fake.State.RightTrigger = 255;
        Gamepad.Update(0.016f);
        Gamepad.HadActivityThisFrame.Should().BeFalse("the trigger was already down, the same as a held button");

        fake.State.RightTrigger = 0;
        fake.State.LeftTrigger = 50;
        Gamepad.Update(0.016f);
        Gamepad.Held(GamepadButton.LeftTrigger).Should().BeTrue();
        Gamepad.HadActivityThisFrame.Should().BeTrue("a light LT pull counts too");
        Gamepad.ActiveDevice.Should().Be(InputDevice.Gamepad);
    }

    [Fact]
    public void Rumble_stops_after_duration()
    {
        Gamepad.Rumble(0.5f, 0.7f, 0.1f);
        Gamepad.Update(0.016f);
        fake.Vibrations[0].Should().Be((0.5f, 0.7f));
        Gamepad.Update(0.11f);
        fake.Vibrations[^1].Should().Be((0f, 0f));
    }

    [Fact]
    public void StopRumble_turns_motors_off_immediately()
    {
        Gamepad.Rumble(0.5f, 0.7f, 1f);
        Gamepad.Update(0.016f);
        fake.Vibrations[^1].Should().Be((0.5f, 0.7f));

        // On shutdown no Update follows, so StopRumble itself has to reach the motors.
        Gamepad.StopRumble();
        fake.Vibrations[^1].Should().Be((0f, 0f));

        Gamepad.Update(0.016f);
        fake.Vibrations.Should().HaveCount(2, "the motors are already off");
    }

    [Fact]
    public void Rumble_ignored_when_vibration_disabled()
    {
        Gamepad.VibrationEnabled = false;
        Gamepad.Rumble(0.5f, 0.7f, 0.1f);
        Gamepad.Update(0.016f);
        fake.Vibrations.Should().BeEmpty();
    }
}
