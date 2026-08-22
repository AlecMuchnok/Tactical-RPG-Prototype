# C# Style — Unity Conventions

## Field Declarations

- `[SerializeField] private` for inspector-exposed fields — never public
- Private/protected fields use `_lowerCamelCase`: `_moveSpeed`, `_health`
- Public fields use `lowerCamelCase`: `moveSpeed`, `health`
- Properties (public and private) use `UpperCamelCase`: `MoveSpeed`, `Health`
- `static readonly` fields use `UpperCamelCase`: `JumpHash`, `DefaultColor`
- `const` fields use `UpperCamelCase`: `MaxHealth`, `MaxPlayerCount` (matches the `static readonly` rule above and .NET convention — one casing for all compile-time constants)
- `readonly` for fields set only in constructor or Awake

```csharp
[SerializeField] private float _moveSpeed = 5f;
[SerializeField] private Transform _spawnPoint;

private Rigidbody _rigidbody;
private static readonly int JumpHash = Animator.StringToHash("Jump");
private const int MaxJumpCount = 3;
```

## Encapsulation (NON-NEGOTIABLE)

**Minimum visibility principle: everything is `private` unless proven otherwise.**

- Fields: `private` by default. Only use `[SerializeField] private` if the field MUST be configured in the Inspector. Do NOT add `[SerializeField]` speculatively — only when a designer/developer actually needs to tweak that value in the Inspector.
- Methods: `private` by default. Only make `public` if another class actually calls it. "Might be useful later" is NOT a reason.
- Properties: `private` by default. Expose a public getter only when another class reads it. Expose a public setter only when another class writes it.
- Classes/structs: this project has no `.asmdef` files, so everything is one assembly and `internal` is indistinguishable from `public` — don't reach for it. Use `sealed` instead (see *Types and Naming* below). Revisit this if assembly definitions are added later.
- Nested types: `private` unless external access is required.

**The test:** Before making anything non-private, identify the caller. If you can't name a concrete caller in the current codebase, it stays `private`. Agents must not generate speculative public API surface.

```csharp
// BAD — everything public "just in case"
public class EnemySystem
{
    public EnemyModel Model;                    // Should be private
    public void Initialize() { }                // Only called internally
    public int CalculateDamage() { return 5; }  // Only called internally
    public void TakeDamage(int amount) { }      // Actually called by CombatSystem — this one is fine
}

// GOOD — minimum viable visibility
public sealed class EnemySystem
{
    private readonly EnemyModel _model;
    
    private void Initialize() { }
    private int CalculateDamage() => 5;
    public void TakeDamage(int amount) { }  // CombatSystem calls this
}
```

**`[SerializeField]` discipline:**
```csharp
// BAD — serializing fields that don't need Inspector exposure
[SerializeField] private int _currentHealth;      // Runtime state, not config — don't serialize
[SerializeField] private bool _isInitialized;     // Internal flag — don't serialize
[SerializeField] private Transform _cachedTransform; // Cached ref — don't serialize

// GOOD — only serialize what designers configure
[SerializeField] private float _moveSpeed = 5f;   // Designer tweaks this in Inspector
[SerializeField] private GameObject _bulletPrefab; // Set via Inspector reference
private int _currentHealth;                         // Runtime state — plain private
private bool _isInitialized;                        // Internal flag — plain private
```

## Types and Naming

- Use `var` when the type is obvious from the right-hand side. Use explicit types when it isn't
- One type per file — file name MUST match the primary class/struct name (Unity requirement for MonoBehaviour)
- `sealed` by default — only unseal when inheritance is explicitly designed
- Explicit access modifiers on everything — no implicit `private`

## Structure Ordering

```csharp
public sealed class PlayerController : MonoBehaviour
{
    // 1. Serialized fields
    // 2. Private fields / cached references
    // 3. Properties
    // 4. Unity lifecycle: Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy
    // 5. Public methods
    // 6. Private methods
}
```

## Control Flow

- Braces always, even for single-line `if`/`for`/`while`
- **`foreach` is the default.** Reach for indexed `for` only in a genuine hot path (`Update`, `FixedUpdate`, `LateUpdate`) where the allocation/bounds-check difference actually matters, or when the loop body needs the index itself, not just the current element. An indexed loop outside a hot path that never touches the index is a rule violation, not a stylistic choice — don't default to `for` out of habit.
- No abbreviated loop variables — `for (int enemyIndex = 0; ...)` not `for (int i = 0; ...)`
- No magic strings — use `nameof()`, `Animator.StringToHash()`, `Shader.PropertyToID()`

## Comments

- No `why:` label. If an explanation is worth including, write it directly — the label adds a word without adding information.
- Never reference `architecture.md`, `performance.md`, or a section number from either. Those are project rules for people editing the code, not something a reader of the committed code can consult inline — a comment that cites a rule to justify itself is explaining the rule's existence, not the code's behavior.
- Never reference a plan file or planning step ("per the plan", "the plan's §4", "as planned") — plans are temporary and never committed, so the reference is stale the moment it's read.
- Never name a class, split, or design that doesn't exist in the current codebase (an earlier design option, a since-removed type). Describe what the code is, not what it used to be or almost was.
- Class/type summary comments are one to three sentences: what the thing is, not a design-rationale essay. A non-obvious *why* belongs on the specific line it explains, briefly — not folded into the class header.

## Other

- No LINQ in gameplay code
- `StringBuilder` for string building
- `CompareTag("tag")` not `tag == "tag"`
