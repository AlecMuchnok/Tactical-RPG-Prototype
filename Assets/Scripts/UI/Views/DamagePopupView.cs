using System;
using TMPro;
using UnityEngine;

/// <summary>Pure UI: floats its text upward and fades it out over its duration. No gameplay types — takes plain text and a colour choice, nothing else.</summary>
public sealed class DamagePopupView : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _floatDistance = 0.75f;
    [SerializeField] private float _duration = 1f;
    [SerializeField] private Color _damageColor = Color.white;
    [SerializeField] private Color _missColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    public async Awaitable PlayAsync(string text, bool isMiss, Vector3 worldPosition) {
        _label.text = text;
        Color color = isMiss ? _missColor : _damageColor;
        transform.position = worldPosition;

        Vector3 startPosition = worldPosition;
        Vector3 endPosition = worldPosition + Vector3.up * _floatDistance;

        try {
            float elapsed = 0f;
            while (elapsed < _duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                color.a = Mathf.Lerp(1f, 0f, t);
                _label.color = color;
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        } catch (OperationCanceledException) {
            // The popup's GameObject was destroyed mid-float (e.g. exiting
            // play mode) — nothing left to animate, safe to swallow.
        }
    }
}
