using System;
using UnityEngine;

/// <summary>Cross-system signal carrying the result of one attack — drives the floating damage/miss popup.</summary>
[CreateAssetMenu(menuName = "Game/Event Channels/Attack Outcome Channel")]
public sealed class AttackOutcomeEventChannel : ScriptableObject
{
    public event Action<AttackOutcome> Raised;

    public void Raise(AttackOutcome outcome) => Raised?.Invoke(outcome);
}
