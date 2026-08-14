/// <summary>One phase of the overall battle (whose turn it is). Deliberately minimal — matches architecture.md §3's IUnitState shape.</summary>
public interface IBattlePhase
{
    void Enter(BattleStateMachine machine);
    void Tick(BattleStateMachine machine, float deltaTime);
    void Exit(BattleStateMachine machine);
}
