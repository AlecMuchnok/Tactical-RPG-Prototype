using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides one enemy unit's turn: path toward whichever player unit is
/// cheapest to reach (lowest terrain movement cost, not tile count or
/// straight-line distance), then attack if adjacent afterward. Builds the
/// same ICommand types a player action would — no separate AI combat path.
/// </summary>
[RequireComponent(typeof(Unit))]
public sealed class EnemyBrain : MonoBehaviour
{
    private Unit _unit;
    private GridManager _grid;
    private UnitRegistry _unitRegistry;

    private readonly Dictionary<Vector2Int, int> _floodCosts = new Dictionary<Vector2Int, int>();

    private void Awake() {
        _unit = GetComponent<Unit>();
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
        _unitRegistry = ServiceLocator.Get<UnitRegistry>();
    }

    /// <summary>This unit's turn: at most one move, then an attack or a wait.</summary>
    public List<ICommand> BuildTurn() {
        List<ICommand> result = new List<ICommand>();

        List<Unit> playerUnits = _unitRegistry.UnitsOnTeam(Team.Player);
        if (playerUnits.Count == 0) { return result; }

        _grid.Pathfinder.FloodCosts(_unit.Cell, int.MaxValue, _grid, _unitRegistry.IsOccupied, _floodCosts);

        Unit targetUnit = FindClosestPlayer(playerUnits, out Vector2Int bestAdjacentCell);
        if (targetUnit == null) { return result; }

        Vector2Int finalCell = _unit.Cell;
        if (_unit.Cell != bestAdjacentCell) {
            List<Vector2Int> fullPath = new List<Vector2Int>();
            _grid.Pathfinder.TryFindPath(_unit.Cell, bestAdjacentCell, _grid, _unitRegistry.IsOccupied, fullPath);
            List<Vector2Int> truncatedPath = TruncateToMovement(fullPath, _unit.Stats.Movement);
            if (truncatedPath.Count > 0) {
                result.Add(new MoveCommand(_unit, truncatedPath));
                finalCell = truncatedPath[truncatedPath.Count - 1];
            }
        }

        // finalCell and targetUnit.Cell are orthogonally adjacent (Manhattan distance 1)
        if (Mathf.Abs(finalCell.x - targetUnit.Cell.x) + Mathf.Abs(finalCell.y - targetUnit.Cell.y) == 1) {
            result.Add(new AttackCommand(_unit, targetUnit));
        } else {
            result.Add(new WaitCommand(_unit));
        }

        return result;
    }

    private Unit FindClosestPlayer(List<Unit> playerUnits, out Vector2Int bestAdjacentCell) {
        Unit bestUnit = null;
        bestAdjacentCell = default;
        int bestCost = int.MaxValue;

        foreach (Unit player in playerUnits) {
            foreach (Vector2Int offset in GridDirections.Orthogonal) {
                Vector2Int neighbor = player.Cell + offset;
                if (!_floodCosts.TryGetValue(neighbor, out int cost)) { continue; }
                if (cost < bestCost) {
                    bestCost = cost;
                    bestAdjacentCell = neighbor;
                    bestUnit = player;
                }
            }
        }

        return bestUnit;
    }

    private List<Vector2Int> TruncateToMovement(List<Vector2Int> fullPath, int movement) {
        List<Vector2Int> result = new List<Vector2Int>();
        int cumulativeCost = 0;
        foreach (Vector2Int step in fullPath) {
            int stepCost = _grid.MovementCost(step);
            if (cumulativeCost + stepCost > movement) { break; }
            cumulativeCost += stepCost;
            result.Add(step);
        }
        return result;
    }
}
