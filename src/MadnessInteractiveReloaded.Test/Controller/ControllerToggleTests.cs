using System;
using FluentAssertions;
using Xunit;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class ControllerToggleTests : IDisposable
{
    public ControllerToggleTests() => ResetStatics();

    public void Dispose() => ResetStatics();

    private static void ResetStatics()
    {
        Gamepad.ResetForTests(new FakeGamepadSource());
        MenuNavigator.ResetSuspensionForTests();
    }

    [Fact]
    public void Turning_the_controller_off_with_the_pad_asks_first()
    {
        MenuUiUtils.AsksBeforeTurningControllerOff(wasOn: true, nowOn: false, padActive: true)
            .Should().BeTrue("the player would lose the device they are using");

        MenuUiUtils.AsksBeforeTurningControllerOff(wasOn: true, nowOn: false, padActive: false)
            .Should().BeFalse("with the mouse or keyboard it can be turned back on");
        MenuUiUtils.AsksBeforeTurningControllerOff(wasOn: false, nowOn: true, padActive: true)
            .Should().BeFalse("turning it on stays immediate");
        MenuUiUtils.AsksBeforeTurningControllerOff(wasOn: false, nowOn: true, padActive: false)
            .Should().BeFalse();
    }
}
