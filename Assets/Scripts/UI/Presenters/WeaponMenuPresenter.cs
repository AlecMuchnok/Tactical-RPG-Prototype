using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure mediator between the weapon-picker View and the game: converts a
/// unit's weapons to labels, positions the menu at a board cell, computes the
/// hover preview (attack chance, the target's dodge chance, damage on a hit)
/// against a specific defender, and reports which weapon was chosen.
/// </summary>
public sealed class WeaponMenuPresenter : MonoBehaviour
{
    [SerializeField] private WeaponMenuView _view;
    [SerializeField] private Camera _uiCamera;

    private GridManager _grid;

    private readonly List<string> _optionLabels = new List<string>();
    private IReadOnlyList<Weapon> _weapons;
    private Character _attackerCharacter;
    private Character _defenderCharacter;
    private ArmorType _defenderArmorType;
    private Action<Weapon> _onChosen;

    // Registered here even though this lives in UI/Presenters, not Systems —
    // the plain-C# SelectingWeaponState isn't a MonoBehaviour and has no
    // scene reference otherwise.
    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<WeaponMenuPresenter>();
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
    }

    private void OnEnable() {
        _view.WeaponChosen += OnWeaponChosen;
        _view.WeaponHovered += OnWeaponHovered;
        _view.WeaponUnhovered += _view.HidePreview;
    }

    private void OnDisable() {
        _view.WeaponChosen -= OnWeaponChosen;
        _view.WeaponHovered -= OnWeaponHovered;
        _view.WeaponUnhovered -= _view.HidePreview;
    }

    public void Show(Vector2Int anchorCell, IReadOnlyList<Weapon> weapons, Character attackerCharacter, Character defenderCharacter, ArmorType defenderArmorType, Action<Weapon> onChosen) {
        _weapons = weapons;
        _attackerCharacter = attackerCharacter;
        _defenderCharacter = defenderCharacter;
        _defenderArmorType = defenderArmorType;
        _onChosen = onChosen;

        _optionLabels.Clear();
        foreach (Weapon weapon in weapons) {
            _optionLabels.Add(weapon.WeaponType.ToString());
        }

        Vector3 worldPosition = _grid.CellToWorld(anchorCell);
        Camera cam = _uiCamera != null ? _uiCamera : Camera.main;
        Vector2 screenPosition = cam.WorldToScreenPoint(worldPosition);
        _view.Show(screenPosition, _optionLabels);
    }

    public void Hide() {
        _view.Hide();
    }

    private void OnWeaponChosen(int index) {
        Weapon weapon = _weapons[index];
        Action<Weapon> callback = _onChosen;
        _onChosen = null;
        callback?.Invoke(weapon);
    }

    private void OnWeaponHovered(int index) {
        Weapon weapon = _weapons[index];
        float attackChance = CombatMath.AttackChance(_attackerCharacter, weapon, _defenderCharacter, _defenderArmorType);
        float dodgeChance = CombatMath.DodgeChance(_defenderCharacter, _defenderArmorType);
        int damage = CombatMath.CalculateDamage(_attackerCharacter, weapon);
        _view.ShowPreview($"Attack: {Mathf.RoundToInt(attackChance)}%  Dodge: {Mathf.RoundToInt(dodgeChance)}%  Damage: {damage}");
    }
}
