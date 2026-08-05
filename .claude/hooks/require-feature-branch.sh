#!/usr/bin/env bash
# ============================================================================
# require-feature-branch.sh — BLOCKING HOOK (standard profile)
# main is protected on GitHub — pushes to it are rejected. Blocks `git commit`
# while `main`/`master` is checked out, so that rejection is caught locally
# at commit time instead of at push time (or worse, several commits later).
#
# No two-stage "deny then allow on retry" here, unlike gateguard.sh — the
# fix is a single unambiguous command (git checkout -b ...), not a
# judgment call that benefits from a facts-first retry.
# ============================================================================
# Trigger: PreToolUse on Bash
# Exit:    2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

if [ -z "$COMMAND" ]; then
    exit 0
fi

# Only care about actual commit creation, not `git commit --help`,
# `git commit-graph`, status checks, etc.
if ! echo "$COMMAND" | grep -qE '(^|;|&&|\|)\s*git\s+commit(\s|$)'; then
    exit 0
fi

CURRENT_BRANCH=$(git branch --show-current 2>/dev/null || echo "")

case "$CURRENT_BRANCH" in
    main|master)
        ;;
    *)
        exit 0
        ;;
esac

echo "" >&2
echo "  RequireFeatureBranch — commit blocked on '$CURRENT_BRANCH'" >&2
echo "" >&2
echo "  main is protected — this push would be rejected on GitHub anyway." >&2
echo "  Create a branch first:" >&2
echo "" >&2
echo "    git checkout -b <feature|fix|chore>/<short-description>" >&2
echo "" >&2
echo "  Then retry the commit." >&2
echo "" >&2
unity_hook_block "RequireFeatureBranch: create a branch before committing (see .claude/rules/git-workflow.md)."
