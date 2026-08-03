---
name: unity-fix
description: "Diagnoses and fixes a Unity bug — reads console errors, checks common causes, applies targeted fix, verifies via MCP."
user-invocable: true
args: bug_description
---

# /unity-fix — Diagnose and Fix a Bug

Fix the issue described by the user: **$ARGUMENTS**

## Agent Routing

Use the `unity-fixer` agent (opus — deep investigation) for all bugs regardless of size.

## Workflow

Use the selected fixer agent to:

1. **Gather evidence:**
   - Read Unity console via `read_console` MCP for errors, warnings, stack traces
   - Search the codebase for the error message or related code
   - If the user pasted an error, parse it for file name, line number, and error type

2. **Diagnose** — check these common Unity causes in order:
   - NullReferenceException → missing reference, destroyed object, execution order
   - Missing Script → file/class name mismatch
   - Serialization data loss → field renamed without FormerlySerializedAs
   - Coroutine stopped → SetActive(false) or Destroy
   - Service not found → `ServiceLocator.Get<T>()` called from Awake before the service registered (see `architecture.md` §6's ordering rule)
   - Build failure → UnityEditor in runtime, platform defines

3. **Fix** — create a branch if not already on one (`git checkout -b fix/<short-description>` — `main` is protected and `require-feature-branch.sh` blocks committing there directly), then apply the minimal targeted fix. Don't refactor surrounding code.

4. **Verify:**
   - Check console via `read_console` — error should be gone
   - If it was a serialization issue, warn about data that may need re-configuration

5. **Explain** what caused the bug and how the fix prevents recurrence.
