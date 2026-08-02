# Architecture Rules

## How This Project Is Wired

Three kinds of class, one place that connects them.

```
Model   plain C# — the facts (a unit's coord, its HP, whose turn it is)
System  plain C# — the rules (can this unit move there? apply damage)
View    MonoBehaviour — the pixels (draw the sprite, play the glide, read the mouse)
```

A **bootstrap** MonoBehaviour in the scene creates the Models and Systems in `Awake`, then hands each View exactly the Model and Systems it needs by calling `Init(...)`. Nothing looks anything up. Nothing is `static`. If class A uses class B, someone handed B to A — you can always find that line.

That last sentence is the whole point. When you're debugging, "who gave this object its dependencies" has exactly one answer: the bootstrap.

**This project uses no DI framework, no message bus, and no reactive library.** Everything below is stock C# and stock Unity 6.

## Model-View-System (MVS) Pattern

```
Model  — plain C# class. State + derived state. No MonoBehaviour, no GameObject,
         no Transform, no Component, no scene lookups. Value types like Vector2Int,
         Vector3 and Color are fine — they are just math.
View   — MonoBehaviour. Renders a Model, forwards input to a System. No game rules.
System — plain C# class. Owns and mutates Models. All game rules live here.
```

**Rules:**
- Models NEVER reference Views or Systems
- Systems NEVER reference Views
- **Only Systems mutate Models** — a View reads a Model and calls a System; it never calls a Model's `Set…` method directly
- Views observe Models via C# events — no polling in Update
- Views call Systems for actions
- One System can own multiple Models; one View binds to one primary Model

## Models: State and Change Notification

A Model is a plain C# class: `{ get; private set; }` properties, a `Set…` method per mutable value, and a plain `event Action<T>` raised by that method.

```csharp
using System;
using UnityEngine;

/// <summary>One unit's runtime state. Plain C# — no MonoBehaviour, no scene access.</summary>
public sealed class UnitModel
{
    public Vector2Int Coord { get; private set; }
    public int Health { get; private set; }
    public int MaxHealth { get; }

    // Raised AFTER the value has changed. The payload is the new value, so a
    // handler never has to read back from the model.
    public event Action<Vector2Int> CoordChanged;
    public event Action<int> HealthChanged;
    public event Action Died;

    public bool IsDead => Health <= 0;

    public UnitModel(Vector2Int coord, int maxHealth)
    {
        Coord = coord;
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public void SetCoord(Vector2Int coord)
    {
        if (Coord == coord) { return; }
        Coord = coord;
        CoordChanged?.Invoke(Coord);
    }

    public void SetHealth(int health)
    {
        int clamped = Mathf.Clamp(health, 0, MaxHealth);
        if (Health == clamped) { return; }

        bool wasAlive = !IsDead;
        Health = clamped;
        HealthChanged?.Invoke(Health);

        if (wasAlive && IsDead)
        {
            Died?.Invoke();
        }
    }
}
```

**Rules:**
- Every mutable piece of state is `{ get; private set; }` plus a `Set…` method. That method is the only way it changes.
- **Guard against no-op changes** (`if (Coord == coord) { return; }`). Without it, a value re-assigned every frame fires the event every frame.
- **Change the field first, raise the event second.** A handler reading the Model must see the new value.
- The payload carries the new value; handlers shouldn't need the Model reference.
- Derived state is a get-only expression property (`IsDead`) with no event of its own — it rides on whatever it derives from.
- A Model never subscribes to anything. It only raises.
- If one Model grows more than ~3 of these Set/event/guard triples and it's genuinely hurting readability, see the Appendix — but reach for that late, not by default.

**Why `SetCoord` is public even though only Systems should call it** — a sharp reader will ask this: there are no assembly boundaries in this project, so `internal` would not stop a View from calling `SetCoord`. "Only Systems mutate Models" is a **convention**, checked by `.claude/scripts/validate-architecture.sh`, not by the compiler. If the project later grows assembly definitions, revisit this.

*Rejected alternatives, and why:* a hand-rolled `ReactiveProperty<T>` (the generic wrapper plus `.Value` plus `.Subscribe` indirection is exactly the machinery a beginner must decode before reading any real code, for almost no gain over a plain property + event); `UnityEvent<T>` (allocates, drags Models into `UnityEngine.Events`); `INotifyPropertyChanged` (string property names, no typed payload, a `switch` in every handler).

## Systems

```csharp
using System;
using UnityEngine;

/// <summary>Owns unit movement rules. Plain C# — testable with no scene.</summary>
public sealed class MovementSystem : IDisposable
{
    private readonly BattleModel _battle;

    // Other Systems and Views subscribe to this instead of holding a reference to us.
    public event Action<UnitModel, Vector2Int> UnitMoved;

    public MovementSystem(BattleModel battle)
    {
        _battle = battle;
    }

    public bool CanMove(UnitModel unit, Vector2Int target)
    {
        if (!_battle.Grid.Contains(target)) { return false; }
        if (_battle.GetUnitAt(target) != null) { return false; }
        return true;
    }

    public void Move(UnitModel unit, Vector2Int target)
    {
        if (!CanMove(unit, target)) { return; }

        unit.SetCoord(target);
        UnitMoved?.Invoke(unit, target);
    }

    public void Dispose()
    {
        UnitMoved = null;
    }
}
```

**Rules:**
- `sealed`, plain C#, no MonoBehaviour.
- Every dependency arrives through the constructor — never a bundle (see "No God Container" below). `readonly` on every field.
- Systems never touch `GameObject`, `Transform`, `Input`, `Camera`, or `Debug.Log`.
- If a System subscribes to anything, owns a `CancellationTokenSource`, or exposes an event, it implements `IDisposable` and the bootstrap disposes it.
- `UnitMoved = null;` in `Dispose()` is legal only inside the declaring class, and it's the cheapest way to drop every subscriber at once.

## Views

```csharp
using UnityEngine;

/// <summary>Draws one unit and reacts to its state. Owns no game rules.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class UnitView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    private SpriteRenderer _renderer;
    private UnitModel _model;
    private IsoGrid _grid;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>Called once by the scene bootstrap. This is how the View gets its data.</summary>
    public void Init(UnitModel model, IsoGrid grid)
    {
        _model = model;
        _grid = grid;

        _model.CoordChanged += OnCoordChanged;
        _model.HealthChanged += OnHealthChanged;

        // Subscribe, then paint once. Events only fire on FUTURE changes, so a
        // fresh View must draw the CURRENT state itself or it starts out blank.
        OnCoordChanged(_model.Coord);
        OnHealthChanged(_model.Health);
    }

    private void OnDestroy()
    {
        if (_model == null) { return; }

        _model.CoordChanged -= OnCoordChanged;
        _model.HealthChanged -= OnHealthChanged;
    }

    private void OnCoordChanged(Vector2Int coord)
    {
        transform.position = _grid.CellToWorld(coord);
        _renderer.sortingOrder = _grid.SortingOrder(coord) + 5;
    }

    private void OnHealthChanged(int health)
    {
        // update a health bar, flash red, etc.
    }
}
```

**Rules:**
- A View gets its Model and Systems through a public `Init(...)` called once by the bootstrap — never `FindObjectOfType`, never a static.
- `Awake` caches components on **this** GameObject only, and must not touch `_model` (it may not be set yet — see the bootstrap section below).
- **Subscribe in `Init`, unsubscribe in `OnDestroy`**, and paint the current state right after subscribing.
- A View never calls a Model's `Set…` method.
- No `Update` polling of Model state — if you're re-reading a Model field every frame, the Model is missing an event.
- Views are `sealed`.

## The Composition Root

```csharp
using UnityEngine;

/// <summary>
/// The single wiring point for this scene. Creates every Model and System, then
/// hands each View exactly what it needs. This is the ONLY class that knows about
/// more than its own direct dependencies — that is its job.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class BattleBootstrap : MonoBehaviour
{
    [Header("Scene views")]
    [SerializeField] private GridView _gridView;
    [SerializeField] private UnitView _unitViewPrefab;
    [SerializeField] private BattleInputView _inputView;

    [Header("Setup")]
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;
    [SerializeField] private Vector2Int _unitStartCell = new Vector2Int(4, 4);

    private BattleModel _battle;
    private MovementSystem _movementSystem;
    private SelectionSystem _selectionSystem;

    private void Awake()
    {
        // 1. Models — the facts.
        _battle = new BattleModel(_width, _height);
        UnitModel playerUnit = _battle.AddUnit(_unitStartCell, maxHealth: 20);

        // 2. Systems — the rules. Each constructor lists exactly what it uses.
        _movementSystem = new MovementSystem(_battle);
        _selectionSystem = new SelectionSystem(_battle, _movementSystem);

        // 3. Views — the pixels. Each Init lists exactly what that view may touch.
        _gridView.Init(_battle.Grid);
        _inputView.Init(_battle, _selectionSystem);

        UnitView unitView = Instantiate(_unitViewPrefab, transform);
        unitView.Init(playerUnit, _battle.Grid);
    }

    private void OnDestroy()
    {
        // Reverse creation order. Systems that subscribe or hold a
        // CancellationTokenSource must be disposed or they leak across scene loads.
        _selectionSystem?.Dispose();
        _movementSystem?.Dispose();
    }
}
```

**Rules:**
- **Exactly one bootstrap MonoBehaviour per scene**, named `<Scene>Bootstrap`.
- It is the **only** class allowed to `new` a System.
- `[DefaultExecutionOrder(-1000)]` guarantees its `Awake` runs before any View's `Start` — it does **not** guarantee ordering against other `Awake`s, which is why Views must not touch their Model in `Awake`.
- Create Models → Systems → Views; dispose in reverse.
- Past ~40 lines in `Awake`, split the scene into smaller ones — don't add a manager class to hold the overflow.
- For state that needs to survive a scene load: an `AppBootstrap` in a bootstrap scene with `DontDestroyOnLoad`, referenced by `[SerializeField]` or a scene-load handoff — **not** a static `Instance`.

[GridManager.cs](../../Assets/Scripts/GridManager.cs) already has roughly this shape today (`Awake` → `BuildCells`/`BuildUnit`/`FrameCamera`, `Init(...)` calls on `GridCell` and `Unit`). Migrating it means splitting its state out into Models — not rearchitecting from scratch.

## No Singletons, No Service Locator, No God Container

Do NOT create a `GameContext`, `ServiceLocator`, `Dependencies`, or any "god container" class that bundles multiple dependencies into a single injectable object. This is a **Service Locator anti-pattern**.

```csharp
// BAD — GameContext exposes everything to everyone
public class GameContext
{
    public PlayerModel Player { get; }
    public ScoreSystem Score { get; }      // Why should SpawnView see this?
    public SpawnSystem Spawner { get; }    // Why should ScoreView see this?
    public IAudioService Audio { get; }
}

// Every consumer gets access to ALL dependencies
public sealed class ScoreView : MonoBehaviour
{
    public void Init(GameContext ctx)  // Real dependencies are hidden
    {
        _score = ctx.Score;  // Could also access ctx.Spawner — no access control
    }
}
```

**Why this is wrong:**
- **Violates least-privilege**: every consumer can access every dependency
- **Hides real dependencies**: the `Init` signature says "I need GameContext" instead of "I need ScoreModel"
- **Untestable**: testing one class requires constructing the entire GameContext with all its dependencies
- **A second wiring step**: GameContext construction happens outside the bootstrap, duplicating the bootstrap's only job
- **Properties that should be private are exposed**: GameContext forces public access on dependencies that only specific consumers need

```csharp
// GOOD — each class declares exactly what it needs
public sealed class ScoreView : MonoBehaviour
{
    private ScoreModel _model;

    public void Init(ScoreModel model)  // Only what it needs — nothing else visible
    {
        _model = model;
    }
}

public sealed class CombatSystem : IDisposable
{
    private readonly PlayerModel _player;
    private readonly EventSystem _events;

    // Constructor declares exact dependencies — self-documenting, testable
    public CombatSystem(PlayerModel player, EventSystem events)
    {
        _player = player;
        _events = events;
    }

    public void Dispose() { }
}
```

**The rule:** every class requests only its own dependencies, via constructor (Systems) or `Init(...)` (Views). The scene bootstrap is the single place where wiring happens. No intermediary container objects.

**No singletons.** No `static Instance`, no `static` mutable state, no `FindObjectOfType`, no `Resources.Load` to reach a system. If you need something, it is handed to you.

- Need it in one scene? The scene bootstrap creates it.
- Need it across scenes? An `AppBootstrap` in a bootstrap scene creates it once and passes it down.
- Need it in ten places? That's the real problem — split it, don't reach for a global.

`static readonly` constants and pure `static` helper functions (`Mathf`-style, no state) are fine. The ban is on **shared mutable state reachable from anywhere**.

## Cross-System Communication: C# Events

No SO event channels, no static EventBus, no message-bus library. A System that wants to notify others exposes a plain C# `event`; anyone who cares subscribes.

```csharp
// --- A System raises ---
public sealed class MovementSystem : IDisposable
{
    public event Action<UnitModel, Vector2Int> UnitMoved;

    public void Move(UnitModel unit, Vector2Int target)
    {
        unit.SetCoord(target);
        UnitMoved?.Invoke(unit, target);
    }

    public void Dispose() => UnitMoved = null;
}

// --- Another System subscribes, via the constructor ---
public sealed class TurnSystem : IDisposable
{
    private readonly MovementSystem _movement;

    public TurnSystem(MovementSystem movement)
    {
        _movement = movement;
        _movement.UnitMoved += OnUnitMoved;
    }

    private void OnUnitMoved(UnitModel unit, Vector2Int cell) { /* spend the unit's move */ }

    public void Dispose() => _movement.UnitMoved -= OnUnitMoved;
}
```

**Subscribe/unsubscribe pairing — every subscription has exactly one home for each half:**

| What | Subscribe | Unsubscribe |
|---|---|---|
| A Model handed to a View | `Init` | `OnDestroy` |
| A System handed to a View | `Init` | `OnDestroy` |
| A System, from another System | constructor | `Dispose` |
| Unity `InputAction` callbacks | `OnEnable` | `OnDisable` |
| A UGUI / UI Toolkit control | `OnEnable` | `OnDisable` |

**Why the split:** input actions and UI controls should stop firing the moment an object is disabled. A View's Model binding should not — the Model keeps changing while the View is hidden, and the View must be correct when re-enabled. Binding once in `Init` and releasing in `OnDestroy` is one pair with no re-entry bug; doing it in `OnEnable`/`OnDisable` requires guarding against `Init` arriving after `OnEnable`, which is exactly where double-subscription bugs come from. See the `event-systems` skill for the general subscribe/unsubscribe rule; this table is the project-specific refinement for Model bindings.

**Other rules:**
- Always `?.Invoke(...)` — a subscriber-less event is `null`.
- Raise **after** the state change, never before.
- Never raise an event from inside a handler for that same event.
- Name events past-tense (`UnitMoved`, `TurnStarted`, `Died`); reserve `OnX` for handler methods.
- Payloads are the changed values, never the publishing System itself.

**When NOT to use an event:** if the publisher and subscriber are both created by the same bootstrap and both always exist, and the call is synchronous and same-frame — **just call the method**. An event is for "I don't know or care who's listening." `_movementSystem.Move(unit, cell)` called directly from the input view is correct and should stay a direct call, not become an event for its own sake.

## Async: `Awaitable`

Unity 6 ships `UnityEngine.Awaitable`, a built-in awaitable type — no async library is needed.

```csharp
public sealed class UnitView : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;

    /// <summary>Glides the sprite through each waypoint. Returns when it arrives.</summary>
    public async Awaitable PlayMoveAsync(IReadOnlyList<Vector3> waypoints, CancellationToken token)
    {
        for (int waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
        {
            Vector3 destination = waypoints[waypointIndex];

            while (transform.position != destination)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, destination, _moveSpeed * Time.deltaTime);

                await Awaitable.NextFrameAsync(token);
            }
        }
    }
}
```

Two legal calling shapes:

```csharp
// A. The whole method is async. Unity officially supports async Awaitable on Start.
private async Awaitable Start()
{
    await ShowIntroAsync(destroyCancellationToken);
}

// B. Fire-and-forget from a non-async method. Discard explicitly so the intent
//    is visible, and swallow cancellation inside the async method.
private void OnMoveOrdered(Vector2Int cell)
{
    _ = RunMoveAsync(cell, destroyCancellationToken);
}

private async Awaitable RunMoveAsync(Vector2Int cell, CancellationToken token)
{
    try
    {
        await _unitView.PlayMoveAsync(_path, token);
        _selectionSystem.Deselect();
    }
    catch (OperationCanceledException)
    {
        // The object was destroyed mid-move. Normal — nothing to do.
    }
}
```

A System (no `destroyCancellationToken` — it isn't a MonoBehaviour — so it owns its own token source):

```csharp
public sealed class WaveSystem : IDisposable
{
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public async Awaitable RunWavesAsync()
    {
        for (int waveIndex = 0; waveIndex < 10; waveIndex++)
        {
            SpawnWave(waveIndex);
            await Awaitable.WaitForSecondsAsync(5f, _cts.Token);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
```

**Rules:**
- Async methods return `Awaitable` or `Awaitable<T>` and end in `Async`.
- **`async void` is banned, no exceptions.** `Start` can return `Awaitable`, and everything else uses `_ = XxxAsync(token)` — there's no shape that legitimately needs `async void`.
- **Every `await` takes a `CancellationToken`** — `destroyCancellationToken` in a MonoBehaviour (built in, no manual `CancellationTokenSource` needed), an owned `CancellationTokenSource` in a System, cancelled and disposed in `Dispose()`.
- Every fire-and-forget body wraps in `try { … } catch (OperationCanceledException) { }`.
- An `Awaitable` can be awaited **once** — don't store one and re-await it.
- `Awaitable` is not thread-safe: call `Awaitable.MainThreadAsync()` before touching any Unity API after `Awaitable.BackgroundThreadAsync()`.

**Coroutines are non-preferred, not banned.** They still allocate, still stop silently on `SetActive(false)`, and have no return value, cancellation, or error handling — `Awaitable` fixes all three. Prefer `Awaitable` for new code; don't rewrite an existing coroutine just because it exists.

## Input Architecture (NON-NEGOTIABLE)

Input is a **View-layer concern**. It follows the same MVS pattern: InputView reads raw input and forwards it to Systems. Systems never touch Unity Input directly.

### InputView Pattern

```csharp
// InputView — thin adapter between New Input System and game Systems
public sealed class InputView : MonoBehaviour
{
    private PlayerControls _controls;
    private PlayerSystem _playerSystem;
    private UISystem _uiSystem;

    private void Awake()
    {
        _controls = new PlayerControls();
    }

    public void Init(PlayerSystem playerSystem, UISystem uiSystem)
    {
        _playerSystem = playerSystem;
        _uiSystem = uiSystem;
    }

    private void OnEnable()
    {
        _controls.Player.Enable();
        _controls.Player.Jump.performed += OnJump;
        _controls.Player.Attack.performed += OnAttack;
        _controls.Player.Pause.performed += OnPause;
    }

    private void OnDisable()
    {
        _controls.Player.Jump.performed -= OnJump;
        _controls.Player.Attack.performed -= OnAttack;
        _controls.Player.Pause.performed -= OnPause;
        _controls.Player.Disable();
    }

    private void Update()
    {
        Vector2 move = _controls.Player.Move.ReadValue<Vector2>();
        _playerSystem.SetMoveInput(move);
    }

    private void OnJump(InputAction.CallbackContext ctx) => _playerSystem.Jump();
    private void OnAttack(InputAction.CallbackContext ctx) => _playerSystem.Attack();
    private void OnPause(InputAction.CallbackContext ctx) => _uiSystem.TogglePause();
}
```

The bootstrap wires it like any other View: `[SerializeField] private InputView _inputView;` then `_inputView.Init(playerSystem, uiSystem);` in `Awake`.

### Rules
- **InputView owns PlayerControls** — no other class creates or holds a `PlayerControls` instance
- **InputView is a View** — it reads input and calls Systems. Zero game logic
- **Systems are input-agnostic** — they expose methods like `SetMoveInput(Vector2)`, `Jump()`, `Attack()`. They never know where input comes from (keyboard, gamepad, AI, network replay)
- **One InputView per scene** — prevents duplicate action subscriptions
- **Enable/Disable is mandatory** — `OnEnable` enables action maps, `OnDisable` disables them and unsubscribes callbacks
- **Continuous input in Update** — read `ReadValue<Vector2>()` in Update, cache it. Apply physics in FixedUpdate using cached values
- **Discrete input via callbacks** — button presses use `performed` callbacks, not polling
- **Action map switching lives in InputView** — controlled by Systems via method calls (e.g., `SwitchToUI()`, `SwitchToGameplay()`)

### Two Ways to Read Input, and When Each Is Right

The New Input System gives you two entry points:

1. **A generated `PlayerControls` class from an `.inputactions` asset** — for anything a player rebinds or that differs across keyboard/gamepad: move, confirm, cancel, cycle-unit, end-turn. Console certification requires rebinding, so every gameplay action goes here.
2. **Direct device polling — `Mouse.current`, `Keyboard.current`, `Gamepad.current`** — still the New Input System, not the legacy API. Use it for raw pointer position, which isn't a rebindable "action". `Mouse.current.position.ReadValue()` is legitimate.

Never `Input.GetKey` / `Input.GetAxis` / `Input.GetMouseButton`. Those are the legacy input manager — they don't see gamepads properly, and the `block-legacy-input` hook blocks the edit.

Because this game ships to console, plan for **no mouse at all**: every mouse interaction needs a gamepad path (a cursor on the left stick, or direct grid-cell navigation with the d-pad). Keep that in the InputView, not in the Systems — Systems just receive `SelectCell(Vector2Int)`.

### Testing Input-Driven Systems

Because Systems are input-agnostic, they are trivially testable:
```csharp
[Test]
public void SetMoveInput_WithRightVector_UpdatesModelPosition()
{
    var model = new PlayerModel();
    var sut = new PlayerSystem(model);

    sut.SetMoveInput(Vector2.right);
    sut.Tick(1f);

    Assert.That(model.Position.x, Is.GreaterThan(0));
}
```

No input mocking needed — the System never sees InputAction, PlayerControls, or any Unity Input type.

## ScriptableObjects for Static Data

Items, abilities, enemy configs, level data — all should be ScriptableObjects:

```csharp
[CreateAssetMenu(menuName = "Game/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private float _damage;
    [SerializeField] private float _fireRate;
    [SerializeField] private GameObject _prefab;
}
```

ScriptableObjects hold **static/config data**. Runtime mutable state goes in Models. A ScriptableObject is not a Model.

## Composition Over Inheritance, No God Objects

MonoBehaviour is a component, not a base class. Don't build deep inheritance trees.

Max MonoBehaviour inheritance depth: 2 (base + one subclass). Beyond that, compose.

Views should be thin — the logic lives in Systems, data lives in Models.

```csharp
// BAD
class GameManager : MonoBehaviour
{
    // Handles: score, lives, spawning, UI, audio, saving, input, pause...
}

// GOOD — separate Systems, each created by the scene bootstrap
// PlayerSystem — health, movement
// ScoreSystem — scoring, combos
// SpawnSystem — enemy waves
// Each is a plain C# class with its dependencies passed to the constructor
```

## Dependency Direction

```
Bootstrap ──creates──▶ Models ◀──mutates── Systems
    │                    │                    ▲
    └──Init(…)──▶ Views ─┘ (read + subscribe) │
                    └────────calls────────────┘

Systems talk to each other with C# events, never direct back-references.
Models depend on nothing.
```

- Views depend on Systems and Models (handed in via `Init`)
- Systems depend on Models and other Systems (handed in via constructor)
- Models depend on nothing
- Cross-system communication goes through C# events, never a hidden global
- Nothing enforces this at compile time yet — there are no assembly definitions. `.claude/scripts/validate-architecture.sh` checks it by grep, and it keys on the `*Model` / `*System` / `*View` filename suffixes, so name files accordingly.

## Scene Independence

Each scene should be loadable independently:
1. An `AppBootstrap` lives in a bootstrap scene (app-wide services, `DontDestroyOnLoad`)
2. Each game scene has its own `<Scene>Bootstrap` that creates that scene's Models and Systems
3. No hidden dependencies on "the scene before this one"
4. Scene loading/unloading is async:

```csharp
await SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
```

## Appendix: `Observable<T>` (Optional)

Use this **only** if a Model has grown so many `Set…`/event/no-op-guard triples that they're drowning the rest of the class — not by default. Plain properties with plain events (see "Models" above) are easier to read and debug, and should stay the default.

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// A value that raises an event when it changes. Optional convenience for a Model
/// with many observed fields — not a replacement for the plain property + event
/// pattern, which should still be the default.
/// </summary>
public sealed class Observable<T>
{
    private T _value;

    public event Action<T> Changed;

    public Observable(T initial) => _value = initial;

    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) { return; }
            _value = value;
            Changed?.Invoke(_value);
        }
    }

    /// <summary>Subscribes AND immediately calls the handler with the current value.</summary>
    public void Bind(Action<T> handler)
    {
        Changed += handler;
        handler(_value);
    }

    public void Unbind(Action<T> handler) => Changed -= handler;
}
```

`Bind`/`Unbind` still needs pairing per the subscribe/unsubscribe table above — it's a convenience, not an escape from lifetime discipline.
