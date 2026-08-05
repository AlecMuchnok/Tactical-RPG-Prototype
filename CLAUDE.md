# CLAUDE.md - Project Context for AI Assistants

## Project Overview

Isometric, grid-based tactical RPG (Fire Emblem / XCOM-style turn-based tactics), targeting PC/desktop and console.

Current milestone: prototyping a 10x10 isometric grid with a single player unit that can be selected and glide-moved along an orthogonal path.

| Property         | Value |
|------------------|-------|
| **Unity Version** | 6000.5.6f1 |
| **Render Pipeline** | Universal Render Pipeline (URP) |
| **Project Root**  | `/c/Unity Projects/Tactical RPG Prototype` |

### Key Packages

- Input System (New Input System — mandatory, see [unity-specifics.md](.claude/rules/unity-specifics.md))
- 2D Sprite, 2D Animation, 2D Aseprite/PSD Importer, 2D Spriteshape, 2D Tilemap
- Visual Scripting
- Unity Test Framework
- Unity MCP for Unity (`com.coplaydev.unity-mcp`) — enables MCP-driven Editor automation

**No heavy frameworks.** No Unity ECS/DOTS, no Zenject/VContainer — the architecture stack is deliberately lightweight: component composition, ScriptableObject data + event channels, state machines, the command pattern, MVP for UI, and a small explicit service locator (not a God Container) for shared systems like `GridManager`/`TurnManager`. Async is `UnityEngine.Awaitable`. See [architecture.md](.claude/rules/architecture.md) for the full stack and the reasoning behind each piece.

### Assembly Definitions

No `.asmdef` files exist yet — the project is a single default assembly. Add assembly definitions once the codebase grows enough to need compile-time dependency direction enforcement or faster iteration.

### Scenes in Build

0. `Assets/Scenes/SampleScene.unity`

---

## Coding Standards

Detailed, enforced rules live in `.claude/rules/`. Read the relevant file before writing code in that area:

- [architecture.md](.claude/rules/architecture.md) — component composition, SO data + event channels, state machines, command pattern, MVP for UI, service locator for shared systems
- [csharp-unity.md](.claude/rules/csharp-unity.md) — naming conventions, encapsulation (private-by-default), file/type structure ordering
- [performance.md](.claude/rules/performance.md) — zero-allocation hot paths, draw call budget, atlasing/batching, UI canvas splitting
- [serialization.md](.claude/rules/serialization.md) — `[FormerlySerializedAs]` on renames, Unity's `== null` vs `is null`, `[SerializeReference]` for polymorphism
- [unity-specifics.md](.claude/rules/unity-specifics.md) — New Input System usage, `#if UNITY_EDITOR` guards, lifecycle order, `Awaitable` over coroutines, desktop/console platform defines
- [git-workflow.md](.claude/rules/git-workflow.md) — `main` is protected: pull it before planning new work, branch before implementing (enforced by `guard-destructive-commands.sh`)

---

## Hooks

`.claude/hooks/*.sh` mechanically enforce the subset of the rules above where a mistake is expensive or unrecoverable — a renamed `[SerializeField]` silently wiping every configured value, a hand-edited `.meta` breaking a GUID, a compile error slipping past "done." Everything else in the rules files is advisory, caught by review rather than a hook.

**One-time setup:** the hooks are bash scripts and need `bash` on `PATH` (Git for Windows ships one, just not wired in by default):

```bash
powershell -Command "[Environment]::SetEnvironmentVariable('Path', [Environment]::GetEnvironmentVariable('Path','User') + ';C:\Program Files\Git\bin', 'User')"
```

Restart Claude Code/your terminal afterward. This adds only `C:\Program Files\Git\bin` (just `bash.exe`/`sh.exe`/`git.exe` — not the 375-executable `Git\usr\bin`, which would shadow Windows builtins).

Every hook honors `DISABLE_UNITY_HOOKS=1` (turn all off), `UNITY_HOOK_MODE=warn` (downgrade every blocking hook to an advisory message), and its own `DISABLE_HOOK_<NAME>=1`. Set these in `.claude/settings.local.json` (copy from `.claude/settings.local.json.template`, which is git-ignored).

| Hook | Event | Blocks | Kill switch |
|---|---|---|---|
| `protect-unity-assets.sh` | PreToolUse | Hand-editing `.meta`/`.unity`/`.prefab`/`.asset`/etc, or anything under `Library/`, `Temp/`, `ProjectSettings/` | `DISABLE_HOOK_PROTECT_UNITY_ASSETS` |
| `guard-serialized-rename.sh` | PreToolUse | A `[SerializeField]`/public field renamed without `[FormerlySerializedAs]` — escape: `// serialization:ignore` | `DISABLE_HOOK_GUARD_SERIALIZED_RENAME` |
| `guard-build-breakers.sh` | PreToolUse | Unguarded `UnityEditor` usage outside `Editor/`, legacy `Input.*` (escape: `// input:ignore`), `async void`, `UNITY_ANDROID`/`UNITY_IOS` | `DISABLE_HOOK_GUARD_BUILD_BREAKERS` |
| `guard-destructive-commands.sh` | PreToolUse | `git reset --hard`, `clean -fd`, `push --force`, `branch -D`, recursive deletes of `Library`/`Assets`/`ProjectSettings`/`Packages`/`*.meta`, `git commit` on `main`/`master` | `DISABLE_HOOK_GUARD_DESTRUCTIVE_COMMANDS` |
| `check-meta-pairing.sh` | PreToolUse (on `git commit`) | Committing an `Assets/` file whose `.meta` isn't staged/tracked, or vice versa | `DISABLE_HOOK_CHECK_META_PAIRING` |
| `lint-csharp-rules.sh` | PostToolUse, advisory | Nothing — reports `performance.md`/`csharp-unity.md`/`architecture.md`/etc violations in the edited file as feedback | `DISABLE_HOOK_LINT_CSHARP_RULES` |
| `session-context.sh` | SessionStart | — injects branch/dirty state, Unity Editor/unity-mcp reachability, misplaced-script count | `DISABLE_HOOK_SESSION_CONTEXT` |
| `check-unity-console.sh` | Stop | Ending the turn while Unity reports a C# compile error (`error CS####`) — via unity-mcp `read_console`, falling back to scanning `Editor.log` if the MCP bridge is unreachable | `DISABLE_HOOK_CHECK_UNITY_CONSOLE` |
| `session-cleanup.sh` | SessionEnd | — clears this session's `.claude/state/` files | `DISABLE_HOOK_SESSION_CLEANUP` |

`_lib.sh` is a shared library sourced by the rest, not a hook itself.

---

## Working With This Project

This project is driven through the Unity Editor via the UnityMCP tool integration — there is no separate CLI build or test command yet. Use MCP tools/agents for:

- Editor state, GameObjects, scenes, and assets — `mcp__UnityMCP__*` tools
- Running tests, once a test assembly exists — `mcp__UnityMCP__run_tests` (no tests exist yet, and writing them is currently out of scope for the `.claude/` toolkit — see [unity-feature](.claude/skills/unity-feature/SKILL.md) and [unity-review](.claude/skills/unity-review/SKILL.md) for what's supported)
- Console output/errors — `mcp__UnityMCP__read_console`

Check `mcpforunity://custom-tools` for any project-specific MCP tools before assuming a capability doesn't exist.

### Model Selection for Feature Work

**Plan on Opus, implement on Sonnet.** The `unity-feature` and `unity-review` skills both write an approval-gated plan file first and only implement after you approve it. There's no agent frontmatter to pin the model anymore, so switching with `/model` at the plan→implement boundary is the only mechanism — both skills state the switch explicitly when they reach it. A wrong approach is expensive to unwind, so planning stays on the frontier tier; implementing an already-approved plan is mechanical enough for Sonnet.
