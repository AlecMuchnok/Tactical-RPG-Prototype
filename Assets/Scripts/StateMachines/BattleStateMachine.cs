public sealed class BattleStateMachine
{
    private IBattlePhase _current;

    public void ChangeState(IBattlePhase next) {
        _current?.Exit(this);
        _current = next;
        _current?.Enter(this);
    }
}
