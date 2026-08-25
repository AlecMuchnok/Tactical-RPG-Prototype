using UnityEngine;

/// <summary>Resolves one attack. Both target selection and EnemyBrain construct this — no separate AI combat path.</summary>
public sealed class AttackCommand : ICommand
{
    private readonly Unit _attacker;
    private readonly Unit _defender;
    private readonly Weapon _weapon;

    public AttackCommand(Unit attacker, Unit defender, Weapon weapon) {
        _attacker = attacker;
        _defender = defender;
        _weapon = weapon;
    }

    public bool CanExecute() {
        if (_attacker.HasActed) { return false; }
        return ServiceLocator.Get<CombatSystem>().AreAdjacent(_attacker, _defender);
    }

    // No visual to await here (ResolveAttack resolves instantly) — kept
    // async with no await so every ICommand.Execute() has the same
    // Awaitable-returning shape for callers to sequence uniformly, rather
    // than special-casing this one command.
    public async Awaitable Execute() {
        ServiceLocator.Get<CombatSystem>().ResolveAttack(_attacker, _defender, _weapon);
        _attacker.MarkActed();
    }

    // Target selection gates this action before Execute() ever runs — once
    // an attack resolves it has real consequences (damage dealt, a unit
    // possibly defeated) that can't be meaningfully rolled back, unlike the
    // pre-move which is purely positional.
    public async Awaitable Undo() {
    }
}
