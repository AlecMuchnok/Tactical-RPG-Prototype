using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Drives the Highlights tilemap for range, path, attack-target, and hover
/// previews. Repaints from scratch on every mutation rather than incrementally
/// patching colours — the previous incremental model needed every mutator to
/// keep two collections in sync, and one didn't (a painted path could be
/// orphaned on screen after the range that spawned it was cleared).
/// `_paintedCells` is now the single source of truth for what's on the
/// tilemap, so nothing can be orphaned again. Each state's Enter is expected
/// to fully establish the highlight (Clear() first if it needs a clean
/// slate); Exit no longer clears.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class TileHighlighter : MonoBehaviour
{
    [SerializeField] private Tilemap _highlightTilemap;
    [SerializeField] private TileBase _highlightTile;
    [SerializeField] private Color _rangeColor = new Color(0.3f, 0.5f, 1f, 0.5f);
    [SerializeField] private Color _pathColor = new Color(1f, 0.9f, 0.3f, 0.7f);
    [SerializeField] private Color _targetColor = new Color(1f, 0.25f, 0.25f, 0.7f);
    [SerializeField] private Color _hoverColor = new Color(1f, 1f, 1f, 0.5f);

    private readonly HashSet<Vector2Int> _rangeCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> _pathCells = new List<Vector2Int>();
    private readonly List<Vector2Int> _targetCells = new List<Vector2Int>();
    private Vector2Int? _hoverCell;

    private readonly HashSet<Vector2Int> _paintedCells = new HashSet<Vector2Int>();

    // why: registered like the Systems in architecture.md §6 even though this
    // lives outside that four-system list — the plain-C# selection states
    // (StateMachines/PlayerSelection) aren't MonoBehaviours and have no scene
    // reference otherwise, so this is the only way for them to reach it.
    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<TileHighlighter>();
    }

    public void SetRange(IEnumerable<Vector2Int> cells) {
        _rangeCells.Clear();
        foreach (Vector2Int cell in cells) { _rangeCells.Add(cell); }
        Repaint();
    }

    public void SetPath(IReadOnlyList<Vector2Int> path) {
        _pathCells.Clear();
        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++) { _pathCells.Add(path[pathIndex]); }
        Repaint();
    }

    public void SetTargets(IReadOnlyList<Vector2Int> cells) {
        _targetCells.Clear();
        for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++) { _targetCells.Add(cells[cellIndex]); }
        Repaint();
    }

    public void SetHover(Vector2Int? cell) {
        _hoverCell = cell;
        Repaint();
    }

    public void Clear() {
        _rangeCells.Clear();
        _pathCells.Clear();
        _targetCells.Clear();
        _hoverCell = null;
        Repaint();
    }

    private void Repaint() {
        foreach (Vector2Int cell in _paintedCells) {
            _highlightTilemap.SetTile(ToTilemapCell(cell), null);
        }
        _paintedCells.Clear();

        foreach (Vector2Int cell in _rangeCells) { Paint(cell, _rangeColor); }
        for (int pathIndex = 0; pathIndex < _pathCells.Count; pathIndex++) { Paint(_pathCells[pathIndex], _pathColor); }
        for (int targetIndex = 0; targetIndex < _targetCells.Count; targetIndex++) { Paint(_targetCells[targetIndex], _targetColor); }
        if (_hoverCell.HasValue) { Paint(_hoverCell.Value, _hoverColor); }
    }

    private void Paint(Vector2Int cell, Color color) {
        Vector3Int cellPosition = ToTilemapCell(cell);
        _highlightTilemap.SetTile(cellPosition, _highlightTile);
        _highlightTilemap.SetTileFlags(cellPosition, TileFlags.None);
        _highlightTilemap.SetColor(cellPosition, color);
        _paintedCells.Add(cell);
    }

    // why: Tilemap's API is the only thing in this class that needs
    // Vector3Int — every other file in the project (GridManager, Pathfinder,
    // Unit.Cell, every command and state) speaks Vector2Int, so conversion is
    // confined to this one boundary method rather than storing Vector3Int
    // throughout the class.
    private static Vector3Int ToTilemapCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
}
