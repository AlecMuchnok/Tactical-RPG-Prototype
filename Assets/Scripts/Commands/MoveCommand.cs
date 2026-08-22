using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves a unit along a precomputed path, and can undo that move.</summary>
public sealed class MoveCommand : ICommand
{
    private readonly Unit _unit;
    private readonly List<Vector2Int> _path;
    private readonly List<Vector2Int> _returnPath = new List<Vector2Int>();

    private Vector2Int _previousCell;

    public bool IsExecuted { get; private set; }

    public MoveCommand(Unit unit, List<Vector2Int> path) {
        _unit = unit;
        _path = path;
    }

    public bool CanExecute() => !IsExecuted && !_unit.HasMoved && _path.Count > 0;

    public async Awaitable Execute() {
        _previousCell = _unit.Cell;
        _unit.SetCell(_path[_path.Count - 1]);
        _unit.MarkMoved();
        IsExecuted = true;
        await _unit.View.PlayMoveAsync(_path);
    }

    // Reverses the confirmed path in place — a real feature now that a unit
    // can pre-move onto a destination before the player commits to an action
    // (AwaitingConfirmState / SelectingTargetState).
    public async Awaitable Undo() {
        if (!IsExecuted) { return; }

        BuildReturnPath();
        _unit.SetCell(_previousCell);
        // MoveCommand only ever executes when !_unit.HasMoved (its own
        // CanExecute), so clearing is always the correct inverse of the
        // MarkMoved() that Execute() applied — no need to snapshot the prior
        // flag value.
        _unit.ClearMoved();
        IsExecuted = false;
        await _unit.View.PlayMoveAsync(_returnPath);
    }

    private void BuildReturnPath() {
        _returnPath.Clear();
        for (int pathIndex = _path.Count - 2; pathIndex >= 0; pathIndex--) {
            _returnPath.Add(_path[pathIndex]);
        }
        _returnPath.Add(_previousCell);
    }
}
