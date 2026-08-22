using UnityEngine;

/// <summary>The four orthogonal neighbor offsets, shared by every system that walks grid adjacency.</summary>
public static class GridDirections
{
    public static readonly Vector2Int[] Orthogonal = new Vector2Int[]
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };
}
