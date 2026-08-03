---
name: unity-feature
description: "Plans and implements a Unity feature — identifies subsystems, loads skills, writes code, sets up scene elements via MCP."
user-invocable: true
args: feature_description
---

# /unity-feature — Implement a Feature

Plan and implement the feature described by the user: **$ARGUMENTS**

## Agent Routing

Use the `unity-coder` agent (opus — full architectural reasoning) for all features regardless of size; a small feature just gets a shorter plan in Phase 1.

## Phase 0: Sync

Before planning, sync with `main` (see `git-workflow.md`): `git checkout main && git pull origin main`. Skip if the working tree isn't clean — surface that to the user instead of pulling over it.

## Phase 1: Plan

1. **Analyze the feature** — identify which Unity subsystems are involved:
   - Input System? Physics? Animation? UI? Audio? Networking?
   - Which existing scripts/systems does this integrate with?

2. **Identify required scripts** — what new scripts to create, what existing ones to modify.

3. **Identify scene changes** — what GameObjects, components, or scene setup is needed.

4. **Present the plan** to the user before implementing. Include:
   - Scripts to create/modify
   - Scene changes via MCP
   - Dependencies on existing systems
   - Estimated complexity (simple / moderate / complex)

## Phase 2: Implement

1. **Create a feature branch** if not already on one — `git checkout -b feature/<short-description>` (`main` is protected; `require-feature-branch.sh` blocks committing there directly).
2. **Write C# code** using the `unity-coder` agent:
   - Follow all rules in `.claude/rules/`
   - Place scripts in the correct folder per `architecture.md`'s Folder Structure (`Components/`, `Systems/`, `EventChannels/`, `StateMachines/`, `Commands/`, `Data/`, `UI/Views/`, `UI/Presenters/`, `Input/`, `Utility/`)
   - Use `[SerializeField]` for inspector configuration
   - Add `[Header]` attributes for organization

3. **Set up scene elements** via MCP:
   - Create GameObjects with `batch_execute`
   - Configure components

4. **Check console** via `read_console` for compilation errors.

## Phase 3: Verify

1. Verify no console errors via `read_console`
2. Summarize what was created/modified
3. Explain how to test the feature
4. Note any manual steps needed (e.g., assigning references in Inspector)

## Phase 4: Auto-Verify (Optional)

After implementation, offer to run the `unity-verifier` agent for a verify-fix loop:
- Reviews all changed files for serialization safety, performance, and Unity-specific pitfalls
- Auto-fixes safe issues (missing FormerlySerializedAs, CompareTag, cached GetComponent, etc.)
- Re-verifies up to 3 iterations until clean
- Reports remaining items that require human judgment

Suggest: "Would you like me to run a verification pass on the changes?"
