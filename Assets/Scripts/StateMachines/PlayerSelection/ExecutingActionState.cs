using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the confirmed command sequence with the board unresponsive (every
/// handler here is a no-op), then returns to selection or ends the turn. The
/// unit's move (if any) already ran in AwaitingConfirmState's pre-move, so
/// `_commands` here holds only the remaining Wait or Attack.
/// </summary>
public sealed class ExecutingActionState : ISelectionState
{
    private readonly List<ICommand> _commands;

    public ExecutingActionState(List<ICommand> commands) {
        _commands = commands;
    }

    public void Enter(SelectionStateMachine machine) {
        machine.Highlighter.Clear();
        // why: fire-and-forget from a synchronous Enter() — cancellation from
        // a mid-glide destroy is already caught inside UnitView.PlayMoveAsync,
        // but WaitUntilClearAsync below adds a new throw site (play mode
        // exiting while a popup is mid-float), so this now needs its own catch.
        _ = RunAsync(machine);
    }

    private async Awaitable RunAsync(SelectionStateMachine machine) {
        try {
            for (int commandIndex = 0; commandIndex < _commands.Count; commandIndex++) {
                ICommand command = _commands[commandIndex];
                if (command.CanExecute()) {
                    await command.Execute();
                }
            }

            // why: holds the turn open until any fire-and-forget feedback
            // (damage popups today) finishes, so the next unit's turn can't
            // start while a popup from this action is still on screen.
            await machine.Locks.WaitUntilClearAsync();
        } catch (System.OperationCanceledException) {
            return;
        }

        if (machine.AllPlayerUnitsDone()) {
            machine.CompleteTurn();
        } else {
            machine.ChangeState(new AwaitingSelectionState());
        }
    }

    public void Exit(SelectionStateMachine machine) {
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
    }

    public void OnCancelled(SelectionStateMachine machine) {
    }
}
