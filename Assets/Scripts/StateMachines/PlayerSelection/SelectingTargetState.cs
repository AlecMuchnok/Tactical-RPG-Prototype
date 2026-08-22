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
        // No path shown — the unit has already arrived, so only the targets
        // it can strike are worth drawing.
        machine.Highlighter.Clear();

        _opponents.Clear();
        _opponents.AddRange(machine.Combat.FindAdjacentOpponents(_unit.Cell, _unit.Team));
        _targetCells.Clear();
        foreach (Unit opponent in _opponents) {
            _targetCells.Add(opponent.Cell);
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
        foreach (Unit opponent in _opponents) {
            if (opponent.Cell == cell) { return opponent; }
        }
        return null;
    }
}
