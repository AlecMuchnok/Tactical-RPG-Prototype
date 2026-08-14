using TMPro;
using UnityEngine;

/// <summary>Pure UI: the top-of-screen phase label. No gameplay types.</summary>
public sealed class TurnBannerView : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;

    public void SetLabel(string text) {
        _label.text = text;
    }
}
