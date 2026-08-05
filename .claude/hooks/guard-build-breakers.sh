#!/usr/bin/env bash
# H3 — PreToolUse (Edit|Write). Denies four things that are objectively wrong,
# not a matter of taste — each compiles fine in the Editor and breaks later:
#   1. UnityEditor usage outside Editor/ with no #if UNITY_EDITOR guard
#   2. Legacy Input.* APIs (New Input System is mandatory) — escape: // input:ignore
#   3. async void
#   4. UNITY_ANDROID / UNITY_IOS defines (desktop + console only, never mobile)
# See .claude/rules/unity-specifics.md and CLAUDE.md.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
TOOL_NAME="$(hooklib::field '.tool_name')"
FILE_PATH="$(hooklib::field '.tool_input.file_path')"

if [[ -z "$FILE_PATH" ]] || ! hooklib::is_project_cs "$FILE_PATH"; then
    exit 0
fi

if [[ "$TOOL_NAME" == "Write" ]]; then
    TEXT="$(hooklib::field '.tool_input.content')"
else
    TEXT="$(hooklib::field '.tool_input.new_string')"
fi

if [[ -z "$TEXT" ]]; then
    exit 0
fi

REASONS=""

# --- 1. UnityEditor outside Editor/ without a guard --------------------
if ! hooklib::is_editor_folder_path "$FILE_PATH"; then
    if grep -qE '(^|[^A-Za-z0-9_])UnityEditor([^A-Za-z0-9_]|$)' <<<"$TEXT" \
        && ! grep -qE 'UNITY_EDITOR' <<<"$TEXT"; then
        REASONS="$REASONS
- UnityEditor is referenced with no #if UNITY_EDITOR guard, and this file is not
  under an Editor/ folder. Compiles fine in the Editor, fails at build time with
  no prior warning. Wrap the UnityEditor usage in #if UNITY_EDITOR / #endif."
    fi
fi

# --- 2. Legacy Input API ------------------------------------------------
LEGACY_LINES="$(grep -nE '(^|[^A-Za-z0-9_.])Input\.(GetKey|GetAxis|GetButton|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp)' <<<"$TEXT" | grep -v 'input:ignore' || true)"
if [[ -n "$LEGACY_LINES" ]]; then
    REASONS="$REASONS
- Legacy Input.* API used. The New Input System is mandatory (CLAUDE.md,
  unity-specifics.md). Use Mouse.current / Keyboard.current / Gamepad.current, or
  a generated PlayerControls action map. Add '// input:ignore' on the line if this
  is deliberate.
  offending line(s):
$(printf '%s' "$LEGACY_LINES" | sed 's/^/    /')"
fi

# --- 3. async void --------------------------------------------------------
if grep -qE '(^|[^A-Za-z0-9_])async[[:space:]]+void([^A-Za-z0-9_]|$)' <<<"$TEXT"; then
    REASONS="$REASONS
- 'async void' is never acceptable (unity-specifics.md) — exceptions vanish and
  there's no cancellation. Use 'async Awaitable' with destroyCancellationToken, or
  '_ = SomethingAsync(destroyCancellationToken);' from a non-async caller wrapping
  the body in try/catch (OperationCanceledException)."
fi

# --- 4. Mobile platform defines -----------------------------------------
if grep -qE '(^|[^A-Za-z0-9_])UNITY_(ANDROID|IOS)([^A-Za-z0-9_]|$)' <<<"$TEXT"; then
    REASONS="$REASONS
- UNITY_ANDROID / UNITY_IOS referenced. This game ships desktop and console only
  (unity-specifics.md Platform Defines table) — a mobile define here is dead code
  that misleads the next reader."
fi

if [[ -n "$REASONS" ]]; then
    hooklib::guard_deny "GUARD_BUILD_BREAKERS" \
"DENIED — build-breaking pattern(s) in $FILE_PATH:
$REASONS"
fi

exit 0
