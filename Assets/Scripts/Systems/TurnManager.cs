using UnityEngine;

/// <summary>Owns the battle-phase state machine and whose turn it currently is.</summary>
public sealed class TurnManager : MonoBehaviour
{
    [SerializeField] private TeamEventChannel _turnChangedChannel;

    private BattleStateMachine _stateMachine;

    public Team CurrentTeam { get; private set; } = Team.Player;

    private void Awake() {
        ServiceLocator.Register(this);
        _stateMachine = new BattleStateMachine();
    }

    private void Start() {
        _turnChangedChannel.Raise(Team.Player);
        _stateMachine.ChangeState(new PlayerTurnState());
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<TurnManager>();
    }

    public void EndTurn() {
        CurrentTeam = CurrentTeam == Team.Player ? Team.Enemy : Team.Player;
        _turnChangedChannel.Raise(CurrentTeam);

        if (CurrentTeam == Team.Enemy) {
            _stateMachine.ChangeState(new EnemyTurnState());
        } else {
            _stateMachine.ChangeState(new PlayerTurnState());
        }
    }
}
