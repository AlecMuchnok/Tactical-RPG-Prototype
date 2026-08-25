using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A target was chosen: shows a weapon-picker menu for the unit's weapons.
/// Hovering an option previews the real attack chance, the target's dodge
/// chance, and the damage on a hit against that specific defender. Only the
/// chosen target stays highlighted, so the previewed numbers visibly belong
/// to it.
/// </summary>
public sealed class SelectingWeaponState : ISelectionState
{
    private readonly Unit _unit;
    private readonly Unit _target;
    private readonly MoveCommand _moveCommand;

    public SelectingWeaponState(Unit unit, Unit target, MoveCommand moveCommand) {
        _unit = unit;
        _target = target;
        _moveCommand = moveCommand;
    }

    public void Enter(SelectionStateMachine machine) {
        machine.Highlighter.Clear();
        machine.Highlighter.SetTargets(new List<Vector2Int> { _target.Cell });

        machine.WeaponMenu.Show(_target.Cell, _unit.Weapons, _unit.Character, _target.Character, _target.Class.ArmorType, weapon => OnWeaponChosen(machine, weapon));
    }

    public void Exit(SelectionStateMachine machine) {
        machine.WeaponMenu.Hide();
    }

    public void OnCellHovered(SelectionStateMachine machine, Vector2Int? cell) {
    }

    public void OnCellClicked(SelectionStateMachine machine, Vector2Int cell) {
    }

    public void OnCancelled(SelectionStateMachine machine) {
        machine.ChangeState(new SelectingTargetState(_unit, _moveCommand));
    }

    private void OnWeaponChosen(SelectionStateMachine machine, Weapon weapon) {
        List<ICommand> commands = new List<ICommand> { new AttackCommand(_unit, _target, weapon) };
        machine.ChangeState(new ExecutingActionState(commands));
    }
}
