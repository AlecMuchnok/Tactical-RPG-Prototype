using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One terrain kind: its tile visual and its movement cost. No impassability
/// flag — a cost of 99 against a max unit Movement of 4 is naturally
/// unreachable, so "impassable" is an emergent property of the number, not
/// something the code needs to check for separately.
/// </summary>
[CreateAssetMenu(menuName = "Game/Terrain Type")]
public sealed class TerrainType : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private TileBase _tile;
    [SerializeField, Range(1, 99)] private int _movementCost = 1;

    public string DisplayName => _displayName;
    public TileBase Tile => _tile;
    public int MovementCost => _movementCost;
}
