namespace MIR;

public interface IGamepadSource
{
    /// <summary>
    /// Reads the current raw state. Returns false when no controller is connected.
    /// </summary>
    bool TryGetState(out RawGamepadState state);

    /// <summary>
    /// Motor speeds from 0 to 1.
    /// </summary>
    void SetVibration(float low, float high);
}

/// <summary>
/// A source that is always disconnected.
/// </summary>
public sealed class NullGamepadSource : IGamepadSource
{
    public bool TryGetState(out RawGamepadState state)
    {
        state = default;
        return false;
    }

    public void SetVibration(float low, float high) { }
}
