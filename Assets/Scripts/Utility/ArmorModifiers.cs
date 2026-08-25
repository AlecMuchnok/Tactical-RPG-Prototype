using System;

/// <summary>Pure lookup: which power/dex/speed multipliers an ArmorType contributes to dodge chance.</summary>
public readonly struct ArmorModifiers
{
    public readonly float PowerModifier;
    public readonly float DexterityModifier;
    public readonly float SpeedModifier;

    private ArmorModifiers(float powerModifier, float dexterityModifier, float speedModifier) {
        PowerModifier = powerModifier;
        DexterityModifier = dexterityModifier;
        SpeedModifier = speedModifier;
    }

    public static ArmorModifiers For(ArmorType armorType) {
        switch (armorType) {
            case ArmorType.None: return new ArmorModifiers(0f, 1.5f, 1.5f);
            case ArmorType.Light: return new ArmorModifiers(0f, 1.5f, 1.4f);
            case ArmorType.Medium: return new ArmorModifiers(0.2f, 1.5f, 1.3f);
            case ArmorType.Heavy: return new ArmorModifiers(0.4f, 1.5f, 1.1f);
            default: throw new ArgumentOutOfRangeException(nameof(armorType), armorType, null);
        }
    }
}
