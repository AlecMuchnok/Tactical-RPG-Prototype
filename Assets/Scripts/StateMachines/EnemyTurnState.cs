using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs every enemy unit's brain-built turn in sequence, then hands the turn back to the player.</summary>
public sealed class EnemyTurnState : IBattlePhase
{
    private readonly List<Unit> _scratchUnits = new List<Unit>();
    private readonly List<ICommand> _scratchCommands = new List<ICommand>();

    public void Enter(BattleStateMachine machine) {
        // why: fire-and-forget from a synchronous Enter() — cancellation from
        // a mid-glide destroy is already caught inside UnitView.PlayMoveAsync,
        // but WaitUntilClearAsync below adds a new throw site (play mode
        // exiting while a popup is mid-float), so this now needs its own catch.
        _ = RunAsync();
    }

    private async Awaitable RunAsync() {
        UnitRegistry unitRegistry = ServiceLocator.Get<UnitRegistry>();
        BattleLocks locks = ServiceLocator.Get<BattleLocks>();
        unitRegistry.UnitsOnTeam(Team.Enemy, _scratchUnits);

        try {
            for (int unitIndex = 0; unitIndex < _scratchUnits.Count; unitIndex++) {
                Unit enemy = _scratchUnits[unitIndex];
                if (enemy.Brain == null) { continue; }

                enemy.ResetForTurn();
                enemy.Brain.BuildTurn(_scratchCommands);
                for (int commandIndex = 0; commandIndex < _scratchCommands.Count; commandIndex++) {
                    ICommand command = _scratchCommands[commandIndex];
                    if (command.CanExecute()) {
                        await command.Execute();
                    }
                }

                // why: holds this enemy's turn open until its own combat
                // feedback finishes, so two enemies attacking in sequence
                // don't stack their popups on top of each other.
                await locks.WaitUntilClearAsync();
            }
        } catch (System.OperationCanceledException) {
            return;
        }

        ServiceLocator.Get<TurnManager>().EndTurn();
    }

    public void Tick(BattleStateMachine machine, float deltaTime) {
    }

    public void Exit(BattleStateMachine machine) {
    }
}
