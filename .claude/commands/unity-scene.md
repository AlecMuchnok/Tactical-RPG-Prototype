---
name: unity-scene
description: "Build or modify a Unity scene entirely via MCP — GameObjects, hierarchy, and camera setup."
user-invocable: true
args: scene_description
---

# /unity-scene — Build a Scene

Build or modify a scene based on the description: **$ARGUMENTS**

## Workflow

Use the `unity-scene-builder` agent to:

1. **Plan the scene** — identify GameObjects, components, hierarchy, lighting, and camera setup
2. **Create or load scene** via `manage_scene` MCP (use the `2d_basic` template — this project is 2D isometric)
3. **Build hierarchy** using `batch_execute`:
   - Environment objects (grid, tiles)
   - Unit spawn points
   - Camera (plain orthographic `Camera`, framed by a Component/System per `architecture.md` — no Cinemachine, it isn't installed)
   - System objects (`GridManager`, `TurnManager`, etc. — registered with `ServiceLocator`)
4. **Configure components** via `manage_components`
5. **Set up camera** via `manage_camera` (orthographic size, position, framing)
6. **Verify** via `read_console` — no errors

## Hierarchy Convention
```
@Environment/ — static world geometry
@Characters/  — player, NPCs, enemies
@Cameras/     — main camera, virtual cameras
@Lighting/    — lights, reflection probes
@UI/          — canvases
@Systems/     — managers, spawners
_Dynamic/     — parent for runtime-spawned objects
```

Report the complete scene structure when done.
