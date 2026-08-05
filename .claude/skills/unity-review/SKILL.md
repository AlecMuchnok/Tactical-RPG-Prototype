---
name: unity-review
description: "Full Unity-aware code review of the current branch — Opus checks changed files against the project's serialization, lifecycle, performance, architecture, and platform rules, then either confirms clean or writes an annotatable fix plan for Sonnet to implement."
user-invocable: true
---

# /unity-review — Review, Annotate, Fix

**Model tier:** the review pass itself runs on **Opus** — it's the thorough verification of what Sonnet implemented, and it's the only check left on several rules whose enforcement hooks were removed for being unreviewed scaffold. If findings require fixes, switch to **Sonnet** (`/model sonnet`) to implement them, per the same tier split as `unity-feature`.

## Scope

Files changed on the current branch since it diverged from `main`, plus any uncommitted changes:

```bash
git diff --name-only $(git merge-base main HEAD) HEAD
git status --short
```

## Checklist

Check every changed `.cs` file (and touched assets) against:

- **Serialization** — `[FormerlySerializedAs]` present on every renamed serialized field; `== null`, never `is null` or `?.`, on Unity object references (`serialization.md`, `unity-specifics.md`). Nothing automated catches the rename case, so check it explicitly.
- **Lifecycle** — the subscribe/unsubscribe pairing table in `architecture.md §2` (sibling components: `Awake`/`OnDestroy`; event channels, input, UI: `OnEnable`/`OnDisable`); `ServiceLocator.Get<T>()` called in `Start`, not `Awake`, unless the service has `[DefaultExecutionOrder]` guaranteeing it registers first.
- **Performance** — zero heap allocations in `Update`/`FixedUpdate`/`LateUpdate`; never `renderer.material` (use `sharedMaterial` + `MaterialPropertyBlock`); VFX/projectiles come from a pool; grid pathfinding uses A* or equivalent, never brute-force (`performance.md`).
- **Architecture** — files in the correct folder per `architecture.md`'s Folder Structure; Views hold no gameplay types; no direct cross-system reference where an event channel belongs; component composition over inheritance — flag any `class X : Y` on a project type exceeding depth 2, or a new abstract base class where a component or SO reference would serve.
- **Over-engineering** — abstractions with a single implementation, wrapper classes adding no behavior, dead code, comments that restate the code, defensive checks with no plausible failure mode. Judge against `architecture.md`'s stated constraints (max inheritance depth 2, no 7th pattern without a recorded `// why:`). Prefer leaving code alone over a speculative simplification — false positives cost more than missed bloat.
- **Platform** — no `UNITY_ANDROID`/`UNITY_IOS`; every `#if` platform chain has an `#else` fallback so it doesn't silently compile to nothing on an uncovered target (`unity-specifics.md`).
- **Style** — private-by-default fields/methods/properties; `[SerializeField]` only where a designer actually needs Inspector access; braces always; no LINQ in gameplay code; `CompareTag`, not `tag ==` (`csharp-unity.md`).
- **Unity assets** — `.meta` files committed alongside their assets; no `.unity`/`.prefab`/`.asset` YAML hand-edited as text. Nothing blocks this at the tool-call level, so it's a review item.

## Outcome

Pick exactly one:

- **Clean.** State plainly what was checked and that no issues were found. Do not manufacture findings to appear thorough.
- **Findings.** Write `.claude/plans/review-<slug>.md` (gitignored). One entry per finding: `file:line`, the rule it violates, the concrete failure it causes (not just "this is bad practice"), and a proposed fix. Send the file to the user and state its path.

### If there are findings — annotate-and-iterate, same as `unity-feature`

1. The user annotates the review file in place with `<!-- ALEC: ... -->` comments.
2. Re-read the file, address every marker, edit the plan in place, mark each `<!-- RESOLVED: what changed -->`, report the diff.
3. Repeat until the user states approval explicitly — not silence, not a question.
4. On approval, switch to Sonnet and implement the fixes exactly as specified in the approved plan. Re-run `read_console` after fixes to confirm no regressions.
