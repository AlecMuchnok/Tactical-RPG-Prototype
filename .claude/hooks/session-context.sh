#!/usr/bin/env bash
# H7 — SessionStart. Clears stale per-session state, then prints a compact status
# block so Claude doesn't burn turns rediscovering branch/Unity/MCP state.
# Plain stdout — SessionStart hook output is added to context automatically.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

if hooklib::is_disabled "SESSION_CONTEXT"; then
    exit 0
fi

PROJECT_DIR="$(hooklib::project_dir)"
STATE_DIR="$(hooklib::state_dir)"

date +%s > "$STATE_DIR/session-start-time" 2>/dev/null || true

# Recorded so the Stop hook's Editor.log fallback (H8) only scans lines written
# during this session, not stale errors from a previous run.
EDITOR_LOG="$(hooklib::editor_log_path)"
if [[ -n "$EDITOR_LOG" && -f "$EDITOR_LOG" ]]; then
    wc -c < "$EDITOR_LOG" | tr -d '[:space:]' > "$STATE_DIR/editor-log-offset" 2>/dev/null || true
else
    printf '0' > "$STATE_DIR/editor-log-offset"
fi

BRANCH="$(git -C "$PROJECT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
DIRTY="clean"
if [[ -n "$(git -C "$PROJECT_DIR" status --porcelain 2>/dev/null || true)" ]]; then
    DIRTY="dirty (uncommitted changes present)"
fi

MAIN_WARNING=""
if [[ "$BRANCH" == "main" || "$BRANCH" == "master" ]]; then
    MAIN_WARNING=" — on a protected branch; create a feature branch before implementing (git-workflow.md)"
fi

UNITY_RUNNING="not detected"
if command -v tasklist >/dev/null 2>&1; then
    if tasklist //FI "IMAGENAME eq Unity.exe" 2>/dev/null | grep -qi 'Unity.exe'; then
        UNITY_RUNNING="running"
    fi
fi

MCP_STATUS="unreachable"
if command -v curl >/dev/null 2>&1; then
    if curl -s -m 2 -o /dev/null "http://localhost:8080/mcp" 2>/dev/null; then
        MCP_STATUS="responding"
    fi
fi

MISPLACED_COUNT=0
if [[ -d "$PROJECT_DIR/Assets/Scripts" ]]; then
    MISPLACED_COUNT="$(find "$PROJECT_DIR/Assets/Scripts" -maxdepth 1 -name '*.cs' 2>/dev/null | wc -l | tr -d '[:space:]')"
fi

cat <<EOF
[session-context] branch: $BRANCH ($DIRTY)$MAIN_WARNING
[session-context] Unity Editor: $UNITY_RUNNING | unity-mcp (localhost:8080/mcp): $MCP_STATUS
[session-context] Assets/Scripts/ root has $MISPLACED_COUNT script(s) outside the canonical subfolders (architecture.md Folder Structure)
EOF

exit 0
