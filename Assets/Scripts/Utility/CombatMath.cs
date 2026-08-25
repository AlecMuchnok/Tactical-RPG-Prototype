using UnityEngine;

/// <summary>Pure combat formulas for hit chance, dodge chance, and damage.</summary>
public static class CombatMath
{
    public static float HitChance(Character character, Weapon weapon) {
        float statModifier = character.Power * weapon.PowerModifier
            + character.Dexterity * weapon.DexterityModifier
            + character.Speed * weapon.SpeedModifier;
        return weapon.BaseAccuracy + statModifier;
    }

    public static float DodgeChance(Character character, ArmorType armorType) {
        ArmorModifiers modifiers = ArmorModifiers.For(armorType);
        return character.Power * modifiers.PowerModifier
            + character.Dexterity * modifiers.DexterityModifier
            + character.Speed * modifiers.SpeedModifier;
    }

    public static float AttackChance(Character attacker, Weapon weapon, Character defender, ArmorType defenderArmorType) {
        return Mathf.Clamp(HitChance(attacker, weapon) - DodgeChance(defender, defenderArmorType), 0f, 100f);
    }

    /// <summary>Placeholder damage formula: unit stats weighted by weapon modifiers, no randomness.
    /// Deliberately does not call HitChance/AttackChance — it's a stand-in for a real damage model
    /// and expected to change shape independently of the hit-chance formulas.</summary>
    public static int CalculateDamage(Character character, Weapon weapon) {
        float raw = character.Power * weapon.PowerModifier
            + character.Dexterity * weapon.DexterityModifier
            + character.Speed * weapon.SpeedModifier;
        return Mathf.Max(Mathf.RoundToInt(raw), 1);
    }
}
