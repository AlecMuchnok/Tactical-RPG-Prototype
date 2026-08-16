using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides one enemy unit's turn: path toward whichever player unit is
/// cheapest to reach (lowest terrain movement cost, not tile count or
/// straight-line distance), then attack if adjacent afterward. Builds the
/// same ICommand types a player action would — no separate AI combat path
/// (architecture.md §4).
/// </summary>
[RequireComponent(typeof(Unit))]
public sealed class EnemyBrain : MonoBehaviour
{
    private Unit _unit;
    private GridManager _grid;
    private UnitRegistry _unitRegistry;

    private readonly Dictionary<Vector2Int, float> _floodCosts = new Dictionary<Vector2Int, float>();
    private readonly List<Vector2Int> _fullPath = new List<Vector2Int>();
    private readonly List<Vector2Int> _truncatedPath = new List<Vector2Int>();
    private readonly List<Unit> _playerUnits = new List<Unit>();

    private static readonly Vector2Int[] NeighborOffsets = new Vector2Int[]
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    private void Awake() {
        _unit = GetComponent<Unit>();
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
        _unitRegistry = ServiceLocator.Get<UnitRegistry>();
    }

    /// <summary>Fills `result` (cleared first) with this unit's turn: at most one move, then an attack or a wait.</summary>
    public void BuildTurn(List<ICommand> result) {
        result.Clear();

        _unitRegistry.UnitsOnTeam(Team.Player, _playerUnits);
        if (_playerUnits.Count == 0) { return; }

        bool IsBlocked(Vector2Int cell) => _unitRegistry.GetUnitAt(cell) != null;

        _grid.Pathfinder.FloodCosts(_unit.Cell, float.MaxValue, _grid, IsBlocked, _floodCosts);

        Unit targetUnit = FindClosestPlayer(out Vector2Int bestAdjacentCell);
        if (targetUnit == null) { return; }

        Vector2Int finalCell = _unit.Cell;
        if (_unit.Cell != bestAdjacentCell) {
            _grid.Pathfinder.TryFindPath(_unit.Cell, bestAdjacentCell, _grid, IsBlocked, _fullPath);
            TruncateToMovement(_fullPath, _unit.Stats.Movement, _truncatedPath);
            if (_truncatedPath.Count > 0) {
                result.Add(new MoveCommand(_unit, new List<Vector2Int>(_truncatedPath)));
                finalCell = _truncatedPath[_truncatedPath.Count - 1];
            }
        }

        if (IsAdjacent(finalCell, targetUnit.Cell)) {
            result.Add(new AttackCommand(_unit, targetUnit));
        } else {
            result.Add(new WaitCommand(_unit));
        }
    }

    private Unit FindClosestPlayer(out Vector2Int bestAdjacentCell) {
        Unit bestUnit = null;
        bestAdjacentCell = default;
        float bestCost = float.MaxValue;

        for (int playerIndex = 0; playerIndex < _playerUnits.Count; playerIndex++) {
            Unit player = _playerUnits[playerIndex];
            for (int offsetIndex = 0; offsetIndex < NeighborOffsets.Length; offsetIndex++) {
                Vector2Int neighbor = player.Cell + NeighborOffsets[offsetIndex];
                if (!_floodCosts.TryGetValue(neighbor, out float cost)) { continue; }
                if (cost < bestCost) {
                    bestCost = cost;
                    bestAdjacentCell = neighbor;
                    bestUnit = player;
                }
            }
        }

        return bestUnit;
    }

    private void TruncateToMovement(List<Vector2Int> fullPath, int movement, List<Vector2Int> result) {
        result.Clear();
        float cumulativeCost = 0f;
        for (int stepIndex = 0; stepIndex < fullPath.Count; stepIndex++) {
            float stepCost = _grid.MovementCost(fullPath[stepIndex]);
            if (cumulativeCost + stepCost > movement) { break; }
            cumulativeCost += stepCost;
            result.Add(fullPath[stepIndex]);
        }
    }

    private static bool IsAdjacent(Vector2Int cellA, Vector2Int cellB) {
        return Mathf.Abs(cellA.x - cellB.x) + Mathf.Abs(cellA.y - cellB.y) == 1;
    }
}
