using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Wraps the scene's Grid/Terrain Tilemap: cell &lt;-&gt; world conversion,
/// bounds, and terrain movement cost. Terrain is read from the Tilemap once
/// into a plain array in Awake (`_terrainCache`) so Pathfinder's inner loop
/// never calls into the Tilemap API. Also owns the single shared Pathfinder
/// instance — one 10x10 board only needs one, and every system that paths
/// (player selection, enemy AI) fetches it from here instead of allocating
/// its own buffers.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class GridManager : MonoBehaviour, ITerrainCostSource
{
    [SerializeField] private Grid _grid;
    [SerializeField] private Tilemap _terrainTilemap;
    [SerializeField] private TerrainDatabase _terrainDatabase;
    [SerializeField, Min(1)] private int _width = 10;
    [SerializeField, Min(1)] private int _height = 10;

    private TerrainType[,] _terrainCache;

    public Pathfinder Pathfinder { get; private set; }

    private void Awake() {
        CacheTerrain();
        Pathfinder = new Pathfinder(_width, _height);
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<GridManager>();
    }

    public bool Contains(Vector2Int cell) {
        return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
    }

    public float MovementCost(Vector2Int cell) {
        TerrainType terrain = _terrainCache[cell.x, cell.y];
        return terrain != null ? terrain.MovementCost : 1f;
    }

    public Vector3 CellToWorld(Vector2Int cell) {
        return _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    public Vector2Int WorldToCell(Vector3 world) {
        Vector3Int cell = _grid.WorldToCell(world);
        return new Vector2Int(cell.x, cell.y);
    }

    private void CacheTerrain() {
        _terrainCache = new TerrainType[_width, _height];
        for (int cellX = 0; cellX < _width; cellX++) {
            for (int cellY = 0; cellY < _height; cellY++) {
                TileBase tile = _terrainTilemap.GetTile(new Vector3Int(cellX, cellY, 0));
                _terrainCache[cellX, cellY] = tile != null ? _terrainDatabase.Lookup(tile) : null;
            }
        }
    }
}
