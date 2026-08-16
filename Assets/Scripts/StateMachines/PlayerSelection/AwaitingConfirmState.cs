using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pending destination: pre-moves the unit there (skipped on
/// re-entry from SelectingTargetState, since the same MoveCommand is already
/// executed), then shows the Wait/Attack menu. Right-click undoes the move
/// and returns to selection; choosing Attack routes to target selection
/// without moving again.
/// </summary>
public sealed class AwaitingConfirmState : ISelectionState
{
    private readonly Unit _unit;
    private readonly MoveCommand _moveCommand;
    private readonly List<Unit> _scratchOpponents = new List<Unit>();
    private readonly List<BattleAction> _actions = new List<BattleAction>();

    private bool _isBusy;

    public AwaitingConfirmState(Unit unit, MoveCommand moveCommand) {
        _unit = unit;
        _moveCommand = moveCommand;
    }

    public void Enter(SelectionStateMachine machine) {
        // why: fire-and-forget from a synchronous Enter() — the pre-move
        // glide (if any) must finish before the menu can appear, and
        // OnCancelled's _isBusy guard prevents a right-click from racing it.
        _ = EnterAsync(machine);
    }

    private async Awaitable EnterAsync(SelectionStateMachine machine) {
        machine.Highlighter.Clear();

        if (_moveCommand.CanExecute()) {
            _isBusy = true;
            await _moveCommand.Execute();
            _isBusy = false;
        }

        // why: the board stays completely clear while the menu is open — the
        // unit is already standing on the destination, so leaving the path
        // painted underneath just reads as leftover UI.
        ShowMenu(machine);
    }

    public void Exit(SelectionStateMachine machine) {
        machine.ActionMenu.Hide();
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
    }

    public void OnCancelled(SelectionStateMachine machine) {
        if (_isBusy) { return; }
        _ = UndoAndReturnAsync(machine);
    }

    private async Awaitable UndoAndReturnAsync(SelectionStateMachine machine) {
        _isBusy = true;
        machine.ActionMenu.Hide();
        await _moveCommand.Undo();
        _isBusy = false;
        machine.ChangeState(new UnitSelectedState(_unit));
    }

    private void ShowMenu(SelectionStateMachine machine) {
        _actions.Clear();
        _actions.Add(BattleAction.Wait);

        machine.Combat.FindAdjacentOpponents(_unit.Cell, _unit.Team, _scratchOpponents);
        if (!_unit.HasActed && _scratchOpponents.Count > 0) {
            _actions.Add(BattleAction.Attack);
        }

        machine.ActionMenu.Show(_unit.Cell, _actions, action => OnActionChosen(machine, action));
    }

    private void OnActionChosen(SelectionStateMachine machine, BattleAction action) {
        if (action == BattleAction.Attack) {
            machine.ChangeState(new SelectingTargetState(_unit, _moveCommand));
        } else {
            List<ICommand> commands = new List<ICommand> { new WaitCommand(_unit) };
            machine.ChangeState(new ExecutingActionState(commands));
        }
    }
}
