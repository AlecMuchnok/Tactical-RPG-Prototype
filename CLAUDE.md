# CLAUDE.md - Project Context for AI Assistants

## Project Overview

Isometric, grid-based tactical RPG (Fire Emblem / XCOM-style turn-based tactics), targeting PC/desktop first.

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

**Not yet installed:** VContainer, MessagePipe, and UniTask. The architecture rules ([architecture.md](.claude/rules/architecture.md)) require these for DI, messaging, and async — add them before building new systems that need injection, cross-system messaging, or async/await.

### Assembly Definitions

No `.asmdef` files exist yet — the project is a single default assembly. Add assembly definitions (see [assembly-definitions skill](.claude/skills/core/assembly-definitions)) once the codebase grows enough to need compile-time dependency direction enforcement or faster iteration.

### Scenes in Build

0. `Assets/Scenes/SampleScene.unity`

---

## Coding Standards

Detailed, enforced rules live in `.claude/rules/`. Read the relevant file before writing code in that area:

- [architecture.md](.claude/rules/architecture.md) — Model-View-System pattern, VContainer DI, MessagePipe messaging, UniTask async, no singletons/service locators
- [csharp-unity.md](.claude/rules/csharp-unity.md) — naming conventions, encapsulation (private-by-default), file/type structure ordering
- [performance.md](.claude/rules/performance.md) — zero-allocation hot paths, draw call budget, atlasing/batching, UI canvas splitting
- [serialization.md](.claude/rules/serialization.md) — `[FormerlySerializedAs]` on renames, Unity's `== null` vs `is null`, `[SerializeReference]` for polymorphism
- [unity-specifics.md](.claude/rules/unity-specifics.md) — New Input System usage, `#if UNITY_EDITOR` guards, lifecycle order, no coroutines (UniTask only)

---

## Working With This Project

This project is driven through the Unity Editor via the UnityMCP tool integration — there is no separate CLI build or test command yet. Use MCP tools/agents for:

- Editor state, GameObjects, scenes, and assets — `mcp__UnityMCP__*` tools
- Running tests once they exist — `mcp__UnityMCP__run_tests` (no tests exist yet; see `unity-test` skill/agent to add them)
- Console output/errors — `mcp__UnityMCP__read_console`

Check `mcpforunity://custom-tools` for any project-specific MCP tools before assuming a capability doesn't exist.
