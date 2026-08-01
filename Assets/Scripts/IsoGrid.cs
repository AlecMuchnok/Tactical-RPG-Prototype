using UnityEngine;

/// <summary>
/// Pure math for a 2:1 isometric grid: cell&lt;-&gt;world conversion, bounds, and
/// depth sorting. Not a MonoBehaviour so anything (GridManager, editor tools,
/// tests) can construct one without touching the scene.
/// </summary>
public class IsoGrid
{
    public int Width { get; }
    public int Height { get; }
    public float TileWidth { get; }
    public float TileHeight { get; }

    public IsoGrid(int width, int height, float tileWidth = 1f, float tileHeight = 0.5f)
    {
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    /// <summary>Grid cell -> world position of the cell's center (z = 0).</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float x = (cell.x - cell.y) * TileWidth * 0.5f;
        float y = (cell.x + cell.y) * -TileHeight * 0.5f;
        return new Vector3(x, y, 0f);
    }

    /// <summary>World position -> the grid cell whose diamond contains it.</summary>
    public Vector2Int WorldToCell(Vector3 world)
    {
        float a = world.x / (TileWidth * 0.5f);   // == cx - cy
        float b = world.y / (-TileHeight * 0.5f); // == cx + cy

        float cx = (a + b) * 0.5f;
        float cy = (b - a) * 0.5f;

        // +0.5 before flooring rounds to the nearest cell center, which is
        // exactly the diamond hit-test we need with no colliders/raycasts.
        int ix = Mathf.FloorToInt(cx + 0.5f);
        int iy = Mathf.FloorToInt(cy + 0.5f);
        return new Vector2Int(ix, iy);
    }

    public bool Contains(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    /// <summary>
    /// Renderer sorting order for a cell. Cells further "back" (smaller x+y)
    /// draw first. Units should use SortingOrder(cell) + 5 to render above
    /// their own tile but behind tiles in front of them.
    /// </summary>
    public int SortingOrder(Vector2Int cell)
    {
        return (cell.x + cell.y) * 10;
    }

    /// <summary>World-space center of the whole grid, useful for framing a camera.</summary>
    public Vector3 CenterWorld()
    {
        Vector2 centerCell = new Vector2((Width - 1) * 0.5f, (Height - 1) * 0.5f);
        float x = (centerCell.x - centerCell.y) * TileWidth * 0.5f;
        float y = (centerCell.x + centerCell.y) * -TileHeight * 0.5f;
        return new Vector3(x, y, 0f);
    }
}
