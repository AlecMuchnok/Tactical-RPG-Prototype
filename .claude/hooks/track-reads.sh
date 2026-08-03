#!/usr/bin/env bash
# ============================================================================
# track-reads.sh — TRACKING HOOK (standard profile)
# Records files that have been Read, so GateGuard can verify investigation
# before allowing edits.
# ============================================================================
# Trigger: PostToolUse on Read
# Exit: 0 always (tracking only)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"  # was "strict" — must stay in sync with
                                # gateguard.sh, which reads what this writes
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

if [ -n "$FILE_PATH" ]; then
    unity_track_read "$FILE_PATH"
fi

exit 0
