using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads mouse input directly via the New Input System's Mouse.current — no
/// .inputactions asset or PlayerInput component required. Owns the
/// hover/select/move state machine for the single-unit slice:
///
///   - hover highlights the cell under the cursor
///   - clicking the unit's cell selects it
///   - clicking another cell while selected orders a move
///   - clicking the unit's own cell while selected deselects it
///
/// While the unit is moving, input is locked: hover is suppressed and clicks
/// are swallowed entirely (not queued), so a move can't be redirected or
/// stacked mid-glide.
/// </summary>
[RequireComponent(typeof(GridManager))]
public class GridInputController : MonoBehaviour
{
    private GridManager _gridManager;
    private Camera _camera;
    private Unit _selectedUnit;
    private Unit _movingUnit;
    private Vector2Int? _hoveredCell;

    private bool InputLocked => _movingUnit != null;

    private void Awake()
    {
        _gridManager = GetComponent<GridManager>();
        _camera = Camera.main;
    }

    private void Update()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null || Mouse.current == null) return;

        if (InputLocked)
        {
            ClearHover();
            return;
        }

        IsoGrid grid = _gridManager.Grid;
        // z is set to the camera's distance from the world (i.e. world z = 0), which
        // is what an orthographic camera needs to project screen -> world correctly.
        // This only holds for an orthographic camera — see GridManager.FrameCamera.
        Vector2 screenPoint = Mouse.current.position.ReadValue();
        Vector3 worldPoint = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -_camera.transform.position.z));
        Vector2Int cell = grid.WorldToCell(worldPoint);
        bool inBounds = grid.Contains(cell);

        UpdateHover(inBounds ? cell : (Vector2Int?)null);

        if (Mouse.current.leftButton.wasPressedThisFrame && inBounds)
        {
            HandleClick(cell);
        }
    }

    private void UpdateHover(Vector2Int? cell)
    {
        if (_hoveredCell == cell) return;

        if (_hoveredCell.HasValue)
        {
            _gridManager.GetCell(_hoveredCell.Value)?.SetHovered(false);
        }

        _hoveredCell = cell;

        if (_hoveredCell.HasValue)
        {
            _gridManager.GetCell(_hoveredCell.Value)?.SetHovered(true);
        }
    }

    private void ClearHover()
    {
        UpdateHover(null);
    }

    private void HandleClick(Vector2Int cell)
    {
        if (_selectedUnit == null)
        {
            Unit clicked = _gridManager.GetUnitAt(cell);
            if (clicked != null)
            {
                _selectedUnit = clicked;
                clicked.SetSelected(true);
            }
            return;
        }

        if (cell == _selectedUnit.Coord)
        {
            Deselect();
            return;
        }

        Unit unit = _selectedUnit;
        _movingUnit = unit;
        StartCoroutine(unit.MoveTo(cell, _gridManager.Grid, OnMoveComplete));
    }

    private void OnMoveComplete()
    {
        Deselect();
        _movingUnit = null;
    }

    private void Deselect()
    {
        _selectedUnit?.SetSelected(false);
        _selectedUnit = null;
    }
}
