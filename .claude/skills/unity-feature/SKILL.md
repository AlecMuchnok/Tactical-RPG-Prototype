---
name: unity-feature
description: "Plans and implements a Unity feature for this project — Opus writes an annotatable plan file, iterates on your inline comments until you approve it, then Sonnet implements it via Unity MCP."
user-invocable: true
args: feature_description
---

# /unity-feature — Plan, Annotate, Implement

Build: **$ARGUMENTS**

**Model tier:** Phase 1 (Plan) runs on **Opus** — a wrong approach is expensive to unwind. Phase 2 (Implement) runs on **Sonnet** — implementing an already-approved plan is mechanical. Switch with `/model` at the boundary between them; nothing does this automatically.

## Phase 0 — Sync

Before planning, sync with `main` (`git-workflow.md`):

```bash
git checkout main
git pull origin main
```

If the working tree isn't clean, stop and surface the uncommitted changes instead of pulling over them.

## Phase 1 — Plan (Opus)

1. Explore the codebase for existing scripts, systems, and patterns this feature should reuse or integrate with. Read `CLAUDE.md` and the relevant `.claude/rules/*.md` files for the areas this feature touches.
2. Write a plan file to `.claude/plans/<slug>.md` (gitignored — this is a working doc, not repo content). Required sections:
   - **What & why** — what's being built, what problem it solves
   - **Files to create/modify** — each with its target folder per `architecture.md`'s Folder Structure
   - **Architecture patterns used** — which of the six patterns in `architecture.md` apply, and why (component composition, SO data + event channels, state machines, command pattern, MVP for UI, service locator)
   - **Scene changes** — GameObjects, components, and Inspector wiring needed via MCP
   - **Serialization risk** — any renamed `[SerializeField]` needs `[FormerlySerializedAs]` per `serialization.md`
   - **Performance implications** — anything touching hot paths, VFX/projectile pooling, or pathfinding per `performance.md`
   - **Manual steps needed** — Inspector assignments, scene references, or asset creation (sprite atlases, etc.) that can't be done via MCP
3. Check the design against `architecture.md`'s existing constraints before presenting it: max inheritance depth 2, no 7th pattern without a recorded `// why:` justification, the newcomer-readability requirement (one team member is new to C#/Unity). Catching over-engineering here, before code exists, is cheaper than removing it after.
4. Send the plan file to the user (`SendUserFile`) and state its absolute path so it opens for editing.

### Annotate-and-iterate loop

5. The user annotates the plan file in place with `<!-- ALEC: ... -->` comments anywhere in the file.
6. Re-read the file. Address every `ALEC:` marker, edit the plan in place, replace each addressed marker with `<!-- RESOLVED: what changed -->`, and report the diff in chat.
7. Repeat from step 5 until the user states approval explicitly in chat. A question, "looks interesting," or silence is not approval — keep iterating.

## Phase 2 — Implement (Sonnet)

The plan is approved. Switch to Sonnet now (`/model sonnet`) if driving this interactively.

1. **Branch** — `git checkout -b <type>/<kebab-description>` (`feature/`, `fix/`, `chore/`), unless already on a branch created for this work. `main` is protected; `require-feature-branch.sh` blocks committing there directly.
2. Follow the approved plan file. Write C# per `.claude/rules/`.
3. Build scene elements via `mcp__UnityMCP__*` (use `batch_execute` where it helps).
4. Run `read_console` after each major step; fix compilation errors before continuing.
5. Close with a summary: what was built, files touched, console status, and the **Manual steps needed** list carried forward from the plan.

## Notes

- No separate critic phase — the annotate-and-iterate loop in Phase 1 is the critic, and it's you.
- No deslop/cleanup pass at the end of implementation. `architecture.md`'s constraints are checked during planning (step 3 above), and the `unity-review` skill's Over-engineering criterion covers post-hoc cleanup with an independent (Opus) reviewer. Running a self-review immediately after writing the code is the weakest place to catch it — the reasoning that justified an abstraction is still loaded. For an explicit standalone cleanup outside this flow, use `/simplify`.
