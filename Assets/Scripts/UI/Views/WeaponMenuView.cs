using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pure UI: a small vertical list of weapon option buttons positioned at a
/// screen point, plus a preview label that updates on hover. No gameplay
/// types — takes plain strings, reports a chosen or hovered index.
/// </summary>
public sealed class WeaponMenuView : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _optionButtonTemplate;
    [SerializeField] private TMP_Text _previewText;

    private readonly List<Button> _spawnedButtons = new List<Button>();

    public event Action<int> WeaponChosen;
    public event Action<int> WeaponHovered;
    public event Action WeaponUnhovered;

    private void Awake() {
        _optionButtonTemplate.gameObject.SetActive(false);
        _panel.gameObject.SetActive(false);
        _previewText.gameObject.SetActive(false);
    }

    public void Show(Vector2 screenPosition, IReadOnlyList<string> options) {
        ClearButtons();
        _panel.position = screenPosition;

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++) {
            Button button = Instantiate(_optionButtonTemplate, _panel);
            button.gameObject.SetActive(true);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.text = options[optionIndex]; }

            int capturedIndex = optionIndex;
            button.onClick.AddListener(() => WeaponChosen?.Invoke(capturedIndex));

            EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTriggerEntry(trigger, EventTriggerType.PointerEnter, () => WeaponHovered?.Invoke(capturedIndex));
            AddTriggerEntry(trigger, EventTriggerType.PointerExit, () => WeaponUnhovered?.Invoke());

            _spawnedButtons.Add(button);
        }

        _panel.gameObject.SetActive(true);
    }

    public void Hide() {
        _panel.gameObject.SetActive(false);
        _previewText.gameObject.SetActive(false);
        ClearButtons();
    }

    public void ShowPreview(string text) {
        _previewText.text = text;
        _previewText.gameObject.SetActive(true);
    }

    public void HidePreview() {
        _previewText.gameObject.SetActive(false);
    }

    private void ClearButtons() {
        foreach (Button button in _spawnedButtons) {
            Destroy(button.gameObject);
        }
        _spawnedButtons.Clear();
    }

    private static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, Action callback) {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }
}
