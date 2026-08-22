using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Reads mouse input via the New Input System and converts it into board
/// hover/click/cancel events — pure adapter, zero gameplay logic.
/// Non-interactive states aren't handled here; they simply leave their
/// ISelectionState handlers as no-ops.
/// </summary>
public sealed class BattleInputView : MonoBehaviour
{
    private Camera _camera;
    private GridManager _grid;

    public Vector2Int? HoveredCell { get; private set; }

    public event Action<Vector2Int?> CellHovered;
    public event Action<Vector2Int> CellClicked;
    public event Action Cancelled;

    private void Awake() {
        ServiceLocator.Register(this);
        _camera = Camera.main;
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<BattleInputView>();
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
    }

    private void Update() {
        if (_camera == null || Mouse.current == null || _grid == null) { return; }

        if (Mouse.current.rightButton.wasPressedThisFrame) {
            Cancelled?.Invoke();
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
            SetHovered(null);
            return;
        }

        Vector2 screenPoint = Mouse.current.position.ReadValue();
        Vector3 worldPoint = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -_camera.transform.position.z));
        Vector2Int cell = _grid.WorldToCell(worldPoint);
        if (!_grid.Contains(cell)) {
            SetHovered(null);
            return;
        }

        SetHovered(cell);

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            CellClicked?.Invoke(cell);
        }
    }

    private void SetHovered(Vector2Int? cell) {
        if (HoveredCell == cell) { return; }
        HoveredCell = cell;
        CellHovered?.Invoke(cell);
    }
}
