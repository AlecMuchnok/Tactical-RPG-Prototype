using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A unit is selected: shows its reachable range (0 if it already moved this
/// turn — Dijkstra naturally collapses to just the unit's own cell at budget
/// 0, so no special-casing is needed), the hovered tile, and the path preview
/// to the hovered tile when it's reachable.
/// </summary>
public sealed class UnitSelectedState : ISelectionState
{
    private static readonly List<Vector2Int> EmptyPath = new List<Vector2Int>();

    private readonly Unit _unit;
    private readonly Dictionary<Vector2Int, int> _rangeCosts = new Dictionary<Vector2Int, int>();
    private readonly List<Vector2Int> _previewPath = new List<Vector2Int>();

    public UnitSelectedState(Unit unit) {
        _unit = unit;
    }

    public void Enter(SelectionStateMachine machine) {
        int movementBudget = _unit.HasMoved ? 0 : _unit.Class.Movement;
        machine.Grid.Pathfinder.FloodCosts(_unit.Cell, movementBudget, machine.Grid, machine.UnitRegistry.IsOccupied, _rangeCosts);

        machine.Highlighter.Clear();
        machine.Highlighter.SetRange(_rangeCosts.Keys);
        _unit.View.SetSelected(true);

        UpdateHoverPath(machine, machine.Input.HoveredCell);
    }

    public void Exit(SelectionStateMachine machine) {
        _unit.View.SetSelected(false);
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
        UpdateHoverPath(machine, cell);
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
        if (cell == _unit.Cell) {
            machine.ChangeState(new AwaitingConfirmState(_unit, new MoveCommand(_unit, new List<Vector2Int>())));
            return;
        }

        if (!_rangeCosts.ContainsKey(cell)) { return; }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!machine.Grid.Pathfinder.TryFindPath(_unit.Cell, cell, machine.Grid, machine.UnitRegistry.IsOccupied, path)) { return; }

        machine.ChangeState(new AwaitingConfirmState(_unit, new MoveCommand(_unit, path)));
    }

    public void OnCancelled(SelectionStateMachine machine) {
        machine.ChangeState(new AwaitingSelectionState());
    }

    private void UpdateHoverPath(SelectionStateMachine machine, Vector2Int? cell) {
        machine.Highlighter.SetHover(cell);

        if (!cell.HasValue || cell.Value == _unit.Cell || !_rangeCosts.ContainsKey(cell.Value)) {
            machine.Highlighter.SetPath(EmptyPath);
            return;
        }

        machine.Grid.Pathfinder.TryFindPath(_unit.Cell, cell.Value, machine.Grid, machine.UnitRegistry.IsOccupied, _previewPath);
        machine.Highlighter.SetPath(_previewPath);
    }
}
