using UnityEngine;

/// <summary>Ends a unit's turn outright — latches HasActed regardless of prior state.</summary>
public sealed class WaitCommand : ICommand
{
    private readonly Unit _unit;

    public WaitCommand(Unit unit) {
        _unit = unit;
    }

    public bool CanExecute() => true;

    public async Awaitable Execute() {
        _unit.MarkActed();
    }

    // Wait is the terminal choice in the confirm menu — nothing follows it
    // to cancel back out of, so Undo() is never called in practice, but the
    // interface still requires an implementation.
    public async Awaitable Undo() {
    }
}
