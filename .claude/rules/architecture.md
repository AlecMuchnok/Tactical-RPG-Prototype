# Architecture Rules

## The Stack (priority order: composability/flexibility → onboarding ease → performance)

1. **Component composition over inheritance** — units are built from small, focused MonoBehaviours.
2. **ScriptableObject data + event channels** — static data and cross-system communication both live in SO assets.
3. **State machines** — turn flow and per-unit behavior are explicit states, not bools.
4. **Command pattern** — player and AI actions are the same command objects.
5. **MVP for UI** — Presenters mediate between game state and UI Views; Views never touch gameplay types.
6. **Service locator for shared systems** — `TurnManager`, `GridManager`, etc. are fetched by type, not found or singleton'd.

**Explicitly out of scope:** no Unity ECS/DOTS (single-player, modest unit counts don't need it), no heavy DI framework (Zenject, VContainer) — the service locator below is deliberately the lightest thing that solves the problem.

**Newcomer-friendliness requirement, since one team member is new to C#/Unity:** prefer explicit, readable code over clever abstractions. Comment *why* at each non-obvious application of a pattern below (an example of this is in every code sample in this file — look for the `// why:` comments). Don't introduce a 7th pattern beyond the six above without a comment recording the justification.

---

## 1. Component Composition Over Inheritance

A unit is a GameObject assembled from small, focused MonoBehaviours — not a `Unit : Character : Entity` inheritance chain.

```csharp
using UnityEngine;

/// <summary>One unit's hit points. Owns its own state and notifies siblings via a
/// plain C# event — components on the same GameObject share a lifetime, so they
/// subscribe in Awake and unsubscribe in OnDestroy (see the table below).</summary>
public sealed class Health : MonoBehaviour
{
    [SerializeField] private int _maxHealth = 20;

    public int Current { get; private set; }
    public int Max => _maxHealth;
    public bool IsDead => Current <= 0;

    public event Action<int> Changed;
    public event Action Died;

    private void Awake()
    {
        Current = _maxHealth;
    }

    public void ApplyDamage(int amount)
    {
        int clamped = Mathf.Clamp(Current - amount, 0, _maxHealth);
        if (Current == clamped) { return; }

        bool wasAlive = !IsDead;
        Current = clamped;
        Changed?.Invoke(Current);

        if (wasAlive && IsDead) { Died?.Invoke(); }
    }
}
```

```csharp
using UnityEngine;

/// <summary>Grid movement for one unit. Reads GridManager through the service
/// locator (see §6) because pathing needs the shared grid state, not because
/// every component should reach for the locator by default.</summary>
public sealed class Movement : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;

    private GridManager _grid;

    public Vector2Int Cell { get; private set; }
    public event Action<Vector2Int> CellChanged;

    private void Awake()
    {
        // why: fetched in Awake because Movement only READS the grid's static
        // layout, which is registered before any unit spawns. Contrast with
        // Start-time fetches for services that depend on scene-wide Awake order
        // (see the ordering rule in §6).
        _grid = ServiceLocator.Get<GridManager>();
    }

    public bool CanMoveTo(Vector2Int target) => _grid.Contains(target);

    public void SetCell(Vector2Int cell)
    {
        if (Cell == cell) { return; }
        Cell = cell;
        CellChanged?.Invoke(Cell);
    }
}
```

**Rules:**
- Sibling components talk to each other via `GetComponent<T>()` cached in `Awake` — this is the ONE place `GetComponent` at runtime (outside `Awake`) is fine, because they're guaranteed to co-exist for the GameObject's whole lifetime.
- Components talk to shared, scene-wide systems (`GridManager`, `TurnManager`, `CombatSystem`) via `ServiceLocator.Get<T>()` — never `FindObjectOfType`, never a raw `static Instance`.
- **Max inheritance depth: 2** (e.g. `MonoBehaviour → AttackAbility`). Anything deeper needs a `// why:` comment justifying it, right above the `class` declaration.
- One component, one responsibility. If a component's `Awake` is doing three unrelated things, it's two components.
- Component file names are plain nouns (`Health`, `Movement`, `AttackAbility`) — no `*Component` suffix. The `Scripts/Components/` folder is what makes a file a Component, not its filename (see Folder Structure).

---

## 2. ScriptableObject Data + Event Channels

### Static data

```csharp
[CreateAssetMenu(menuName = "Game/Unit Stats")]
public sealed class UnitStatsSO : ScriptableObject
{
    [SerializeField] private string _displayName;
    [SerializeField] private int _maxHealth;
    [SerializeField] private int _moveRange;
}
```

Unit stats, abilities, items, enemy templates — all ScriptableObjects, never hardcoded in a script or hand-authored per scene object.

### Event channels — cross-system communication

An event channel is a ScriptableObject that does nothing but hold a C# event. Publishers and subscribers each hold a serialized reference to the **same channel asset** — neither one knows the other exists.

```csharp
using System;
using UnityEngine;

/// <summary>A cross-system signal carrying a Health reference (e.g. "this unit
/// was defeated"). One concrete class per payload type — no open generic SO,
/// which is confusing to inspect and configure for a newcomer.</summary>
[CreateAssetMenu(menuName = "Game/Event Channels/Unit Channel")]
public sealed class UnitEventChannelSO : ScriptableObject
{
    public event Action<Health> Raised;

    public void Raise(Health unit) => Raised?.Invoke(unit);
}
```

```csharp
// --- Publisher: CombatSystem, holds the channel via a serialized reference ---
public sealed class CombatSystem : MonoBehaviour
{
    [SerializeField] private UnitEventChannelSO _unitDefeatedChannel;

    private void ResolveDamage(Health target, int amount)
    {
        target.ApplyDamage(amount);
        if (target.IsDead) { _unitDefeatedChannel.Raise(target); }
    }
}

// --- Subscriber: a UI Presenter, holds the SAME channel asset via the Inspector ---
public sealed class DefeatBannerPresenter : MonoBehaviour
{
    [SerializeField] private UnitEventChannelSO _unitDefeatedChannel;
    [SerializeField] private DefeatBannerView _view;

    private void OnEnable() => _unitDefeatedChannel.Raised += OnUnitDefeated;
    private void OnDisable() => _unitDefeatedChannel.Raised -= OnUnitDefeated;

    private void OnUnitDefeated(Health unit) => _view.ShowDefeat(unit.name);
}
```

**Flag any direct cross-system reference as a violation to reconsider** — e.g. a UI script holding `[SerializeField] private CombatSystem _combat;` and calling into it directly. That's exactly the coupling event channels exist to remove. The one standing exception: a component reading a **shared, read-only, scene-wide** system like `GridManager` through the service locator (§6) is not "cross-system" in this sense — it's a component reading shared world state, not two systems reaching into each other's internals.

**Subscribe/unsubscribe pairing:**

| What | Subscribe | Unsubscribe |
|---|---|---|
| A sibling component's event (same GameObject) | `Awake` | `OnDestroy` |
| An SO event channel | `OnEnable` | `OnDisable` |
| Unity `InputAction` callbacks | `OnEnable` | `OnDisable` |
| A UGUI / UI Toolkit control | `OnEnable` | `OnDisable` |

**Why the split:** sibling components share an identical lifetime (same GameObject, same destroy moment), so `Awake`/`OnDestroy` is one pairing with no re-entry risk. Everything wired through the Inspector as a serialized asset or scene reference (channels, input, UI) should stop listening the instant the object is disabled — that's the standard Unity idiom, and it's what keeps a disabled object from silently reacting to game state.

---

## 3. State Machines

Turn flow and per-unit behavior are explicit states — not a scatter of `bool isMoving`, `bool isAttacking` flags checked across several `Update()` methods.

```csharp
/// <summary>One state in a unit's behavior state machine. Deliberately minimal —
/// no generic constraints, no attribute-based registration, no framework. A
/// newcomer should be able to read this interface in ten seconds.</summary>
public interface IUnitState
{
    void Enter(UnitStateMachine machine);
    void Tick(UnitStateMachine machine, float deltaTime);
    void Exit(UnitStateMachine machine);
}

public sealed class UnitStateMachine : MonoBehaviour
{
    private IUnitState _current;

    public void ChangeState(IUnitState next)
    {
        _current?.Exit(this);
        _current = next;
        _current?.Enter(this);
    }

    private void Update() => _current?.Tick(this, Time.deltaTime);
}

public sealed class IdleState : IUnitState
{
    public void Enter(UnitStateMachine machine) { }
    public void Tick(UnitStateMachine machine, float deltaTime) { }
    public void Exit(UnitStateMachine machine) { }
}

public sealed class MovingState : IUnitState
{
    // why: a state can hold its own short-lived data (the path being walked)
    // instead of stuffing it onto the state machine or the unit's components.
    private readonly List<Vector2Int> _path;

    public MovingState(List<Vector2Int> path) => _path = path;

    public void Enter(UnitStateMachine machine) { /* start the glide */ }
    public void Tick(UnitStateMachine machine, float deltaTime) { /* advance it */ }
    public void Exit(UnitStateMachine machine) { /* snap to the final cell */ }
}
```

The same shape covers turn flow — `IBattlePhase` / `BattleStateMachine` with `PlayerTurnState`, `EnemyTurnState`, `ResolvingState` — owned by `TurnManager` (registered in the service locator, since other systems need to ask "whose turn is it").

**Rules:**
- One state = one class. No `switch` on an enum standing in for a state machine.
- States live in `Scripts/StateMachines/`.
- A state may hold data scoped to that state's lifetime (like `MovingState._path` above) — that data doesn't belong on the machine or the unit.

---

## 4. Command Pattern

Move, Attack, UseItem — every player or AI action is a command object with enough state to execute, and to undo where that matters.

```csharp
public interface ICommand
{
    bool CanExecute();
    void Execute();
    void Undo();
}

public sealed class MoveCommand : ICommand
{
    private readonly Movement _mover;
    private readonly Vector2Int _target;
    private Vector2Int _previousCell;

    public MoveCommand(Movement mover, Vector2Int target)
    {
        _mover = mover;
        _target = target;
    }

    public bool CanExecute() => _mover.CanMoveTo(_target);

    public void Execute()
    {
        _previousCell = _mover.Cell;
        _mover.SetCell(_target);
    }

    public void Undo() => _mover.SetCell(_previousCell);
}
```

```csharp
// --- Player: an InputView builds the command from a click ---
private void OnCellClicked(Vector2Int cell)
{
    var command = new MoveCommand(_selectedUnitMovement, cell);
    if (command.CanExecute()) { command.Execute(); }
}

// --- AI: builds and evaluates the SAME command type — no separate AI logic path ---
private ICommand ChooseBestMove(Movement mover, IReadOnlyList<Vector2Int> candidates)
{
    foreach (Vector2Int candidate in candidates)
    {
        var command = new MoveCommand(mover, candidate);
        if (command.CanExecute()) { return command; }
    }
    return null;
}
```

**Rules:**
- `Execute()` mutates state synchronously; if the action needs a visual (a glide, a swing animation), that's a component reacting to the resulting state change (`Movement.CellChanged`, etc.), not something `Execute()` blocks on.
- AI never has a bespoke "AI attacks" code path — it constructs and runs the same `AttackCommand` a player action would.
- `Undo()` only needs to be meaningful where undo is a real feature (e.g. a "confirm move" UI step). If a command type genuinely can't be undone, say so in a `// why:` comment on `Undo()` rather than a silent no-op.

---

## 5. MVP for UI

A UI **View** renders and forwards raw UI input. It never references a gameplay type. A **Presenter** mediates: it holds the gameplay-side references (event channels, service-located systems) and pushes plain data into the View.

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pure UI. No Health, no Movement, no gameplay type appears here —
/// that's what makes this swappable for a different UI implementation without
/// touching a single gameplay file.</summary>
public sealed class UnitInfoView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Slider _healthSlider;

    public void SetName(string unitName) => _nameText.text = unitName;
    public void SetHealth(int current, int max) => _healthSlider.value = (float)current / max;
}

/// <summary>Mediates between game state and the View. This is the ONLY class
/// allowed to know both UnitInfoView and Health exist.</summary>
public sealed class UnitInfoPresenter : MonoBehaviour
{
    [SerializeField] private UnitInfoView _view;
    [SerializeField] private UnitEventChannelSO _selectionChangedChannel;

    private void OnEnable() => _selectionChangedChannel.Raised += OnSelectionChanged;
    private void OnDisable() => _selectionChangedChannel.Raised -= OnSelectionChanged;

    private void OnSelectionChanged(Health unit)
    {
        _view.SetName(unit.name);
        _view.SetHealth(unit.Current, unit.Max);
    }
}
```

**Rules:**
- `Scripts/UI/Views/` files may reference other UI types (UGUI, TMP, UI Toolkit) and plain data (`string`, `int`, `Color`) — nothing from `Scripts/Components/`, `Scripts/Systems/`, or `Scripts/Data/`.
- `Scripts/UI/Presenters/` files are the bridge — they may reference both sides.
- A UI redesign (new View implementation) should never require touching a Presenter's gameplay-facing logic, and vice versa.

---

## 6. Service Locator for Shared Systems

One small, explicit registry — not a `GameContext` bundling everything, and not scattered `static Instance` fields. Each system registers itself by its own concrete type; each consumer fetches exactly the one type it needs.

```csharp
using System;
using System.Collections.Generic;

/// <summary>The one explicit registry in this project. Keyed by concrete type,
/// so a consumer only ever gets the ONE service it asked for — nobody receives
/// a bundle of unrelated systems the way a GameContext would hand out.</summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

    public static void Register<T>(T service) where T : class => Services[typeof(T)] = service;

    public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

    public static T Get<T>() where T : class
    {
        if (Services.TryGetValue(typeof(T), out object service)) { return (T)service; }
        throw new InvalidOperationException(
            $"{typeof(T).Name} is not registered yet. Fetch it in Start, not Awake — see the ordering rule below.");
    }
}
```

```csharp
[DefaultExecutionOrder(-100)]  // why: registers itself before any consumer's Awake/Start runs
public sealed class GridManager : MonoBehaviour
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;

    private void Awake() => ServiceLocator.Register<GridManager>(this);
    private void OnDestroy() => ServiceLocator.Unregister<GridManager>();

    public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
}
```

**Rules:**
- Only genuinely shared, scene-wide systems register (`GridManager`, `TurnManager`, `CombatSystem`). Per-unit components (`Health`, `Movement`) are never registered — they're reached via `GetComponent` by their own GameObject's siblings, or via an event channel by anything else.
- **The known gotcha: Unity doesn't guarantee `Awake` order across objects.** A consumer calling `ServiceLocator.Get<T>()` in its own `Awake` can run before the service's `Awake` has registered it. Two ways to dodge this:
  1. Give the service `[DefaultExecutionOrder(-100)]` (or lower) so it registers first, **and** fetch it in `Start` (which always runs after every `Awake`), not `Awake`.
  2. If a component genuinely needs the service inside its own `Awake`, that's a sign it should be a sibling component (`GetComponent`) instead of a service — reach for the locator only for things that are truly scene-wide.
- Still no `static Instance` per-class singletons anywhere else — the locator is the one explicit registry, not a pattern to reinvent per-system.
- Systems themselves stay MonoBehaviours (not plain C#) — one construction model (`GameObject` + `MonoBehaviour`) is simpler to teach than two, and it matches how components already work.

---

## Folder Structure

```
Assets/
  Scripts/
    Components/       Health.cs, Movement.cs, AttackAbility.cs, StatusEffectHandler.cs
    Systems/           GridManager.cs, TurnManager.cs, CombatSystem.cs (MonoBehaviours, service-registered)
    Services/          ServiceLocator.cs
    EventChannels/     UnitEventChannelSO.cs, VoidEventChannelSO.cs, ...
    StateMachines/     IUnitState.cs, UnitStateMachine.cs, IdleState.cs, MovingState.cs, IBattlePhase.cs, BattleStateMachine.cs
    Commands/          ICommand.cs, MoveCommand.cs, AttackCommand.cs, UseItemCommand.cs
    Data/              UnitStatsSO.cs, AbilitySO.cs, ItemSO.cs, EnemyTemplateSO.cs — the SO class DEFINITIONS
    UI/
      Views/           UnitInfoView.cs, TurnBannerView.cs — no gameplay references
      Presenters/      UnitInfoPresenter.cs — mediates
    Input/             The InputView-equivalent — reads input, calls Systems via the locator
    Utility/           IsoGrid.cs, PlaceholderSprites.cs — pure helpers, no Unity lifecycle
  Data/                The actual .asset instances (as opposed to their class defs in Scripts/Data)
    EventChannels/
    Units/
    Abilities/
  Prefabs/
    Units/
```

A file's folder is what the hooks use to classify it (`gateguard.sh`, `validate-architecture.sh`) — component/system/view naming doesn't follow one fixed suffix convention the way the old `*Model`/`*System`/`*View` rule did, so put new files in the right folder.

---

## Performance Notes

Full detail lives in [performance.md](performance.md). Two hot paths get **mandatory** treatment; everything else favors readability over micro-optimization:
- **Object pooling is required for VFX and projectiles** — `Instantiate`/`Destroy` churn on anything spawned per-hit or per-frame is not acceptable.
- **Grid pathfinding uses A* (or equivalent)** — flag any naive/brute-force pathfinding (e.g. flood-fill without a priority queue, or recomputing the full grid every query) for replacement.

## No `.asmdef` Files Yet

Same as before: single default assembly. `internal` is meaningless until that changes — don't reach for it. Add assembly definitions once the codebase grows enough to need compile-time enforcement of these layer boundaries instead of the grep-based hooks.
