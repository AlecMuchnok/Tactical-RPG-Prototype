using UnityEngine;

/// <summary>
/// Player and AI actions are the same command objects (architecture.md §4).
///
/// why: architecture.md's sample Execute()/Undo() are synchronous `void`, but
/// commands here drive cosmetic glides or combat feedback that
/// ExecutingActionState, EnemyTurnState, and AwaitingConfirmState's pre-move
/// undo all need to sequence after — Unity's idiomatic answer for "do a
/// thing, then let the caller know when it's visually done" is an
/// Awaitable-returning method. State still mutates synchronously at the top
/// of each Execute()/Undo(), before the first await, so a caller that
/// doesn't await the result still sees correct state immediately.
/// </summary>
public interface ICommand
{
    bool CanExecute();
    Awaitable Execute();
    Awaitable Undo();
}
