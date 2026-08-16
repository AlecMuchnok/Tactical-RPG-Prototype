using UnityEngine;

/// <summary>One state in the player's select/hover/confirm interaction flow. Event-driven rather than ticked — every transition is a response to input.</summary>
public interface ISelectionState
{
    void Enter(SelectionStateMachine machine);
    void Exit(SelectionStateMachine machine);
    void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell);
    void OnCellClicked(SelectionStateMachine machine, Vector2Int cell);
    void OnCancelled(SelectionStateMachine machine);
}
