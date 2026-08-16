using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Reads mouse input directly via the New Input System's Mouse.current — no
/// .inputactions asset needed for this simple hover/click/cancel surface.
/// Pure adapter: converts screen input into board events, zero gameplay
/// logic. "Input locked" during non-interactive states isn't handled here —
/// it falls out naturally from the current ISelectionState's handlers being
/// no-ops (see ExecutingActionState/AwaitingConfirmState).
/// </summary>
[DefaultExecutionOrder(-100)]
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
