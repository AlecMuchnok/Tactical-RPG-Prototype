using UnityEngine;

/// <summary>Relabels the turn banner whenever the active team changes — the only feedback that input is locked during the enemy turn.</summary>
public sealed class TurnBannerPresenter : MonoBehaviour
{
    [SerializeField] private TurnBannerView _view;
    [SerializeField] private TeamEventChannel _turnChangedChannel;

    private void OnEnable() {
        _turnChangedChannel.Raised += OnTurnChanged;
    }

    private void OnDisable() {
        _turnChangedChannel.Raised -= OnTurnChanged;
    }

    private void OnTurnChanged(Team team) {
        _view.SetLabel(team == Team.Player ? "Player Phase" : "Enemy Phase");
    }
}
