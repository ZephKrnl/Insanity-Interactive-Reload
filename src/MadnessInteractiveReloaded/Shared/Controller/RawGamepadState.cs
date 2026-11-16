using System.Runtime.InteropServices;

namespace MIR;

/// <summary>
/// Raw controller state. Layout matches the native XINPUT_GAMEPAD struct exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RawGamepadState
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short ThumbLX;
    public short ThumbLY;
    public short ThumbRX;
    public short ThumbRY;
}
