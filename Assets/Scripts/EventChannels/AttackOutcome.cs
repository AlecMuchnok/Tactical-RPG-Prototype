/// <summary>Payload for AttackOutcomeEventChannel.</summary>
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
