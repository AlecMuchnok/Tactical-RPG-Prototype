using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure mediator between the confirm-dialog View and the game: converts
/// BattleAction choices to labels, positions the menu at a board cell, and
/// reports which action was chosen. Game-flow decisions (which actions exist,
/// what each one does) live in AwaitingConfirmState — this class used to
/// decide those itself (finding adjacent enemies, building commands), which
/// was game logic living in a UI class.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class ActionMenuPresenter : MonoBehaviour
{
    [SerializeField] private ActionMenuView _view;
    [SerializeField] private Camera _uiCamera;

    private GridManager _grid;

    private readonly List<string> _optionLabels = new List<string>();
    private IReadOnlyList<BattleAction> _actions;
    private Action<BattleAction> _onChosen;

    // why: registered like the four named Systems in architecture.md §6 even
    // though this lives in UI/Presenters — the plain-C# AwaitingConfirmState
    // isn't a MonoBehaviour and has no scene reference otherwise.
    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<ActionMenuPresenter>();
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
    }

    private void OnEnable() {
        _view.OptionChosen += OnOptionChosen;
    }

    private void OnDisable() {
        _view.OptionChosen -= OnOptionChosen;
    }

    public void Show(Vector2Int anchorCell, IReadOnlyList<BattleAction> actions, Action<BattleAction> onChosen) {
        _actions = actions;
        _onChosen = onChosen;

        _optionLabels.Clear();
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++) {
            _optionLabels.Add(actions[actionIndex].ToString());
        }

        Vector3 worldPosition = _grid.CellToWorld(anchorCell);
        Camera cam = _uiCamera != null ? _uiCamera : Camera.main;
        Vector2 screenPosition = cam.WorldToScreenPoint(worldPosition);
        _view.Show(screenPosition, _optionLabels);
    }

    public void Hide() {
        _view.Hide();
    }

    private void OnOptionChosen(int index) {
        BattleAction action = _actions[index];
        Action<BattleAction> callback = _onChosen;
        _onChosen = null;
        callback?.Invoke(action);
    }
}
