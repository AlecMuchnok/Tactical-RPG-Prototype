#!/usr/bin/env bash
# H4 — PreToolUse (Bash|PowerShell). Denies commands that discard work or Unity
# state, in both shells since this project has two. See .claude/rules/git-workflow.md.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
CMD="$(hooklib::field '.tool_input.command')"

if [[ -z "$CMD" ]]; then
    exit 0
fi

# --- git reset --hard ----------------------------------------------------
if grep -qE 'git[[:space:]]+reset\b.*--hard' <<<"$CMD"; then
    hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — 'git reset --hard' discards uncommitted work with no recovery path.
If you need to discard specific changes, confirm with the user first, or use
'git stash' so the work is recoverable."
fi

# --- git clean -f...d (any flag order/spelling) --------------------------
if grep -qE 'git[[:space:]]+clean\b' <<<"$CMD"; then
    HAS_FORCE=0
    HAS_DIRS=0
    if grep -qE '(^|[[:space:]])(-[a-zA-Z]*f[a-zA-Z]*|--force)([[:space:]]|$)' <<<"$CMD"; then HAS_FORCE=1; fi
    if grep -qE '(^|[[:space:]])(-[a-zA-Z]*d[a-zA-Z]*|--force)([[:space:]]|$)' <<<"$CMD"; then HAS_DIRS=1; fi
    if [[ "$HAS_FORCE" == "1" && "$HAS_DIRS" == "1" ]]; then
        hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — 'git clean -fd' (or equivalent) permanently deletes untracked files and
directories with no recovery path. Run 'git clean -ndx' first to see what would be
removed, and confirm with the user before deleting."
    fi
fi

# --- git checkout/restore that wipes the working tree --------------------
if grep -qE 'git[[:space:]]+(checkout|restore)[[:space:]]+(--[[:space:]]+)?\.([[:space:]]|$)' <<<"$CMD"; then
    hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — this discards all uncommitted changes in the working tree. Run
'git status' and confirm with the user, or stash first ('git stash -u') so the
work is recoverable."
fi

# --- git branch -D --------------------------------------------------------
if grep -qE 'git[[:space:]]+branch[[:space:]]+(-D|.*--delete[[:space:]]+--force)' <<<"$CMD"; then
    hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — 'git branch -D' force-deletes a branch even with unmerged commits.
Use 'git branch -d' (lowercase) if it's safe to delete, or confirm with the user."
fi

# --- git push --force (allow --force-with-lease) --------------------------
if grep -qE 'git[[:space:]]+push\b' <<<"$CMD"; then
    CMD_NO_LEASE="$(sed -E 's/--force-with-lease(=[^[:space:]]*)?//g' <<<"$CMD")"
    if grep -qE '(--force\b|(^|[[:space:]])-f([[:space:]]|$))' <<<"$CMD_NO_LEASE"; then
        hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — plain 'git push --force' can overwrite upstream commits other people
have. Use '--force-with-lease' if a force push is truly needed, and confirm with
the user first."
    fi
fi

# --- recursive delete of Unity-critical directories or any .meta file ----
if grep -qE '(rm[[:space:]].*-[a-zA-Z]*r[a-zA-Z]*f|rm[[:space:]].*-[a-zA-Z]*f[a-zA-Z]*r|Remove-Item.*-Recurse.*-Force|Remove-Item.*-Force.*-Recurse)' <<<"$CMD"; then
    if grep -qE '(^|[/\\[:space:]])(Library|Assets|ProjectSettings|Packages)([/\\[:space:]]|$)' <<<"$CMD"; then
        hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — recursive delete targeting a Unity-critical directory (Library, Assets,
ProjectSettings, or Packages). This is very likely to break the project. Confirm
with the user first."
    fi
fi
if grep -qE '(rm[[:space:]]|Remove-Item)' <<<"$CMD" && grep -qE '\*\.meta([[:space:]]|$)' <<<"$CMD"; then
    hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — deleting .meta files breaks every reference to the paired asset (GUID
loss). If assets themselves are being removed, delete them (and their .meta) via
the Unity Editor or mcp__UnityMCP__* tools, not a raw shell delete."
fi

# --- git commit on a protected branch -------------------------------------
if grep -qE 'git[[:space:]]+commit(\s|$)' <<<"$CMD"; then
    PROJECT_DIR="$(hooklib::project_dir)"
    BRANCH="$(git -C "$PROJECT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || true)"
    if [[ "$BRANCH" == "main" || "$BRANCH" == "master" ]]; then
        hooklib::guard_deny "GUARD_DESTRUCTIVE_COMMANDS" \
"DENIED — committing directly on '$BRANCH' (git-workflow.md: main is protected).
Create a branch first:
  git checkout -b <type>/<short-description>
(prefixes: feature/, fix/, chore/). Escape hatch if truly needed:
DISABLE_HOOK_GUARD_DESTRUCTIVE_COMMANDS=1 — prefer the branch."
    fi
fi

exit 0
