using UnityEngine;

/// <summary>Read-only terrain query Pathfinder needs, implemented by GridManager — kept as a tiny interface so Pathfinder doesn't depend on a MonoBehaviour.</summary>
public interface ITerrainCostSource
{
    bool Contains(Vector2Int cell);
    int MovementCost(Vector2Int cell);
}
