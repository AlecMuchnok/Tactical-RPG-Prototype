/// <summary>
/// Payload for AttackOutcomeEventChannel. Carries an explicit Hit flag rather
/// than letting readers infer a miss from Damage == 0 — that inference
/// happens to hold today (CombatMath.RollDamage clamps to a minimum of 1) but
/// is a hidden invariant spanning two files that would break silently the day
/// a glancing-blow-for-0 is added.
/// </summary>
public readonly struct AttackOutcome
{
    public readonly Unit Target;
    public readonly int Damage;
    public readonly bool Hit;

    public AttackOutcome(Unit target, int damage, bool hit) {
        Target = target;
        Damage = damage;
        Hit = hit;
    }
}
