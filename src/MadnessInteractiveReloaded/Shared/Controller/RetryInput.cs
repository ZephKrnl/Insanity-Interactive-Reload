using Walgelijk;

namespace MIR;

/// <summary>
/// Retry (campaign) and revive (other modes) from the death screen, with R or the pad's A.
/// </summary>
public static class RetryInput
{
    /// <summary>
    /// Seconds into the death sequence before the pad counts: the prompt is visible from then on, so mashing A while dying
    /// doesn't restart instantly.
    /// </summary>
    public const float PadAcceptDelay = 1.25f;

    public static bool Requested(Scene scene, in InputState input)
    {
        if (input.IsKeyReleased(Key.R))
            return true;

        // Read the pad directly: GameplayInputAllowed is false while the player is dead.
        if (Gamepad.Pressed(GamepadButton.A)
            && scene.TryGetEntityWithTag(Tags.PlayerDeathSequence, out var entity)
            && scene.TryGetComponentFrom<PlayerDeathSequenceComponent>(entity, out var seq))
            return seq.Time >= PadAcceptDelay;

        return false;
    }

    public static string Label => Gamepad.IsActiveDevice && Gamepad.IsConnected ? "[A]" : "[R]";
}
