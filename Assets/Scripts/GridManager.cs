using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstraps the 10x10 isometric grid and the player unit at runtime, and
/// frames the main camera to fit the whole board. This is the single object
/// placed in the scene (via Tools/Tactical RPG/Setup Scene); everything else
/// is generated in Awake so no hand-authored prefabs or scene content are needed.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private float tileWidth = 1f;
    [SerializeField] private float tileHeight = 0.5f;

    [Header("Unit")]
    [SerializeField] private Vector2Int unitStartCell = new Vector2Int(4, 4);

    [Header("Camera")]
    [SerializeField] private float cameraPadding = 1.5f;

    public IsoGrid Grid { get; private set; }
    public Unit PlayerUnit { get; private set; }

    private readonly Dictionary<Vector2Int, GridCell> _cells = new Dictionary<Vector2Int, GridCell>();
    // A list rather than a Dictionary<Vector2Int, Unit>: units move, so a coord-keyed
    // dictionary would need re-keying on every step. Coord is read live from each Unit
    // instead, which stays correct with no extra bookkeeping — cheap at this unit count.
    private readonly List<Unit> _units = new List<Unit>();

    private void Awake()
    {
        Grid = new IsoGrid(width, height, tileWidth, tileHeight);

        BuildCells();
        BuildUnit();
        FrameCamera();
    }

    private void BuildCells()
    {
        var gridRoot = new GameObject("Grid").transform;
        gridRoot.SetParent(transform);

        Sprite tileSprite = PlaceholderSprites.DiamondTile(tileWidth);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var coord = new Vector2Int(x, y);
                var go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(gridRoot);
                go.transform.position = Grid.CellToWorld(coord);

                go.AddComponent<SpriteRenderer>();
                var cell = go.AddComponent<GridCell>();
                cell.Init(coord, tileSprite, Grid.SortingOrder(coord));

                _cells[coord] = cell;
            }
        }
    }

    private void BuildUnit()
    {
        var go = new GameObject("PlayerUnit");
        go.transform.SetParent(transform);
        go.AddComponent<SpriteRenderer>();
        var unit = go.AddComponent<Unit>();
        unit.Init(unitStartCell, PlaceholderSprites.UnitToken(tileWidth), Grid);
        PlayerUnit = unit;
        _units.Add(unit);
    }

    private void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;

        Vector3 center = Grid.CenterWorld();
        cam.transform.position = new Vector3(center.x, center.y, -10f);

        // Half-extent of the diamond along each screen axis, plus padding.
        float halfWidth = (width + height) * tileWidth * 0.25f + cameraPadding;
        float halfHeight = (width + height) * tileHeight * 0.25f + cameraPadding;

        float sizeForHeight = halfHeight;
        float sizeForWidth = halfWidth / cam.aspect;
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }

    public GridCell GetCell(Vector2Int coord)
    {
        return _cells.TryGetValue(coord, out GridCell cell) ? cell : null;
    }

    /// <summary>Returns the unit currently occupying `coord`, or null if none.</summary>
    public Unit GetUnitAt(Vector2Int coord)
    {
        foreach (Unit unit in _units)
        {
            if (unit.Coord == coord) return unit;
        }
        return null;
    }
}
