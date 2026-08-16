using System;
using UnityEngine;

/// <summary>
/// Identity, position, and per-turn action state for one unit. Condensed
/// component design (plan §4): Health and UnitView are the only siblings —
/// there is no separate Mover/Attacker/TurnActions, since every unit in this
/// slice has identical capability and splitting two bools and a delegate
/// call into their own MonoBehaviours bought nothing.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(UnitView))]
public sealed class Unit : MonoBehaviour
{
    [SerializeField] private Team _team;
    [SerializeField] private UnitStats _stats;
    [SerializeField] private Vector2Int _startCell;

    private UnitRegistry _unitRegistry;

    public Team Team => _team;
    public UnitStats Stats => _stats;
    public Vector2Int Cell { get; private set; }
    public Health Health { get; private set; }
    public UnitView View { get; private set; }
    /// <summary>Null on player units — only the enemy prefab carries this component.</summary>
    public EnemyBrain Brain { get; private set; }
    public bool HasMoved { get; private set; }
    public bool HasActed { get; private set; }
    // why: acting (Wait or Attack) ends the unit's turn outright — HasMoved no
    // longer factors in. It still exists to stop a second move and to
    // collapse the move range to 0 once used (UnitSelectedState.Enter).
    public bool IsDone => HasActed;

    public event Action<Vector2Int> CellChanged;

    private void Awake() {
        Health = GetComponent<Health>();
        View = GetComponent<UnitView>();
        Brain = GetComponent<EnemyBrain>();
        Health.Initialize(_stats.MaxHealth);
        Health.Died += OnDied;
        // why: set directly (not via SetCell) so it's ready before any
        // Start() runs — UnitView.Start reads Cell to place itself, and
        // Start-phase ordering between sibling components is undefined, but
        // the whole scene's Awake phase always completes before any Start.
        Cell = _startCell;
    }

    private void Start() {
        _unitRegistry = ServiceLocator.Get<UnitRegistry>();
        _unitRegistry.Register(this);
    }

    private void OnDestroy() {
        if (Health != null) { Health.Died -= OnDied; }
        if (_unitRegistry != null) { _unitRegistry.Unregister(this); }
    }

    private void OnDied() {
        Destroy(gameObject);
    }

    public void SetCell(Vector2Int cell) {
        if (Cell == cell) { return; }
        Cell = cell;
        CellChanged?.Invoke(Cell);
    }

    public void MarkMoved() {
        HasMoved = true;
    }

    /// <summary>Pairs with MarkMoved() — used by MoveCommand.Undo() to reverse a pre-move that the player backed out of.</summary>
    public void ClearMoved() {
        HasMoved = false;
    }

    public void MarkActed() {
        HasActed = true;
    }

    public void ResetForTurn() {
        HasMoved = false;
        HasActed = false;
    }
}
