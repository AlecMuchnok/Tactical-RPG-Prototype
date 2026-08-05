#!/usr/bin/env bash
# H9 — SessionEnd. Removes this session's scratch state so .claude/state/ doesn't
# accumulate across sessions.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

if hooklib::is_disabled "SESSION_CLEANUP"; then
    exit 0
fi

STATE_DIR="$(hooklib::state_dir)"
rm -f "$STATE_DIR/session-start-time" "$STATE_DIR/editor-log-offset" 2>/dev/null || true

exit 0
