using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything cosmetic about a unit: its sprite, selection tint, sort order,
/// HP bar, and the glide animation. Condensed from the plan's original
/// Mover + UnitVisual + UnitHealthBar split (§4) — the glide belongs here
/// because MoveCommand moves the logical Cell instantly; the glide is purely
/// visual catch-up, so it lives with the rest of the presentation.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class UnitView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private SpriteRenderer _healthBarFill;
    [SerializeField] private SpriteRenderer _healthBarBackground;

    private SpriteRenderer _renderer;
    private GridManager _grid;
    private Unit _unit;
    private int _healthBarFullWidthPixels;

    /// <summary>World-space anchor for effects like the floating damage popup — the sprite's visual centre, not the transform origin at the unit's feet.</summary>
    public Vector3 SpriteCenter => _renderer.bounds.center;

    private void Awake() {
        _renderer = GetComponent<SpriteRenderer>();
        _unit = GetComponent<Unit>();

        // why: derived from the sprite itself so the bar's authored size
        // stays the single source of truth — no magic number in code that
        // could desync from the art.
        Sprite fillSprite = _healthBarFill != null ? _healthBarFill.sprite : null;
        _healthBarFullWidthPixels = fillSprite != null ? Mathf.RoundToInt(fillSprite.rect.width) : 0;
    }

    private void Start() {
        _grid = ServiceLocator.Get<GridManager>();
        _renderer.sprite = _unit.Stats.Sprite;
        _renderer.color = _normalColor;
        transform.position = _grid.CellToWorld(_unit.Cell);
        UpdateSortingOrder(_unit.Cell);

        // why: subscribing here (not Awake) because Health.Initialize runs
        // inside Unit.Awake, and same-GameObject Awake order between Unit
        // and UnitView is undefined — Start is safe since the whole scene's
        // Awake phase has completed by the time any Start runs. Still
        // unsubscribes in OnDestroy, matching the sibling-event lifetime.
        _unit.Health.Changed += OnHealthChanged;
        UpdateHealthBar();
    }

    private void OnDestroy() {
        if (_unit != null && _unit.Health != null) { _unit.Health.Changed -= OnHealthChanged; }
    }

    public void SetSelected(bool selected) {
        _renderer.color = selected ? _selectedColor : _normalColor;
    }

    /// <summary>Glides through every waypoint in `path`. Fire-and-forget from the caller's
    /// perspective — cancellation on destroy is expected and swallowed, not an error.</summary>
    public async Awaitable PlayMoveAsync(IReadOnlyList<Vector2Int> path) {
        try {
            for (int pathIndex = 0; pathIndex < path.Count; pathIndex++) {
                Vector3 destination = _grid.CellToWorld(path[pathIndex]);
                while (transform.position != destination) {
                    transform.position = Vector3.MoveTowards(transform.position, destination, _moveSpeed * Time.deltaTime);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                UpdateSortingOrder(path[pathIndex]);
            }
        } catch (OperationCanceledException) {
            // why: the unit was destroyed mid-glide (defeated mid-move, or
            // exiting play mode) — nothing left to animate, safe to swallow.
        }
    }

    private void OnHealthChanged(int current) {
        UpdateHealthBar();
    }

    private void UpdateHealthBar() {
        if (_healthBarFill == null || _healthBarFullWidthPixels <= 0) { return; }

        float fraction = _unit.Health.Max > 0 ? (float)_unit.Health.Current / _unit.Health.Max : 0f;

        // why: snap to whole source pixels so a partially-drained bar always
        // ends on a pixel boundary and stays crisp under the pixel-perfect
        // camera, rather than drifting to a fractional pixel edge.
        int filledPixels = Mathf.RoundToInt(Mathf.Clamp01(fraction) * _healthBarFullWidthPixels);
        // why: a living unit never shows a completely empty bar — rounding
        // would otherwise hit 0 px while the unit still has 1 HP remaining.
        if (filledPixels == 0 && _unit.Health.Current > 0) { filledPixels = 1; }

        Vector3 scale = _healthBarFill.transform.localScale;
        // why: the fill sprite's pivot is LeftCenter and it is authored at
        // its exact full-health width, so scaling x alone drains it
        // right-to-left with the left edge pinned — no position compensation
        // needed.
        scale.x = (float)filledPixels / _healthBarFullWidthPixels;
        _healthBarFill.transform.localScale = scale;
    }

    // why: offset from a large base, not just "-cell.y", so every unit stays
    // above the Terrain tilemap (order 0) and Highlights tilemap (order 10)
    // regardless of row — cells lower on screen (smaller y) still draw in
    // front of cells further back (larger y).
    private const int SortingOrderBase = 100;

    private void UpdateSortingOrder(Vector2Int cell) {
        _renderer.sortingOrder = SortingOrderBase - cell.y;
    }
}
