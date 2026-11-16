using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Walgelijk;

namespace MIR;

/// <summary>
/// Moves the operating system's mouse cursor.
/// </summary>
public interface ICursorWarper
{
    /// <summary>
    /// Moves the cursor to <paramref name="windowPosition"/>, given in window coordinates.
    /// </summary>
    void Warp(Window window, Vector2 windowPosition);
}

/// <summary>
/// Moves the cursor with <c>SetCursorPos</c>. Does nothing on platforms other than Windows.
/// </summary>
public sealed class Win32CursorWarper : ICursorWarper
{
    public void Warp(Window window, Vector2 windowPosition)
    {
        if (!OperatingSystem.IsWindows())
            return;

        // WindowToScreenPoint already truncates to whole pixels, so the cursor lands on the target truncated, not rounded.
        var screen = window.WindowToScreenPoint(windowPosition);
        SetCursorPos((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y));
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
}
