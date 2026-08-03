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
- [git-workflow.md](.claude/rules/git-workflow.md) — `main` is protected: pull it before planning new work, branch before implementing (enforced by `require-feature-branch.sh`)

---

## Working With This Project

This project is driven through the Unity Editor via the UnityMCP tool integration — there is no separate CLI build or test command yet. Use MCP tools/agents for:

- Editor state, GameObjects, scenes, and assets — `mcp__UnityMCP__*` tools
- Running tests, once a test assembly exists — `mcp__UnityMCP__run_tests` (no tests exist yet, and writing them is currently out of scope for the `.claude/` toolkit — see `/unity-workflow`, `/unity-feature`, `/unity-review`, `/unity-fix`, `/unity-scene`, `/unity-interview` for what's supported)
- Console output/errors — `mcp__UnityMCP__read_console`

Check `mcpforunity://custom-tools` for any project-specific MCP tools before assuming a capability doesn't exist.

### Model Selection for Feature Work

**Plan on Opus, implement on Sonnet.** Requirements-gathering and the implementation plan (`/unity-interview`; Phases 1-2 of `/unity-workflow`; Phase 1 of `/unity-feature`) run on Opus — a wrong approach is expensive to unwind, so it's worth the frontier reasoning. Once a plan is approved, implementation (`unity-coder`, `unity-verifier`) is mechanical enough for Sonnet — both agents are already pinned to `model: sonnet` in their frontmatter. If you're driving one of these commands interactively rather than letting it delegate to a subagent, switch models yourself with `/model` at the plan→implement boundary; a command has no way to force the interactive session's model.
