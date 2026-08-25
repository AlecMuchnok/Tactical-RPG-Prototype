using UnityEngine;

/// <summary>Designer-tunable base stats for a character (Hero, Enemy, ...), independent of class.</summary>
[CreateAssetMenu(menuName = "Game/Character")]
public sealed class Character : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _sprite;
    [SerializeField, Min(1)] private int _maxHealth = 20;
    [SerializeField, Min(0)] private int _power = 5;
    [SerializeField, Min(0)] private int _dexterity = 3;
    [SerializeField, Min(0)] private int _speed = 3;
    [SerializeField, Min(0)] private int _defense = 0;
    [SerializeField, Min(0)] private int _magicResistance = 0;

    public string DisplayName => _displayName;
    public Sprite Sprite => _sprite;
    public int MaxHealth => _maxHealth;
    public int Power => _power;
    public int Dexterity => _dexterity;
    public int Speed => _speed;
    public int Defense => _defense;
    public int MagicResistance => _magicResistance;
}
