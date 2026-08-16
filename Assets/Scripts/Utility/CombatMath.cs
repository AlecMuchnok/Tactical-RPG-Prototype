using UnityEngine;

/// <summary>
/// Pure combat formulas — no Unity lifecycle, no ScriptableObject references.
/// Callers pass raw stat values so this stays the easiest thing in the
/// codebase to unit-test later.
/// </summary>
public static class CombatMath
{
    public static float HitChance(int power, int dexterity, int speed)
    {
        return power * 1.4f + dexterity * 1.4f + speed * 1.1f;
    }

    public static float DodgeChance(int power, int dexterity, int speed)
    {
        return power * 0.2f + dexterity * 1.5f + speed * 1.3f;
    }

    public static float FinalHitChance(float attackerHitChance, float defenderDodgeChance)
    {
        return Mathf.Clamp(attackerHitChance - defenderDodgeChance, 0f, 100f);
    }

    /// <summary>Random range centered on `power`, spread by +/-`variance` (0.1 = +/-10%).</summary>
    public static int RollDamage(int power, float variance)
    {
        float min = power * (1f - variance);
        float max = power * (1f + variance);
        int damage = Mathf.RoundToInt(Random.Range(min, max));
        return Mathf.Max(damage, 1);
    }
}
