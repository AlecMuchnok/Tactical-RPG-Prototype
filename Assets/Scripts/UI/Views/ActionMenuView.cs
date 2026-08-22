using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pure UI: a small vertical list of option buttons positioned at a screen
/// point. No gameplay types — takes plain strings, returns a chosen index.
/// </summary>
public sealed class ActionMenuView : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _optionButtonTemplate;

    private readonly List<Button> _spawnedButtons = new List<Button>();

    public event Action<int> OptionChosen;

    private void Awake() {
        _optionButtonTemplate.gameObject.SetActive(false);
        _panel.gameObject.SetActive(false);
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
            button.onClick.AddListener(() => OptionChosen?.Invoke(capturedIndex));
            _spawnedButtons.Add(button);
        }

        _panel.gameObject.SetActive(true);
    }

    public void Hide() {
        _panel.gameObject.SetActive(false);
        ClearButtons();
    }

    private void ClearButtons() {
        foreach (Button button in _spawnedButtons) {
            Destroy(button.gameObject);
        }
        _spawnedButtons.Clear();
    }
}
