/// <summary>One phase of the overall battle (whose turn it is).</summary>
public interface IBattlePhase
{
    void Enter(BattleStateMachine machine);
    void Exit(BattleStateMachine machine);
}
