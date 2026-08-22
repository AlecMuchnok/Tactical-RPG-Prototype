using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who is standing where. A list rather than a coord-keyed dictionary: units
/// move, so a dictionary would need re-keying on every step. Cheap at this
/// unit count to scan the list instead.
/// </summary>
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
        foreach (Unit unit in _units) {
            if (unit.Cell == cell) { return unit; }
        }
        return null;
    }

    public bool IsOccupied(Vector2Int cell) => GetUnitAt(cell) != null;

    /// <summary>Every unit on `team`.</summary>
    public List<Unit> UnitsOnTeam(Team team) {
        List<Unit> result = new List<Unit>();
        foreach (Unit unit in _units) {
            if (unit.Team == team) { result.Add(unit); }
        }
        return result;
    }
}
