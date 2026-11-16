namespace MIR;

/// <summary>
/// Vibration strengths for game events, in one place for tuning. Motor values are 0 to 1, durations in seconds.
/// Callers only rumble when <see cref="Gamepad.IsActiveDevice"/>.
/// </summary>
public static class ControllerRumble
{
    public const float ShotLow = 0.15f;
    public const float ShotSeconds = 0.08f;

    /// <summary>
    /// <see cref="WeaponData.Recoil"/> at which a shot reaches the strongest high-frequency rumble.
    /// Vanilla firearms range from 0 to about 2.
    /// </summary>
    public const float FullRumbleRecoil = 2;

    public const float HitLow = 0.6f;
    public const float HitHigh = 0.4f;
    public const float HitSeconds = 0.2f;

    public const float DeathLow = 0.9f;
    public const float DeathHigh = 0.9f;
    public const float DeathSeconds = 0.45f;

    /// <summary>
    /// High-frequency motor strength for a shot: 0.35 plus up to 0.3 more as <paramref name="recoil"/> approaches <see cref="FullRumbleRecoil"/>.
    /// </summary>
    public static float ShotHigh(float recoil) => 0.35f + 0.3f * float.Clamp(recoil / FullRumbleRecoil, 0, 1);

    /// <summary>
    /// A short pulse for one shot of a weapon with the given recoil.
    /// </summary>
    public static void Shot(float recoil) => Gamepad.Rumble(ShotLow, ShotHigh(recoil), ShotSeconds);

    /// <summary>
    /// The player lost health.
    /// </summary>
    public static void Hit() => Gamepad.Rumble(HitLow, HitHigh, HitSeconds);

    /// <summary>
    /// The player died.
    /// </summary>
    public static void Death() => Gamepad.Rumble(DeathLow, DeathHigh, DeathSeconds);
}
