using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single player unit: its grid coordinate, selection visuals, and an
/// orthogonal (no-diagonal) L-shaped glide to a target cell.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Unit : MonoBehaviour
{
    public Vector2Int Coord { get; private set; }

    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    /// <summary>Which grid axis to traverse first when building an L-shaped path.</summary>
    [SerializeField] private bool horizontalFirst = true;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void Init(Vector2Int coord, Sprite sprite, IsoGrid grid)
    {
        Coord = coord;
        _renderer.sprite = sprite;
        _renderer.color = normalColor;
        transform.position = grid.CellToWorld(coord);
        _renderer.sortingOrder = grid.SortingOrder(coord) + 5;
    }

    public void SetSelected(bool selected)
    {
        _renderer.color = selected ? selectedColor : normalColor;
    }

    /// <summary>
    /// Builds an orthogonal path from `from` to `to`: every step changes
    /// exactly one grid axis by one, never both at once (no diagonals).
    /// Traverses the axis chosen by `horizontalFirst` to completion before
    /// switching to the other, producing a single L-shaped corner.
    /// </summary>
    public List<Vector2Int> BuildPath(Vector2Int from, Vector2Int to)
    {
        var path = new List<Vector2Int>();
        Vector2Int current = from;

        int Sign(int a, int b) => a < b ? 1 : (a > b ? -1 : 0);

        void WalkX()
        {
            int step = Sign(current.x, to.x);
            while (current.x != to.x)
            {
                current = new Vector2Int(current.x + step, current.y);
                path.Add(current);
            }
        }

        void WalkY()
        {
            int step = Sign(current.y, to.y);
            while (current.y != to.y)
            {
                current = new Vector2Int(current.x, current.y + step);
                path.Add(current);
            }
        }

        if (horizontalFirst)
        {
            WalkX();
            WalkY();
        }
        else
        {
            WalkY();
            WalkX();
        }

        return path;
    }

    /// <summary>
    /// Glides along the orthogonal path to `target`, updating Coord and
    /// sorting order as each waypoint is entered. Invokes `onComplete` when
    /// the unit arrives (or immediately if already there).
    /// </summary>
    public IEnumerator MoveTo(Vector2Int target, IsoGrid grid, Action onComplete)
    {
        if (!grid.Contains(target))
        {
            onComplete?.Invoke();
            yield break;
        }

        List<Vector2Int> path = BuildPath(Coord, target);
        if (path.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        foreach (Vector2Int waypoint in path)
        {
            Vector3 destination = grid.CellToWorld(waypoint);
            while (transform.position != destination)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
                // Re-derive sorting order from the interpolated position, not the
                // waypoint, so depth flips exactly at the diamond boundary instead
                // of only once the unit fully arrives (which drew it under the
                // next tile for the whole leg of any forward move).
                _renderer.sortingOrder = grid.SortingOrder(grid.WorldToCell(transform.position)) + 5;
                yield return null;
            }
            Coord = waypoint;
            _renderer.sortingOrder = grid.SortingOrder(waypoint) + 5;
        }

        onComplete?.Invoke();
    }
}
