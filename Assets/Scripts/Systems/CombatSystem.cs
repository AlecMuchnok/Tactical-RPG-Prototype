using System.Collections.Generic;
using UnityEngine;

/// <summary>Adjacency check and attack resolution — the one place combat math meets game state.</summary>
public sealed class CombatSystem : MonoBehaviour
{
    [SerializeField] private UnitEventChannel _unitDefeatedChannel;
    [SerializeField] private AttackOutcomeEventChannel _attackResolvedChannel;

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

    /// <summary>Every opponent of `ownTeam` orthogonally adjacent to `cell` — the single definition of attack adjacency, used by both the confirm menu and target selection.</summary>
    public List<Unit> FindAdjacentOpponents(Vector2Int cell, Team ownTeam) {
        List<Unit> results = new List<Unit>();
        foreach (Vector2Int offset in GridDirections.Orthogonal) {
            Unit occupant = _unitRegistry.GetUnitAt(cell + offset);
            if (occupant != null && occupant.Team != ownTeam) { results.Add(occupant); }
        }
        return results;
    }

    public void ResolveAttack(Unit attacker, Unit defender, Weapon weapon) {
        float finalHitChance = CombatMath.AttackChance(attacker.Character, weapon, defender.Character, defender.Class.ArmorType);

        float roll = Random.Range(0f, 100f);
        bool didHit = roll <= finalHitChance;
        int damage = didHit ? CombatMath.CalculateDamage(attacker.Character, weapon) : 0;

        // Raised before ApplyDamage — a killing blow destroys the defender's
        // GameObject synchronously via Health.Died, so anything reading the
        // defender off this event must do so while it's still guaranteed alive.
        _attackResolvedChannel.Raise(new AttackOutcome(defender, damage, didHit));

        if (!didHit) { return; }

        defender.Health.ApplyDamage(damage);

        if (defender.Health.IsDead) {
            _unitDefeatedChannel.Raise(defender);
        }
    }
}
