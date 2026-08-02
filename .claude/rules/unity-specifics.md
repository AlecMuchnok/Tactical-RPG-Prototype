# Unity-Specific Rules

## Editor vs Runtime

```csharp
// Runtime code (Assets/Scripts/) — NEVER use UnityEditor unguarded
#if UNITY_EDITOR
using UnityEditor;
#endif

private void OnValidate()
{
    #if UNITY_EDITOR
    EditorUtility.SetDirty(this);
    #endif
}
```

- Code in `Editor/` folder: editor-only, excluded from builds automatically
- Code outside `Editor/`: must guard any `UnityEditor` usage with `#if UNITY_EDITOR`
- Forgetting the guard: compiles in Editor, **fails on build** with no warning until build time

## Platform Defines

This game ships to **desktop and console**. It does not ship to mobile — `UNITY_ANDROID` and `UNITY_IOS` should never appear in this codebase.

| Define | True when |
|---|---|
| `UNITY_EDITOR` | Running in the Editor, on any build target |
| `UNITY_STANDALONE` | Any desktop build (Win / macOS / Linux) |
| `UNITY_STANDALONE_WIN` / `_OSX` / `_LINUX` | That specific desktop target |
| `UNITY_GAMECORE` | Xbox (Game Core) |
| `UNITY_PS5` | PlayStation 5 |
| `UNITY_SWITCH` | Nintendo Switch |

`UNITY_EDITOR` is defined *in addition to* the active build target's define — code inside `#if UNITY_STANDALONE_WIN` also compiles in the Editor when the target is Windows.

```csharp
// GOOD — every branch is covered, so the code compiles on every target
#if UNITY_GAMECORE || UNITY_PS5 || UNITY_SWITCH
    ShowGamepadPrompts();
#elif UNITY_STANDALONE
    ShowKeyboardMousePrompts();
#else
    ShowKeyboardMousePrompts();   // Editor and anything else
#endif

// BAD — silently compiles to nothing on console. No error, no warning, no prompts.
#if UNITY_STANDALONE
    ShowKeyboardMousePrompts();
#endif
```

Console-specific code (save-data APIs, achievements, certification) belongs behind these defines with a desktop fallback, not sprinkled through gameplay Systems.

## The `?.` Operator Trap

```csharp
// DANGEROUS — bypasses Unity's destroyed-object detection
_target?.TakeDamage(10);  // Calls TakeDamage on destroyed objects!

// SAFE — Unity's == operator detects destroyed objects
if (_target != null)
{
    _target.TakeDamage(10);
}
```

Unity overrides `==` to return `true` when comparing destroyed objects to `null`. The `?.` operator uses C# reference equality, which does NOT detect destroyed objects. This is the #1 most subtle Unity bug.

## Lifecycle Order

```
Awake()       → called once when object is created (even if disabled)
OnEnable()    → called when object becomes active
Start()       → called once before first Update (only if enabled)
FixedUpdate() → physics tick (0.02s default)
Update()      → every frame
LateUpdate()  → every frame, after all Updates
OnDisable()   → called when object becomes inactive
OnDestroy()   → called when object is destroyed
```

- Don't depend on Awake order across objects — use `[DefaultExecutionOrder]` or explicit init
- `OnDisable` is called before `OnDestroy` — unsubscribe events in `OnDisable`
- `Start` is NOT called if the object is never enabled

## Threading

Unity API is main-thread only. Background threads cannot:
- Access `Transform`, `GameObject`, `Component`
- Call `Instantiate`, `Destroy`
- Access `Time`, `Input`, `Physics`

```csharp
// Return to main thread with Awaitable:
await Awaitable.MainThreadAsync();

// The reverse direction, for CPU-heavy work that shouldn't block the main thread:
await Awaitable.BackgroundThreadAsync();

// SynchronizationContext.Current.Post also works, but you should not need it —
// Awaitable.MainThreadAsync() covers this case.
```

## Async — Use Awaitable

`StartCoroutine` / `IEnumerator` / `yield return` are **non-preferred** for new code. Use `UnityEngine.Awaitable` (built into Unity 6 — no package needed) instead.

Reasons to prefer `Awaitable`:
- Coroutines stop silently when `gameObject.SetActive(false)` and don't resume
- Coroutines have no cancellation, error handling, or return values
- Coroutines allocate on the heap

```csharp
// Non-preferred — coroutine
private IEnumerator WaitAndDo()
{
    yield return new WaitForSeconds(1f);
    DoSomething();
}

// Preferred — Awaitable
private async Awaitable WaitAndDoAsync(CancellationToken token)
{
    await Awaitable.WaitForSecondsAsync(1f, token);
    DoSomething();
}
```

Always pass a `CancellationToken`. In MonoBehaviours: `destroyCancellationToken` (built in — no extension method, no manual `CancellationTokenSource`). In Systems: own a `CancellationTokenSource` and cancel it in `Dispose()`. See `architecture.md`'s Async section for the full rules, including why `async void` is banned.

## DontDestroyOnLoad

Use sparingly. Prefer a bootstrapper scene pattern:
```
BootstrapScene (loads once, contains persistent services)
    → Additively loads GameScene, MenuScene, etc.
```

## Transform

- `transform.SetParent(parent, false)` — use `worldPositionStays: false` to preserve local transform
- `Application.isPlaying` — check in OnDisable/OnDestroy to avoid cleanup during editor domain reload

## Time

- `Time.deltaTime` in `Update` and `LateUpdate`
- `Time.fixedDeltaTime` in `FixedUpdate`
- Never use `Time.deltaTime` in `FixedUpdate` (it equals `fixedDeltaTime` there, but it's confusing)
- `Time.unscaledDeltaTime` for pause-independent logic (UI animations, etc.)

## Component Attributes

```csharp
[RequireComponent(typeof(Rigidbody))]        // Auto-adds Rigidbody, prevents removal
[DisallowMultipleComponent]                   // Prevents duplicate components
[DefaultExecutionOrder(-100)]                 // Runs before default scripts
[SelectionBase]                               // Click selects this object, not children
```

## Input System (NON-NEGOTIABLE)

The New Input System package is **mandatory**. Legacy `Input.GetKey` / `Input.GetAxis` / `Input.GetButton` / `Input.GetMouseButton` is **BLOCKED** by the `block-legacy-input` PreToolUse hook.

### Generated C# Class (Preferred Approach)

1. Create `Assets/Input/PlayerControls.inputactions` — define all action maps
2. Enable "Generate C# Class" in the asset inspector → generates `PlayerControls.cs`
3. Use the generated class in InputView (see architecture rules)

### Critical Lifecycle Rules

```csharp
// InputView — the ONLY place that touches PlayerControls
public sealed class InputView : MonoBehaviour
{
    private PlayerControls _controls;
    private PlayerSystem _playerSystem;

    private void Awake()
    {
        _controls = new PlayerControls();
    }

    public void Init(PlayerSystem playerSystem)
    {
        _playerSystem = playerSystem;
    }

    // MANDATORY: Enable actions in OnEnable
    private void OnEnable()
    {
        _controls.Player.Enable();
        _controls.Player.Jump.performed += OnJump;
        _controls.Player.Attack.performed += OnAttack;
    }

    // MANDATORY: Disable actions and unsubscribe in OnDisable
    private void OnDisable()
    {
        _controls.Player.Jump.performed -= OnJump;
        _controls.Player.Attack.performed -= OnAttack;
        _controls.Player.Disable();
    }

    // Read continuous input in Update, cache for systems
    private void Update()
    {
        Vector2 moveInput = _controls.Player.Move.ReadValue<Vector2>();
        _playerSystem.SetMoveInput(moveInput);
    }

    private void OnJump(InputAction.CallbackContext ctx) => _playerSystem.Jump();
    private void OnAttack(InputAction.CallbackContext ctx) => _playerSystem.Attack();
}
```

### Rules

| Rule | Why |
|------|-----|
| **Enable in OnEnable, Disable in OnDisable** | Missing Enable = zero input received. Missing Disable = ghost callbacks, leaks |
| **Subscribe in OnEnable, unsubscribe in OnDisable** | Every `+=` must have a matching `-=` in OnDisable |
| **Read continuous input in Update** | FixedUpdate runs at different rate — input can be missed |
| **Cache input, apply in FixedUpdate** | Physics forces use cached values, not raw reads |
| **Never use legacy Input API** | Blocked by the `block-legacy-input` hook. Use `Mouse.current` / `Keyboard.current` / `Gamepad.current`, or a generated `PlayerControls` class |
| **InputView is a View** | Pure thin adapter — reads input, calls Systems. Zero logic |
| **One InputView per scene** | Centralized input reading prevents duplicate subscriptions |

### Action Map Switching

```csharp
// Gameplay → UI (e.g., opening pause menu)
_controls.Player.Disable();
_controls.UI.Enable();

// UI → Gameplay (closing menu)
_controls.UI.Disable();
_controls.Player.Enable();
```

Always disable the current map **before** enabling the next. Never leave multiple gameplay maps enabled simultaneously.

## .meta Files

- NEVER edit manually
- ALWAYS commit alongside their asset
- Missing .meta = Unity regenerates GUID = all references break
- Orphaned .meta = clutter and potential conflicts
