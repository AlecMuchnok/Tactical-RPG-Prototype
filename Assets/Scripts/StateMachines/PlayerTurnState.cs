using System.Collections.Generic;

/// <summary>Resets every player unit's turn state, then drives the player-selection machine until every unit is done.</summary>
public sealed class PlayerTurnState : IBattlePhase
{
    private SelectionStateMachine _selectionMachine;
    private BattleInputView _inputView;

    public void Enter(BattleStateMachine machine) {
        UnitRegistry unitRegistry = ServiceLocator.Get<UnitRegistry>();
        List<Unit> playerUnits = unitRegistry.UnitsOnTeam(Team.Player);
        foreach (Unit unit in playerUnits) {
            unit.ResetForTurn();
        }

        GridManager grid = ServiceLocator.Get<GridManager>();
        TileHighlighter highlighter = ServiceLocator.Get<TileHighlighter>();
        ActionMenuPresenter actionMenu = ServiceLocator.Get<ActionMenuPresenter>();
        CombatSystem combat = ServiceLocator.Get<CombatSystem>();
        TurnManager turnManager = ServiceLocator.Get<TurnManager>();
        BattleLocks locks = ServiceLocator.Get<BattleLocks>();
        _inputView = ServiceLocator.Get<BattleInputView>();

        _selectionMachine = new SelectionStateMachine(grid, unitRegistry, highlighter, actionMenu, combat, _inputView, locks, turnManager.EndTurn);
        _selectionMachine.ChangeState(new AwaitingSelectionState());

        _inputView.CellHovered += _selectionMachine.HandleCellHovered;
        _inputView.CellClicked += _selectionMachine.HandleCellClicked;
        _inputView.Cancelled += _selectionMachine.HandleCancelled;
    }

    public void Tick(BattleStateMachine machine, float deltaTime) {
    }

    public void Exit(BattleStateMachine machine) {
        _inputView.CellHovered -= _selectionMachine.HandleCellHovered;
        _inputView.CellClicked -= _selectionMachine.HandleCellClicked;
        _inputView.Cancelled -= _selectionMachine.HandleCancelled;
        _selectionMachine.ChangeState(null);
    }
}
