using UnityEngine;

/// <summary>Pure combat formulas for hit chance, dodge chance, and damage rolls.</summary>
public static class CombatMath
{
    public static float HitChance(UnitStats stats) {
        return stats.Power * 1.4f + stats.Dexterity * 1.4f + stats.Speed * 1.1f;
    }

    public static float DodgeChance(UnitStats stats) {
        return stats.Power * 0.2f + stats.Dexterity * 1.5f + stats.Speed * 1.3f;
    }

    public static float AttackChance(UnitStats attacker, UnitStats defender) {
        return Mathf.Clamp(HitChance(attacker) - DodgeChance(defender), 0f, 100f);
    }

    /// <summary>Random range centered on `power`, spread by +/-`variance` (0.1 = +/-10%).</summary>
    public static int RollDamage(int power, float variance) {
        float min = power * (1f - variance);
        float max = power * (1f + variance);
        int damage = Mathf.RoundToInt(Random.Range(min, max));
        return Mathf.Max(damage, 1);
    }
}
