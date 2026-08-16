using System.Collections.Generic;
using UnityEngine;

/// <summary>Adjacency check and attack resolution — the one place combat math meets game state.</summary>
[DefaultExecutionOrder(-100)]
public sealed class CombatSystem : MonoBehaviour
{
    [SerializeField] private UnitEventChannel _unitDefeatedChannel;
    [SerializeField] private AttackOutcomeEventChannel _attackResolvedChannel;
    [SerializeField, Range(0f, 1f)] private float _damageVariance = 0.1f;

    private static readonly Vector2Int[] NeighborOffsets = new Vector2Int[]
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    private UnitRegistry _unitRegistry;

    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<CombatSystem>();
    }

    private void Start() {
        _unitRegistry = ServiceLocator.Get<UnitRegistry>();
    }

    /// <summary>Orthogonal adjacency only — no diagonals (Manhattan distance 1).</summary>
    public bool AreAdjacent(Unit unitA, Unit unitB) {
        int manhattanDistance = Mathf.Abs(unitA.Cell.x - unitB.Cell.x) + Mathf.Abs(unitA.Cell.y - unitB.Cell.y);
        return manhattanDistance == 1;
    }

    /// <summary>Writes every opponent of `ownTeam` orthogonally adjacent to `cell` into `results` (cleared first) — the single definition of attack adjacency, used by both the confirm menu and target selection.</summary>
    public void FindAdjacentOpponents(Vector2Int cell, Team ownTeam, List<Unit> results) {
        results.Clear();
        for (int offsetIndex = 0; offsetIndex < NeighborOffsets.Length; offsetIndex++) {
            Unit occupant = _unitRegistry.GetUnitAt(cell + NeighborOffsets[offsetIndex]);
            if (occupant != null && occupant.Team != ownTeam) { results.Add(occupant); }
        }
    }

    public void ResolveAttack(Unit attacker, Unit defender) {
        float attackerHitChance = CombatMath.HitChance(attacker.Stats.Power, attacker.Stats.Dexterity, attacker.Stats.Speed);
        float defenderDodgeChance = CombatMath.DodgeChance(defender.Stats.Power, defender.Stats.Dexterity, defender.Stats.Speed);
        float finalHitChance = CombatMath.FinalHitChance(attackerHitChance, defenderDodgeChance);

        float roll = Random.Range(0f, 100f);
        bool didHit = roll <= finalHitChance;
        int damage = didHit ? CombatMath.RollDamage(attacker.Stats.Power, _damageVariance) : 0;

        // why: raised before ApplyDamage — a killing blow destroys the
        // defender's GameObject synchronously via Health.Died, so anything
        // reading the defender off this event (DamagePopupPresenter reading
        // its sprite position) must do so while it's still guaranteed alive.
        _attackResolvedChannel.Raise(new AttackOutcome(defender, damage, didHit));

        if (!didHit) { return; }

        defender.Health.ApplyDamage(damage);

        if (defender.Health.IsDead) {
            _unitDefeatedChannel.Raise(defender);
        }
    }
}
