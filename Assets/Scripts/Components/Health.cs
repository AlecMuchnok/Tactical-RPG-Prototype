using System;
using UnityEngine;

/// <summary>
/// One unit's hit points. `Max` is set externally by Unit from UnitStats
/// (single source of truth for stats) rather than serialized here, so a
/// prefab's Health can't drift out of sync with its assigned UnitStats asset.
/// </summary>
public sealed class Health : MonoBehaviour
{
    public int Current { get; private set; }
    public int Max { get; private set; }
    public bool IsDead => Current <= 0;

    public event Action<int> Changed;
    public event Action Died;

    public void Initialize(int maxHealth) {
        Max = maxHealth;
        Current = maxHealth;
    }

    public void ApplyDamage(int amount) {
        int clamped = Mathf.Clamp(Current - amount, 0, Max);
        if (Current == clamped) { return; }

        bool wasAlive = !IsDead;
        Current = clamped;
        Changed?.Invoke(Current);

        if (wasAlive && IsDead) { Died?.Invoke(); }
    }
}
