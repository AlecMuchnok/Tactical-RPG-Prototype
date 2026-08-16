using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attack was chosen: highlights adjacent opponents in red and waits for the
/// player to click one. Right-click returns to the Wait/Attack menu without
/// moving the unit again — AwaitingConfirmState's own MoveCommand.CanExecute()
/// check (it's already executed) makes that automatic.
/// </summary>
public sealed class SelectingTargetState : ISelectionState
{
    private readonly Unit _unit;
    private readonly MoveCommand _moveCommand;
    private readonly List<Unit> _opponents = new List<Unit>();
    private readonly List<Vector2Int> _targetCells = new List<Vector2Int>();

    public SelectingTargetState(Unit unit, MoveCommand moveCommand) {
        _unit = unit;
        _moveCommand = moveCommand;
    }

    public void Enter(SelectionStateMachine machine) {
        // why: no path here either — the unit has already arrived at the
        // destination, so only the targets it can actually strike are worth
        // drawing.
        machine.Highlighter.Clear();

        machine.Combat.FindAdjacentOpponents(_unit.Cell, _unit.Team, _opponents);
        _targetCells.Clear();
        for (int opponentIndex = 0; opponentIndex < _opponents.Count; opponentIndex++) {
            _targetCells.Add(_opponents[opponentIndex].Cell);
        }
        machine.Highlighter.SetTargets(_targetCells);
        machine.Highlighter.SetHover(machine.Input.HoveredCell);
    }

    public void Exit(SelectionStateMachine machine) {
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
        machine.Highlighter.SetHover(cell);
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
        Unit target = FindOpponentAt(cell);
        if (target == null) { return; }

        List<ICommand> commands = new List<ICommand> { new AttackCommand(_unit, target) };
        machine.ChangeState(new ExecutingActionState(commands));
    }

    public void OnCancelled(SelectionStateMachine machine) {
        machine.ChangeState(new AwaitingConfirmState(_unit, _moveCommand));
    }

    private Unit FindOpponentAt(Vector2Int cell) {
        for (int opponentIndex = 0; opponentIndex < _opponents.Count; opponentIndex++) {
            if (_opponents[opponentIndex].Cell == cell) { return _opponents[opponentIndex]; }
        }
        return null;
    }
}
