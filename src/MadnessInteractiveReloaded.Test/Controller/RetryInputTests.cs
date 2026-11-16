using System;
using FluentAssertions;
using Walgelijk;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class RetryInputTests : IDisposable
{
    private readonly ControllerTestHarness h = new();

    public void Dispose() => h.Dispose();

    private Scene SceneWithDeath(float? time)
    {
        var scene = new Scene(h.Game);
        if (time.HasValue)
        {
            var e = scene.CreateEntity();
            scene.AttachComponent(e, new PlayerDeathSequenceComponent { Time = time.Value });
            scene.SetTag(e, Tags.PlayerDeathSequence);
        }
        return scene;
    }

    private void PadFrame(ushort buttons)
    {
        h.EngineReset();
        h.SetPad(buttons);
        Gamepad.Update(ControllerTestHarness.Dt);
    }

    [Fact]
    public void Pad_A_before_prompt_does_not_retry()
    {
        using var scene = SceneWithDeath(0.5f);
        PadFrame(0);
        PadFrame(ControllerTestHarness.ButtonA);
        RetryInput.Requested(scene, h.Input).Should().BeFalse();
    }

    [Fact]
    public void Pad_A_after_prompt_retries()
    {
        using var scene = SceneWithDeath(1.3f);
        PadFrame(0);
        PadFrame(ControllerTestHarness.ButtonA);
        RetryInput.Requested(scene, h.Input).Should().BeTrue();
        PadFrame(ControllerTestHarness.ButtonA);
        RetryInput.Requested(scene, h.Input).Should().BeFalse();
    }

    [Fact]
    public void Keyboard_R_retries_any_time()
    {
        using var scene = SceneWithDeath(0.1f);
        h.EngineReset();
        h.Input.KeysUp!.Add(Key.R);
        RetryInput.Requested(scene, h.Input).Should().BeTrue();
    }

    [Fact]
    public void No_death_sequence_pad_does_nothing()
    {
        using var scene = SceneWithDeath(null);
        PadFrame(0);
        PadFrame(ControllerTestHarness.ButtonA);
        RetryInput.Requested(scene, h.Input).Should().BeFalse();
    }

    [Fact]
    public void Label_follows_active_device()
    {
        PadFrame(0);
        PadFrame(ControllerTestHarness.ButtonA);
        Gamepad.IsActiveDevice.Should().BeTrue();
        RetryInput.Label.Should().Be("[A]");

        Gamepad.ActiveDevice = InputDevice.KeyboardMouse;
        RetryInput.Label.Should().Be("[R]");

        Gamepad.ActiveDevice = InputDevice.Gamepad;
        h.Pad.Connected = false;
        PadFrame(0);
        RetryInput.Label.Should().Be("[R]");
    }
}
