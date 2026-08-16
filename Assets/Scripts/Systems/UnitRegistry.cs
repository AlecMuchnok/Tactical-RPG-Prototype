using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who is standing where. A list rather than a coord-keyed dictionary: units
/// move, so a dictionary would need re-keying on every step. Cheap at this
/// unit count to scan the list instead.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class UnitRegistry : MonoBehaviour
{
    private readonly List<Unit> _units = new List<Unit>();

    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<UnitRegistry>();
    }

    public void Register(Unit unit) {
        if (!_units.Contains(unit)) { _units.Add(unit); }
    }

    public void Unregister(Unit unit) {
        _units.Remove(unit);
    }

    public Unit GetUnitAt(Vector2Int cell) {
        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++) {
            if (_units[unitIndex].Cell == cell) { return _units[unitIndex]; }
        }
        return null;
    }

    /// <summary>Writes every unit on `team` into `result` (cleared first) — caller-supplied buffer, no per-call allocation.</summary>
    public void UnitsOnTeam(Team team, List<Unit> result) {
        result.Clear();
        for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++) {
            if (_units[unitIndex].Team == team) { result.Add(_units[unitIndex]); }
        }
    }
}
