using UnityEngine;

/// <summary>
/// A count of things currently holding up battle progression — a damage
/// popup mid-float, and later a hit VFX or death animation, each take a lock
/// while they play and release it when done. Turn flow awaits the count
/// returning to zero instead of holding a reference to any specific effect.
/// </summary>
public sealed class BattleLocks : MonoBehaviour
{
    private int _lockCount;

    private void Awake() {
        ServiceLocator.Register(this);
    }

    private void OnDestroy() {
        ServiceLocator.Unregister<BattleLocks>();
    }

    public void Add() {
        _lockCount++;
    }

    // Floored at zero so a stray double-release can't drive the count
    // negative and leave the battle permanently locked.
    public void Remove() {
        _lockCount = Mathf.Max(0, _lockCount - 1);
    }

    public async Awaitable WaitUntilClearAsync() {
        while (_lockCount > 0) {
            await Awaitable.NextFrameAsync(destroyCancellationToken);
        }
    }
}
