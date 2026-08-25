using UnityEngine;

/// <summary>Base accuracy and its stat modifiers for hit chance and damage.</summary>
[CreateAssetMenu(menuName = "Game/Weapon")]
public sealed class Weapon : ScriptableObject
{
    [SerializeField] private WeaponType _weaponType;
    [SerializeField, Range(1, 100)] private int _baseAccuracy = 50;
    [SerializeField] private float _powerModifier = 1f;
    [SerializeField] private float _dexterityModifier = 1f;
    [SerializeField] private float _speedModifier = 1f;

    public WeaponType WeaponType => _weaponType;
    public int BaseAccuracy => _baseAccuracy;
    public float PowerModifier => _powerModifier;
    public float DexterityModifier => _dexterityModifier;
    public float SpeedModifier => _speedModifier;
}
