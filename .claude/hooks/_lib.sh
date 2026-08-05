#!/usr/bin/env bash
# ============================================================================
# _lib.sh — Shared hook library (sourced, not executed)
# Provides kill switches, hook profiles, and shared utilities for all hooks.
#
# Environment variables:
#   DISABLE_UNITY_HOOKS=1          — bypass ALL hooks (exit 0 immediately)
#   DISABLE_HOOK_<NAME>=1          — bypass a specific hook (name uppercased, hyphens→underscores)
#   UNITY_HOOK_MODE=warn           — downgrade blocking hooks to warnings (exit 0 instead of 2)
#   UNITY_HOOK_PROFILE=standard    — hook profile: minimal|standard|strict (default: standard)
#
# Hook profiles control which hooks are active:
#   minimal  — only the legacy-input and feature-branch blockers
#   standard — safety + quality warnings, including gateguard (default)
#   strict   — reserved for hooks that opt into a stricter tier than standard
#
# Usage in hook scripts (add after set -euo pipefail):
#   SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
#   HOOK_PROFILE_LEVEL="standard"   # minimal|standard|strict
#   source "${SCRIPT_DIR}/_lib.sh"
# ============================================================================

# --- Profile levels (numeric for comparison) ---
_profile_to_num() {
    case "$1" in
        minimal)  echo 1 ;;
        standard) echo 2 ;;
        strict)   echo 3 ;;
        *)        echo 2 ;; # default to standard
    esac
}

_ACTIVE_PROFILE="${UNITY_HOOK_PROFILE:-standard}"
_ACTIVE_PROFILE_NUM=$(_profile_to_num "$_ACTIVE_PROFILE")

# If the hook declared a required profile level, check it
if [ -n "${HOOK_PROFILE_LEVEL:-}" ]; then
    _REQUIRED_NUM=$(_profile_to_num "$HOOK_PROFILE_LEVEL")
    if [ "$_REQUIRED_NUM" -gt "$_ACTIVE_PROFILE_NUM" ]; then
        exit 0  # hook's profile level exceeds active profile — skip silently
    fi
fi

# Global kill switch — disable all hooks
if [ "${DISABLE_UNITY_HOOKS:-}" = "1" ]; then
    exit 0
fi

# Per-hook kill switch — derive hook name from caller's filename
_HOOK_BASENAME="$(basename "${BASH_SOURCE[1]}" .sh)"
_HOOK_ENV_NAME="DISABLE_HOOK_$(echo "$_HOOK_BASENAME" | tr '[:lower:]-' '[:upper:]_')"

if [ "${!_HOOK_ENV_NAME:-}" = "1" ]; then
    exit 0
fi

# --- Shared paths ---
# Resolve project-local state directory, falling back to /tmp
_resolve_state_dir() {
    local git_root
    git_root="$(git rev-parse --show-toplevel 2>/dev/null)" || true
    if [ -n "$git_root" ] && [ -d "$git_root/.claude/state" ]; then
        echo "$git_root/.claude/state"
    else
        echo "/tmp/unity-claude-hooks"
    fi
}
# Honor pre-set UNITY_HOOK_STATE_DIR (for tests and explicit overrides)
if [ -z "${UNITY_HOOK_STATE_DIR:-}" ]; then
    UNITY_HOOK_STATE_DIR="$(_resolve_state_dir)"
fi
mkdir -p "$UNITY_HOOK_STATE_DIR"

UNITY_READS_FILE="${UNITY_HOOK_STATE_DIR}/gateguard-reads.txt"
UNITY_EDITS_FILE="${UNITY_HOOK_STATE_DIR}/session-edits.txt"
UNITY_WARNINGS_FILE="${UNITY_HOOK_STATE_DIR}/session-warnings.txt"

# --- Shared utilities ---

# unity_hook_block — use instead of exit 2 in blocking hooks
# If UNITY_HOOK_MODE=warn, prints the message as a warning and exits 0
# Otherwise, prints the message and exits 2 (blocking)
unity_hook_block() {
    local message="$1"
    if [ "${UNITY_HOOK_MODE:-}" = "warn" ]; then
        echo "WARNING (downgraded from BLOCKED): $message" >&2
        exit 0
    else
        echo "BLOCKED: $message" >&2
        exit 2
    fi
}

# unity_track_edit — record a file edit for session tracking
unity_track_edit() {
    local file_path="$1"
    if [ -n "$file_path" ]; then
        echo "$file_path" >> "$UNITY_EDITS_FILE"
    fi
}

# unity_track_read — record a file read for gateguard tracking
unity_track_read() {
    local file_path="$1"
    if [ -n "$file_path" ]; then
        echo "$file_path" >> "$UNITY_READS_FILE"
    fi
}

# unity_was_read — check if a file was previously read
unity_was_read() {
    local file_path="$1"
    [ -f "$UNITY_READS_FILE" ] && grep -qxF "$file_path" "$UNITY_READS_FILE" 2>/dev/null
}

# unity_track_warning — record a hook warning for session analytics
unity_track_warning() {
    local hook_name="$1"
    local message="$2"
    if [ -n "$hook_name" ]; then
        echo "${hook_name}: ${message}" >> "$UNITY_WARNINGS_FILE"
    fi
}
