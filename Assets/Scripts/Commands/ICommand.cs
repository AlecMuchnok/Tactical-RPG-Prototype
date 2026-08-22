using UnityEngine;

/// <summary>
/// Player and AI actions are the same command objects. Execute()/Undo()
/// return Awaitable rather than void because commands here drive cosmetic
/// glides or combat feedback that callers need to sequence after — state
/// still mutates synchronously at the top of each method, before the first
/// await, so a caller that doesn't await still sees correct state immediately.
/// </summary>
public interface ICommand
{
    bool CanExecute();
    Awaitable Execute();
    Awaitable Undo();
}
