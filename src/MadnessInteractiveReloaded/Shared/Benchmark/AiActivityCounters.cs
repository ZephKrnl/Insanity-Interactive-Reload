namespace MIR;

/// <summary>
/// Running totals of what the AI does, for the benchmark's "shots per minute" and "melee attacks per minute". They only count;
/// nothing reads them to decide anything. Main thread only.
/// </summary>
public static class AiActivityCounters
{
    /// <summary>Bullets fired by an <see cref="Tags.EnemyAI"/> character: one per round, counted in <c>WeaponSystem.EmitBulletFrom</c>.</summary>
    public static long Shots;

    /// <summary>Melee sequences started by an <see cref="Tags.EnemyAI"/> character, counted in <see cref="MeleeUtils.TryPerformMeleeAttack"/>.</summary>
    public static long MeleeAttacks;
}
