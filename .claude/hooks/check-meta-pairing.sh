#!/usr/bin/env bash
# H5 — PreToolUse (Bash|PowerShell), fires only on `git commit`. Denies committing
# an Assets/ file whose .meta counterpart isn't staged or already tracked, and
# vice versa. This only ever bites a collaborator — the committer's own Library
# cache papers over the mismatch locally. See .claude/rules/unity-specifics.md
# (".meta Files") and CLAUDE.md.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
CMD="$(hooklib::field '.tool_input.command')"

if [[ -z "$CMD" ]] || ! grep -qE 'git[[:space:]]+commit(\s|$)' <<<"$CMD"; then
    exit 0
fi

PROJECT_DIR="$(hooklib::project_dir)"

STAGED_PATHS="$(git -C "$PROJECT_DIR" diff --cached --diff-filter=AMR --name-only -- 'Assets/' 2>/dev/null || true)"

if [[ -z "$STAGED_PATHS" ]]; then
    exit 0
fi

MISSING=""
while IFS= read -r path; do
    [[ -z "$path" ]] && continue
    if [[ "$path" == *.meta ]]; then
        asset="${path%.meta}"
        if ! git -C "$PROJECT_DIR" ls-files --error-unmatch -- "$asset" >/dev/null 2>&1; then
            MISSING="$MISSING  $path  (orphaned — no asset '$asset' staged or tracked)"$'\n'
        fi
    else
        meta="${path}.meta"
        if ! git -C "$PROJECT_DIR" ls-files --error-unmatch -- "$meta" >/dev/null 2>&1; then
            MISSING="$MISSING  $path  (missing its .meta — '$meta' not staged or tracked)"$'\n'
        fi
    fi
done <<<"$STAGED_PATHS"

if [[ -n "$MISSING" ]]; then
    hooklib::guard_deny "CHECK_META_PAIRING" \
"DENIED — unpaired asset/.meta in this commit:

$(printf '%s' "$MISSING")
A missing .meta breaks that asset's GUID for anyone who pulls this commit — your
local Library cache is hiding it from you right now. Stage the missing side
('git add <path>') before committing."
fi

exit 0
