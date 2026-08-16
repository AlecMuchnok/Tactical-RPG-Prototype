using UnityEngine;

/// <summary>Designer-tunable base stats for a unit kind (Hero, Enemy, ...).</summary>
[CreateAssetMenu(menuName = "Game/Unit Stats")]
public sealed class UnitStats : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _sprite;
    [SerializeField, Min(1)] private int _maxHealth = 100;
    [SerializeField, Min(0)] private int _movement = 4;
    [SerializeField, Min(0)] private int _power = 10;
    [SerializeField, Min(0)] private int _dexterity = 10;
    [SerializeField, Min(0)] private int _speed = 10;

    public string DisplayName => _displayName;
    public Sprite Sprite => _sprite;
    public int MaxHealth => _maxHealth;
    public int Movement => _movement;
    public int Power => _power;
    public int Dexterity => _dexterity;
    public int Speed => _speed;
}
