using System.Runtime.InteropServices;

namespace MIR;

/// <summary>
/// Asks Windows for a 1 ms timer resolution so Thread.Sleep is accurate to about a millisecond instead of about 15.
/// Does nothing off Windows.
/// </summary>
public static class WinTimerResolution
{
    private const uint PeriodMs = 1;
    private static bool active;

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint period);

    /// <summary>
    /// Start the 1 ms period. Safe to call more than once. Pair with <see cref="End"/>.
    /// </summary>
    public static void Begin()
    {
        if (active || !System.OperatingSystem.IsWindows())
            return;
        try { active = TimeBeginPeriod(PeriodMs) == 0; }
        catch (System.Exception) { active = false; } // winmm missing: sleeps are just coarser
    }

    /// <summary>
    /// End the period started by <see cref="Begin"/>. Does nothing if it was not started.
    /// </summary>
    public static void End()
    {
        if (!active)
            return;
        active = false;
        try { TimeEndPeriod(PeriodMs); }
        catch (System.Exception) { }
    }
}
