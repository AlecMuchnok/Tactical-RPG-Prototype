using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the player's select/hover/confirm/execute flow for one player turn.
/// Carries the shared services its states need (none of them are
/// MonoBehaviours, so they have no scene reference otherwise) and a callback
/// into PlayerTurnState for when every player unit is done.
/// </summary>
public sealed class SelectionStateMachine
{
    public GridManager Grid { get; }
    public UnitRegistry UnitRegistry { get; }
    public TileHighlighter Highlighter { get; }
    public ActionMenuPresenter ActionMenu { get; }
    public CombatSystem Combat { get; }
    public BattleInputView Input { get; }
    public BattleLocks Locks { get; }

    private readonly Action _onTurnComplete;
    private readonly List<Unit> _scratchUnits = new List<Unit>();
    private ISelectionState _current;

    public SelectionStateMachine(GridManager grid, UnitRegistry unitRegistry, TileHighlighter highlighter, ActionMenuPresenter actionMenu, CombatSystem combat, BattleInputView inputView, BattleLocks locks, Action onTurnComplete) {
        Grid = grid;
        UnitRegistry = unitRegistry;
        Highlighter = highlighter;
        ActionMenu = actionMenu;
        Combat = combat;
        Input = inputView;
        Locks = locks;
        _onTurnComplete = onTurnComplete;
    }

    public void ChangeState(ISelectionState next) {
        _current?.Exit(this);
        _current = next;
        _current?.Enter(this);
    }

    public void HandleCellHovered(Vector2Int? cell) => _current?.OnCellHovered(this, cell);
    public void HandleCellClicked(Vector2Int cell) => _current?.OnCellClicked(this, cell);
    public void HandleCancelled() => _current?.OnCancelled(this);

    public bool AllPlayerUnitsDone() {
        UnitRegistry.UnitsOnTeam(Team.Player, _scratchUnits);
        for (int unitIndex = 0; unitIndex < _scratchUnits.Count; unitIndex++) {
            if (!_scratchUnits[unitIndex].IsDone) { return false; }
        }
        return true;
    }

    public void CompleteTurn() {
        _onTurnComplete?.Invoke();
    }
}
