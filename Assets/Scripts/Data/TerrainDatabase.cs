using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Every TerrainType in the game, plus a Tile -> TerrainType lookup built
/// once so GridManager never has to scan the list at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Game/Terrain Database")]
public sealed class TerrainDatabase : ScriptableObject
{
    [SerializeField] private List<TerrainType> _terrainTypes = new List<TerrainType>();

    private Dictionary<TileBase, TerrainType> _lookup;

    private void OnEnable() {
        _lookup = new Dictionary<TileBase, TerrainType>();
        for (int terrainIndex = 0; terrainIndex < _terrainTypes.Count; terrainIndex++) {
            TerrainType terrainType = _terrainTypes[terrainIndex];
            if (terrainType != null && terrainType.Tile != null) {
                _lookup[terrainType.Tile] = terrainType;
            }
        }
    }

    public TerrainType Lookup(TileBase tile) {
        if (_lookup == null) { OnEnable(); }
        return _lookup.TryGetValue(tile, out TerrainType terrainType) ? terrainType : null;
    }
}
