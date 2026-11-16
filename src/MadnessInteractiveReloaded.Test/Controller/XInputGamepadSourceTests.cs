using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace MIR.Test.Controller;

[Collection("Registry collection")]
public class XInputGamepadSourceTests
{
    private const uint Connected = 0;
    private const uint NotConnected = 1167;

    private double now;
    private readonly List<uint> probes = new();
    private readonly List<(uint slot, ushort low, ushort high)> vib = new();
    private readonly Dictionary<uint, uint> results = new();

    private XInputGamepadSource Make(Func<uint, (uint, RawGamepadState)> probe = null)
    {
        probe ??= slot =>
        {
            probes.Add(slot);
            var r = results.TryGetValue(slot, out var v) ? v : NotConnected;
            return (r, new RawGamepadState { Buttons = (ushort)(slot + 1) });
        };
        return new XInputGamepadSource(probe, (s, l, h) => vib.Add((s, l, h)), () => now);
    }

    [Fact]
    public void No_pad_scans_all_slots_once_then_waits()
    {
        var src = Make();
        src.TryGetState(out _).Should().BeFalse();
        probes.Should().Equal(0u, 1u, 2u, 3u);

        now = 1.0;
        src.TryGetState(out _).Should().BeFalse();
        probes.Should().HaveCount(4);

        now = 2.1;
        src.TryGetState(out _).Should().BeFalse();
        probes.Should().HaveCount(8);
    }

    [Fact]
    public void Connected_pad_polls_only_its_slot()
    {
        results[2] = Connected;
        var src = Make();
        src.TryGetState(out var st).Should().BeTrue();
        st.Buttons.Should().Be(3);
        src.ActiveSlot.Should().Be(2);
        probes.Clear();
        for (int i = 0; i < 10; i++)
            src.TryGetState(out _).Should().BeTrue();
        probes.Should().HaveCount(10).And.OnlyContain(s => s == 2);
    }

    [Fact]
    public void Unplug_triggers_rescan_after_interval()
    {
        results[2] = Connected;
        var src = Make();
        src.TryGetState(out _).Should().BeTrue();

        results[2] = NotConnected;
        now = 0.5;
        probes.Clear();
        src.TryGetState(out _).Should().BeFalse();
        src.ActiveSlot.Should().BeNull();
        probes.Should().Equal(2u);

        now = 1.0;
        src.TryGetState(out _).Should().BeFalse();
        probes.Should().HaveCount(1);

        now = 2.6;
        src.TryGetState(out _).Should().BeFalse();
        probes.Should().HaveCount(5);
    }

    [Fact]
    public void Missing_dll_never_throws()
    {
        int calls = 0;
        var src = Make(_ => { calls++; throw new DllNotFoundException(); });
        src.TryGetState(out _).Should().BeFalse();
        int after = calls;
        now = 5;
        src.TryGetState(out _).Should().BeFalse();
        now = 50;
        src.TryGetState(out _).Should().BeFalse();
        calls.Should().Be(after);
    }

    [Fact]
    public void Vibration_goes_to_active_slot_scaled()
    {
        var src = Make();
        src.SetVibration(1f, 0.5f);
        vib.Should().BeEmpty();

        results[1] = Connected;
        src.TryGetState(out _).Should().BeTrue();
        src.SetVibration(1f, 0.5f);
        vib.Should().Equal((1u, (ushort)65535, (ushort)32767));
    }
}
