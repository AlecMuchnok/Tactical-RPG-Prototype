using UnityEngine;
using UnityEngine.Pool;

/// <summary>Subscribes to attack outcomes and plays a pooled floating damage/miss popup at the target's sprite centre.</summary>
public sealed class DamagePopupPresenter : MonoBehaviour
{
    [SerializeField] private AttackOutcomeEventChannel _attackResolvedChannel;
    [SerializeField] private DamagePopupView _popupPrefab;

    private ObjectPool<DamagePopupView> _pool;
    private BattleLocks _locks;

    private void Awake() {
        _pool = new ObjectPool<DamagePopupView>(CreatePopup, OnGetPopup, OnReleasePopup, OnDestroyPopup);
    }

    private void Start() {
        _locks = ServiceLocator.Get<BattleLocks>();
    }

    private void OnEnable() {
        _attackResolvedChannel.Raised += OnAttackResolved;
    }

    private void OnDisable() {
        _attackResolvedChannel.Raised -= OnAttackResolved;
    }

    private void OnAttackResolved(AttackOutcome outcome) {
        // Captured synchronously off the event, before CombatSystem applies
        // damage — a killing blow destroys the target's GameObject, so
        // reading its position any later would risk touching a destroyed object.
        Vector3 position = outcome.Target.View.SpriteCenter;
        string text = outcome.Hit ? outcome.Damage.ToString() : "Miss";
        // Fire-and-forget from a synchronous event handler — cancellation
        // mid-float is already caught inside DamagePopupView.PlayAsync, so
        // nothing here needs its own try/catch.
        _ = PlayPopupAsync(text, !outcome.Hit, position);
    }

    private async Awaitable PlayPopupAsync(string text, bool isMiss, Vector3 position) {
        DamagePopupView popup = _pool.Get();
        _locks.Add();
        // try/finally so a popup cancelled mid-float (e.g. exiting play mode)
        // still releases its lock — otherwise the count would stick above
        // zero and every subsequent turn would hang.
        try {
            await popup.PlayAsync(text, isMiss, position);
        } finally {
            _pool.Release(popup);
            _locks.Remove();
        }
    }

    // Parented to this presenter, not the unit, so it finishes its animation
    // even if the unit is destroyed mid-flight.
    private DamagePopupView CreatePopup() {
        return Instantiate(_popupPrefab, transform);
    }

    private void OnGetPopup(DamagePopupView popup) {
        popup.gameObject.SetActive(true);
    }

    private void OnReleasePopup(DamagePopupView popup) {
        popup.gameObject.SetActive(false);
    }

    private void OnDestroyPopup(DamagePopupView popup) {
        Destroy(popup.gameObject);
    }
}
