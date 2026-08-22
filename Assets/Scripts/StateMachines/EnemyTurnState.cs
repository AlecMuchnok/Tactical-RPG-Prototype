using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs every enemy unit's brain-built turn in sequence, then hands the turn back to the player.</summary>
public sealed class EnemyTurnState : IBattlePhase
{
    public void Enter(BattleStateMachine machine) {
        // Fire-and-forget from a synchronous Enter() — cancellation from a
        // mid-glide destroy is already caught inside UnitView.PlayMoveAsync,
        // but WaitUntilClearAsync below adds a new throw site (play mode
        // exiting while a popup is mid-float), so this needs its own catch.
        _ = RunAsync();
    }

    private async Awaitable RunAsync() {
        UnitRegistry unitRegistry = ServiceLocator.Get<UnitRegistry>();
        BattleLocks locks = ServiceLocator.Get<BattleLocks>();
        List<Unit> enemies = unitRegistry.UnitsOnTeam(Team.Enemy);

        try {
            foreach (Unit enemy in enemies) {
                EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                if (brain == null) { continue; }

                enemy.ResetForTurn();
                List<ICommand> commands = brain.BuildTurn();
                foreach (ICommand command in commands) {
                    if (command.CanExecute()) {
                        await command.Execute();
                    }
                }

                // Holds this enemy's turn open until its own combat feedback
                // finishes, so two enemies attacking in sequence don't stack
                // their popups on top of each other.
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
