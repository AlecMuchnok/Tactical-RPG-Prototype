using System;
using UnityEngine;

/// <summary>Cross-system signal carrying a Unit reference — used for "this unit was defeated".</summary>
[CreateAssetMenu(menuName = "Game/Event Channels/Unit Channel")]
public sealed class UnitEventChannel : ScriptableObject
{
    public event Action<Unit> Raised;

    public void Raise(Unit unit) => Raised?.Invoke(unit);
}
