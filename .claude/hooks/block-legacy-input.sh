#!/usr/bin/env bash
# ============================================================================
# block-legacy-input.sh — BLOCKING HOOK (standard profile)
# The legacy Input Manager (Input.GetKey / GetAxis / GetButton / GetMouse*)
# does not see gamepads reliably and cannot be rebound. This project ships to
# desktop AND console, so every input path must go through the New Input
# System (Mouse.current / Keyboard.current / Gamepad.current, or a generated
# PlayerControls class from an .inputactions asset — see architecture.md).
#
# Escape hatches:
#   // input:ignore                        (per line)
#   DISABLE_HOOK_BLOCK_LEGACY_INPUT=1      (whole hook)
#   UNITY_HOOK_MODE=warn                   (downgrade to warning)
# ============================================================================
# Trigger: PreToolUse on Edit|Write
# Exit:    2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only check C# files
case "$FILE_PATH" in
    *.cs) ;;
    *) exit 0 ;;
esac

# Skip Editor and test code — different rules apply there
case "$FILE_PATH" in
    */Editor/*|*/editor/*|*Test*|*test*) exit 0 ;;
esac

CONTENT=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

if [ -z "$CONTENT" ]; then
    exit 0
fi

# Legacy Input Manager members. The leading (^|[^A-Za-z0-9_]) boundary means
# this only matches "Input." itself (bare or qualified as UnityEngine.Input.),
# never InputAction./InputSystem./PlayerInput./_controls.Player.Move etc.
LEGACY_MEMBERS='GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw|GetButton|GetButtonDown|GetButtonUp|GetMouseButton|GetMouseButtonDown|GetMouseButtonUp|mousePosition|mouseScrollDelta|touchCount|touches|GetTouch|anyKey|anyKeyDown|inputString'
PATTERN="(^|[^A-Za-z0-9_])Input\.(${LEGACY_MEMBERS})\b"

# Strip full-line comments so the rules' own documentation (which mentions
# these API names) doesn't trip the hook on itself.
CODE_ONLY=$(echo "$CONTENT" | grep -vE '^[[:space:]]*(//|\*)')

MATCH=$(echo "$CODE_ONLY" | grep -nE "$PATTERN" | grep -v 'input:ignore' | head -1 || true)

if [ -n "$MATCH" ]; then
    echo "" >&2
    echo "  BLOCKED: Legacy Input Manager API detected." >&2
    echo "  File: $FILE_PATH" >&2
    echo "  Line: $MATCH" >&2
    echo "" >&2
    echo "  Input.GetKey / GetAxis / GetButton / GetMouseButton* don't see" >&2
    echo "  gamepads reliably and can't be rebound — both required for console" >&2
    echo "  certification. Use the New Input System instead:" >&2
    echo "" >&2
    echo "    - Rebindable gameplay actions: a generated PlayerControls class" >&2
    echo "      from an .inputactions asset (see architecture.md's Input" >&2
    echo "      Architecture section)." >&2
    echo "    - Raw pointer position: Mouse.current / Keyboard.current /" >&2
    echo "      Gamepad.current — still the New Input System, not legacy." >&2
    echo "" >&2
    echo "  If this line is unavoidable (rare), add // input:ignore to it." >&2
    unity_hook_block "block-legacy-input: replace the legacy Input.* call above."
fi

exit 0
