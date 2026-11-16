using System;
using System.Diagnostics;
namespace MIR;

/// <summary>
/// Reads the first connected XInput pad. When no pad is connected, slots are only rescanned every
/// <see cref="RescanIntervalSeconds"/> because probing empty slots is slow.
/// </summary>
public sealed class XInputGamepadSource : IGamepadSource
{
    public const double RescanIntervalSeconds = 2.0;
    private const int SlotCount = 4;

    private readonly Func<uint, (uint result, RawGamepadState state)> probe;
    private readonly Action<uint, ushort, ushort> setState;
    private readonly Func<double> clockSeconds;

    private double nextScanTime;
    private bool broken;

    public int? ActiveSlot { get; private set; }

    public XInputGamepadSource()
    {
        var sw = Stopwatch.StartNew();
        clockSeconds = () => sw.Elapsed.TotalSeconds;
        probe = slot =>
        {
            var r = XInputNative.GetState(slot, out var st);
            return (r, st);
        };
        setState = (slot, low, high) => XInputNative.SetState(slot, low, high);
    }

    public XInputGamepadSource(
        Func<uint, (uint result, RawGamepadState state)> probe,
        Action<uint, ushort, ushort> setState,
        Func<double> clockSeconds)
    {
        this.probe = probe;
        this.setState = setState;
        this.clockSeconds = clockSeconds;
    }

    public bool TryGetState(out RawGamepadState state)
    {
        state = default;
        if (broken)
            return false;

        try
        {
            var now = clockSeconds();

            if (ActiveSlot is int active)
            {
                var (result, st) = probe((uint)active);
                if (result == XInputNative.ErrorSuccess)
                {
                    state = st;
                    return true;
                }
                ActiveSlot = null;
                nextScanTime = now + RescanIntervalSeconds;
                return false;
            }

            if (now < nextScanTime)
                return false;

            nextScanTime = now + RescanIntervalSeconds;
            for (uint slot = 0; slot < SlotCount; slot++)
            {
                var (result, st) = probe(slot);
                if (result == XInputNative.ErrorSuccess)
                {
                    ActiveSlot = (int)slot;
                    state = st;
                    return true;
                }
            }
            return false;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            broken = true;
            ActiveSlot = null;
            return false;
        }
    }

    public void SetVibration(float low, float high)
    {
        if (broken || ActiveSlot is not int slot)
            return;

        try
        {
            setState((uint)slot, ToMotor(low), ToMotor(high));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            broken = true;
        }
    }

    private static ushort ToMotor(float v)
    {
        if (float.IsNaN(v)) v = 0;
        return (ushort)(Math.Clamp(v, 0f, 1f) * 65535);
    }
}
