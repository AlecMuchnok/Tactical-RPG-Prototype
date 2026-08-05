#!/usr/bin/env bash
# H8 — Stop. Blocks the turn from ending while Unity has C# compile errors, so
# Claude doesn't report success on code that doesn't build. Primary path is a
# live unity-mcp read_console call (streamable-HTTP transport — confirmed live:
# POST /mcp initialize -> Mcp-Session-Id header -> notifications/initialized ->
# tools/call read_console, response is SSE-framed and double-JSON-encoded).
# Falls back to scanning Editor.log for "error CS" lines written since session
# start if the MCP bridge is unreachable, rather than silently passing.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input

# Never re-trigger ourselves once we've already blocked once this turn.
if [[ "$(hooklib::field '.stop_hook_active')" == "true" ]]; then
    exit 0
fi

if hooklib::is_disabled "CHECK_UNITY_CONSOLE"; then
    exit 0
fi

MCP_URL="http://localhost:8080/mcp"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

block_on_errors() {
    local errors="$1"
    local source="$2"
    jq -n --arg reason "DENIED — Unity reports C# compile error(s) ($source). Fix these before finishing:

$errors" '{decision: "block", reason: $reason}' \
        || true
    exit 0
}

try_mcp() {
    local headers_file="$TMP_DIR/headers.txt"
    local body_file="$TMP_DIR/init_body.txt"

    curl -sS -m 8 -D "$headers_file" -o "$body_file" -X POST "$MCP_URL" \
        -H "Content-Type: application/json" \
        -H "Accept: application/json, text/event-stream" \
        -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"check-unity-console-hook","version":"1"}}}' \
        || return 1

    local session_id
    session_id="$(grep -i '^mcp-session-id:' "$headers_file" 2>/dev/null | head -1 | sed -E 's/^[Mm]cp-[Ss]ession-[Ii]d:[[:space:]]*//' | tr -d '\r')"
    if [[ -z "$session_id" ]]; then
        return 1
    fi

    curl -sS -m 8 -X POST "$MCP_URL" \
        -H "Content-Type: application/json" \
        -H "Accept: application/json, text/event-stream" \
        -H "Mcp-Session-Id: $session_id" \
        -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
        -o /dev/null || true

    local console_file="$TMP_DIR/console.txt"
    curl -sS -m 15 -X POST "$MCP_URL" \
        -H "Content-Type: application/json" \
        -H "Accept: application/json, text/event-stream" \
        -H "Mcp-Session-Id: $session_id" \
        -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"read_console","arguments":{"types":["error"],"count":100}}}' \
        -o "$console_file" || return 1

    local inner_json
    inner_json="$(grep -m1 '^data: ' "$console_file" 2>/dev/null | sed 's/^data: //' | jq -r '.result.content[0].text // empty' 2>/dev/null || true)"
    if [[ -z "$inner_json" ]]; then
        return 1
    fi

    local compile_errors
    compile_errors="$(jq -r '.data[]? // empty' <<<"$inner_json" 2>/dev/null | grep -E 'error CS[0-9]+' || true)"

    if [[ -n "$compile_errors" ]]; then
        block_on_errors "$compile_errors" "via unity-mcp read_console"
    fi

    return 0  # reached Unity successfully, no compile errors — done, no fallback needed
}

try_editor_log_fallback() {
    local log_path
    log_path="$(hooklib::editor_log_path)"
    if [[ -z "$log_path" || ! -f "$log_path" ]]; then
        exit 0  # can't verify either way — fail open rather than block forever
    fi

    local offset=0
    local offset_file
    offset_file="$(hooklib::state_dir)/editor-log-offset"
    if [[ -f "$offset_file" ]]; then
        offset="$(cat "$offset_file" 2>/dev/null || echo 0)"
        [[ "$offset" =~ ^[0-9]+$ ]] || offset=0
    fi

    local compile_errors
    compile_errors="$(tail -c "+$((offset + 1))" "$log_path" 2>/dev/null | grep -E 'error CS[0-9]+' || true)"

    if [[ -n "$compile_errors" ]]; then
        block_on_errors "$compile_errors" "via Editor.log fallback — unity-mcp bridge was unreachable"
    fi
}

if ! try_mcp; then
    try_editor_log_fallback
fi

exit 0
