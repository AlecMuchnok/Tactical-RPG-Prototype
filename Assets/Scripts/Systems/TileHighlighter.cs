using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Drives the Highlights tilemap for range, path, attack-target, and hover
/// previews. Repaints from scratch on every mutation; `_paintedCells` is the
/// single source of truth for what's currently on the tilemap, so a stale
/// layer can never be left painted after it's cleared. Each state's Enter is
/// expected to fully establish the highlight it wants (calling Clear() first
/// if it needs a clean slate) — Exit does not clear.
/// </summary>
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

    // Registered like a Systems-tier service even though this file lives
    // elsewhere — the plain-C# selection states have no scene reference of
    // their own otherwise, so this is the only way for them to reach it.
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
        foreach (Vector2Int cell in path) { _pathCells.Add(cell); }
        Repaint();
    }

    public void SetTargets(IReadOnlyList<Vector2Int> cells) {
        _targetCells.Clear();
        foreach (Vector2Int cell in cells) { _targetCells.Add(cell); }
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
        foreach (Vector2Int cell in _pathCells) { Paint(cell, _pathColor); }
        foreach (Vector2Int cell in _targetCells) { Paint(cell, _targetColor); }
        if (_hoverCell.HasValue) { Paint(_hoverCell.Value, _hoverColor); }
    }

    private void Paint(Vector2Int cell, Color color) {
        Vector3Int cellPosition = ToTilemapCell(cell);
        _highlightTilemap.SetTile(cellPosition, _highlightTile);
        _highlightTilemap.SetTileFlags(cellPosition, TileFlags.None);
        _highlightTilemap.SetColor(cellPosition, color);
        _paintedCells.Add(cell);
    }

    // Tilemap's API is the only thing in this class that needs Vector3Int —
    // everything else here speaks Vector2Int, so the conversion is confined
    // to this one boundary method.
    private static Vector3Int ToTilemapCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
}
