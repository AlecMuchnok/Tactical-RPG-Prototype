using System.Collections.Generic;
using UnityEngine;

/// <summary>Designer-tunable class data: which weapon types a unit of this class can equip, its movement range, and its armor type.</summary>
[CreateAssetMenu(menuName = "Game/Class")]
public sealed class Class : ScriptableObject
{
    [SerializeField] private WeaponType[] _equippableWeaponTypes;
    [SerializeField, Min(0)] private int _movement = 4;
    [SerializeField] private ArmorType _armorType;

    public IReadOnlyList<WeaponType> EquippableWeaponTypes => _equippableWeaponTypes;
    public int Movement => _movement;
    public ArmorType ArmorType => _armorType;
}
