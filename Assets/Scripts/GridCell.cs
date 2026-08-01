using UnityEngine;

/// <summary>
/// A single visual grid tile. Purely a state holder + renderer — the
/// GridInputController decides when a cell is hovered, this component just
/// reflects that as a color change.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GridCell : MonoBehaviour
{
    public Vector2Int Coord { get; private set; }

    [SerializeField] private Color normalColor = new Color(0.45f, 0.5f, 0.6f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.85f, 0.85f, 0.4f, 1f);

    private SpriteRenderer _renderer;
    private bool _hovered;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void Init(Vector2Int coord, Sprite sprite, int sortingOrder)
    {
        Coord = coord;
        _renderer.sprite = sprite;
        _renderer.sortingOrder = sortingOrder;
        _renderer.color = normalColor;
        _hovered = false;
    }

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        _renderer.color = hovered ? hoverColor : normalColor;
    }
}
