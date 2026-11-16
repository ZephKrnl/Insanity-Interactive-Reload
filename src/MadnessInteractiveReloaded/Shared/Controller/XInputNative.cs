using System;
using System.Runtime.InteropServices;
namespace MIR;

/// <summary>
/// Late-bound XInput. Never throws when the library is missing (non-Windows, stripped installs).
/// </summary>
public static class XInputNative
{
    public const uint ErrorSuccess = 0;
    public const uint ErrorDeviceNotConnected = 1167;

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputStateNative
    {
        public uint PacketNumber;
        public RawGamepadState Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibrationNative
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint GetStateDelegate(uint slot, out XInputStateNative state);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint SetStateDelegate(uint slot, ref XInputVibrationNative vibration);

    private static readonly object loadLock = new();
    private static bool loaded;
    private static GetStateDelegate? getState;
    private static SetStateDelegate? setState;

    public static bool IsAvailable
    {
        get
        {
            EnsureLoaded();
            return getState != null;
        }
    }

    public static uint GetState(uint slot, out RawGamepadState state)
    {
        EnsureLoaded();
        state = default;
        if (getState == null)
            return ErrorDeviceNotConnected;

        var result = getState(slot, out var native);
        if (result == ErrorSuccess)
            state = native.Gamepad;
        return result;
    }

    public static uint SetState(uint slot, ushort low, ushort high)
    {
        EnsureLoaded();
        if (setState == null)
            return ErrorDeviceNotConnected;

        var vib = new XInputVibrationNative { LeftMotorSpeed = low, RightMotorSpeed = high };
        return setState(slot, ref vib);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;
        lock (loadLock)
        {
            if (loaded)
                return;
            try
            {
                foreach (var name in new[] { "xinput1_4.dll", "xinput9_1_0.dll" })
                {
                    if (!NativeLibrary.TryLoad(name, out var lib))
                        continue;
                    if (NativeLibrary.TryGetExport(lib, "XInputGetState", out var g) &&
                        NativeLibrary.TryGetExport(lib, "XInputSetState", out var s))
                    {
                        getState = Marshal.GetDelegateForFunctionPointer<GetStateDelegate>(g);
                        setState = Marshal.GetDelegateForFunctionPointer<SetStateDelegate>(s);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                getState = null;
                setState = null;
            }
            loaded = true;
        }
    }
}
