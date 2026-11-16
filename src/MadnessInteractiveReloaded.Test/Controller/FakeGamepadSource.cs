using System;
using System.Collections.Generic;
using Xunit;
namespace MIR.Test.Controller;

public class FakeGamepadSource : IGamepadSource
{
    public RawGamepadState State;
    public bool Connected = true;
    public List<(float, float)> Vibrations = new();

    public bool TryGetState(out RawGamepadState state)
    {
        state = State;
        return Connected;
    }

    public void SetVibration(float low, float high) => Vibrations.Add((low, high));
}
