using UnityEngine;

/// <summary>Root state: nothing selected. Highlights the hovered tile; clicking an own, not-yet-done unit selects it.</summary>
public sealed class AwaitingSelectionState : ISelectionState
{
    public void Enter(SelectionStateMachine machine) {
        machine.Highlighter.Clear();
        machine.Highlighter.SetHover(machine.Input.HoveredCell);
    }

    public void Exit(SelectionStateMachine machine) {
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
        machine.Highlighter.SetHover(cell);
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
        Unit unit = machine.UnitRegistry.GetUnitAt(cell);
        if (unit == null || unit.Team != Team.Player || unit.IsDone) { return; }

        machine.ChangeState(new UnitSelectedState(unit));
    }

    public void OnCancelled(SelectionStateMachine machine) {
    }
}
