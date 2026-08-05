#!/usr/bin/env bash
# Shared helpers for .claude/hooks/*.sh — sourced, never executed directly.
# Kill switches (checked by hooklib::guard_deny / hooklib::advise):
#   DISABLE_UNITY_HOOKS=1        disables every hook
#   UNITY_HOOK_MODE=warn         downgrades every blocking hook to advisory
#   DISABLE_HOOK_<NAME>=1        disables one hook, e.g. DISABLE_HOOK_PROTECT_UNITY_ASSETS

set -euo pipefail

hooklib::project_dir() {
    # CLAUDE_PROJECT_DIR is set by the harness; fall back for standalone testing.
    if [[ -n "${CLAUDE_PROJECT_DIR:-}" ]]; then
        printf '%s' "$CLAUDE_PROJECT_DIR"
    elif git rev-parse --show-toplevel >/dev/null 2>&1; then
        git rev-parse --show-toplevel
    else
        pwd
    fi
}

hooklib::state_dir() {
    local dir
    dir="$(hooklib::project_dir)/.claude/state"
    mkdir -p "$dir"
    printf '%s' "$dir"
}

# Reads stdin once into $INPUT_JSON. Call at the top of every hook's main flow.
hooklib::read_input() {
    INPUT_JSON="$(cat)"
}

# hooklib::field '<jq filter>' — extract a field from $INPUT_JSON, empty string if absent/null.
hooklib::field() {
    jq -r "$1 // empty" <<<"$INPUT_JSON" 2>/dev/null || true
}

# hooklib::is_disabled <HOOK_NAME> — true if DISABLE_UNITY_HOOKS or DISABLE_HOOK_<NAME> is set.
hooklib::is_disabled() {
    local hook_name="$1"
    local disable_var="DISABLE_HOOK_${hook_name}"
    [[ "${DISABLE_UNITY_HOOKS:-0}" == "1" ]] && return 0
    [[ "${!disable_var:-0}" == "1" ]] && return 0
    return 1
}

hooklib::is_warn_mode() {
    [[ "${UNITY_HOOK_MODE:-}" == "warn" ]]
}

# hooklib::emit_deny <reason> — PreToolUse deny JSON, then exit 0 (JSON, not exit code, carries the decision).
hooklib::emit_deny() {
    local reason="$1"
    jq -n --arg reason "$reason" \
        '{hookSpecificOutput: {hookEventName: "PreToolUse", permissionDecision: "deny", permissionDecisionReason: $reason}}' \
        || true
    exit 0
}

# hooklib::emit_allow_with_warning <reason> — allow, but surface the reason as a system message.
hooklib::emit_allow_with_warning() {
    local reason="$1"
    jq -n --arg reason "$reason" \
        '{systemMessage: $reason, hookSpecificOutput: {hookEventName: "PreToolUse", permissionDecision: "allow", permissionDecisionReason: $reason}}' \
        || true
    exit 0
}

# hooklib::guard_deny <HOOK_NAME> <reason> — the entry point every PreToolUse blocking hook calls
# once it has decided to deny. Honors kill switches and the warn-mode downgrade.
hooklib::guard_deny() {
    local hook_name="$1"
    local reason="$2"
    if hooklib::is_disabled "$hook_name"; then
        exit 0
    fi
    if hooklib::is_warn_mode; then
        hooklib::emit_allow_with_warning "$reason"
    fi
    hooklib::emit_deny "$reason"
}

# hooklib::advise <HOOK_NAME> <message> — the entry point PostToolUse advisory hooks call.
# Exit 2 feeds stderr back to Claude without blocking anything (PostToolUse can't block).
hooklib::advise() {
    local hook_name="$1"
    local message="$2"
    if hooklib::is_disabled "$hook_name"; then
        exit 0
    fi
    printf '%s\n' "$message" >&2
    exit 2
}

# --- path classification -----------------------------------------------
# Normalize backslashes to forward slashes so checks work regardless of
# whether the harness reports Windows-native or POSIX-style paths.
hooklib::normalize_path() {
    printf '%s' "$1" | tr '\\' '/'
}

hooklib::is_unity_yaml_asset() {
    local path
    path="$(hooklib::normalize_path "$1")"
    case "$path" in
        *.meta|*.unity|*.prefab|*.asset|*.controller|*.inputactions|*.spriteatlas)
            return 0 ;;
        *)
            return 1 ;;
    esac
}

hooklib::is_unity_managed_dir() {
    local path
    path="$(hooklib::normalize_path "$1")"
    case "$path" in
        */ProjectSettings/*|ProjectSettings/*|*/Library/*|Library/*|*/Temp/*|Temp/*|*/Logs/*|Logs/*|*/obj/*|obj/*|*/Build/*|Build/*|*/Builds/*|Builds/*|*/UserSettings/*|UserSettings/*)
            return 0 ;;
        *packages-lock.json)
            return 0 ;;
        *)
            return 1 ;;
    esac
}

hooklib::is_editor_folder_path() {
    local path
    path="$(hooklib::normalize_path "$1")"
    case "$path" in
        */Editor/*|Editor/*)
            return 0 ;;
        *)
            return 1 ;;
    esac
}

# hooklib::win_to_posix_path <windows-path> — "C:\Users\x\y" -> "/c/Users/x/y".
# Uses cygpath when available (belt-and-suspenders); falls back to manual sed.
hooklib::win_to_posix_path() {
    local win_path="$1"
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -u "$win_path" 2>/dev/null && return 0
    fi
    local posix_path drive
    posix_path="$(printf '%s' "$win_path" | sed -E 's#\\#/#g')"
    drive="$(printf '%s' "$posix_path" | cut -c1 | tr 'A-Z' 'a-z')"
    printf '/%s%s' "$drive" "${posix_path:2}"
}

# hooklib::editor_log_path — POSIX path to Unity's Editor.log, or empty if
# %LOCALAPPDATA% isn't set.
hooklib::editor_log_path() {
    if [[ -z "${LOCALAPPDATA:-}" ]]; then
        return 0
    fi
    hooklib::win_to_posix_path "${LOCALAPPDATA}\\Unity\\Editor\\Editor.log"
}

hooklib::is_project_cs() {
    local path
    path="$(hooklib::normalize_path "$1")"
    case "$path" in
        */Assets/*.cs|Assets/*.cs)
            return 0 ;;
        *)
            return 1 ;;
    esac
}
