#!/usr/bin/env bash
# ============================================================================
# gateguard.sh — BLOCKING HOOK (standard profile)
# Three-stage fact-forcing gate for C# edits: DENY -> FORCE -> ALLOW
#
#   Stage 1 (DENY):  Block first Edit/Write on a C# file. Force investigation.
#   Stage 2 (FORCE): Emit Unity-specific fact demands (callers, GUID refs,
#                    FormerlySerializedAs plan, instruction quote, folder role).
#   Stage 3 (ALLOW): Second attempt on same file proceeds (presumes the agent
#                    read the deny message and gathered facts).
#
# Also enforces Read-before-Edit and the View <-> Presenter counterpart heuristic.
# ============================================================================
# Trigger: PreToolUse on Edit|Write|MultiEdit
# Exit:    2 = block, 0 = allow
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"  # was "strict" — the active profile defaults to
                                # "standard", which silently disabled this hook
                                # entirely. track-reads.sh must stay in sync.
source "${SCRIPT_DIR}/_lib.sh"

INPUT=$(cat)

TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name // empty')
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

# Only gate C# files
case "$FILE_PATH" in
    *.cs) ;;
    *) exit 0 ;;
esac

BASENAME=$(basename "$FILE_PATH" .cs)
DIR=$(dirname "$FILE_PATH")

# --- State tracking: per-file fact-gate progress ---
FACTS_DENIED_FILE="${UNITY_HOOK_STATE_DIR}/gateguard-facts-denied.txt"
FACTS_PASSED_FILE="${UNITY_HOOK_STATE_DIR}/gateguard-facts-passed.txt"
touch "$FACTS_DENIED_FILE" "$FACTS_PASSED_FILE"

# Detect Write (new file) vs Edit (existing) vs MultiEdit
IS_WRITE="false"
if [ "$TOOL_NAME" = "Write" ]; then
    # Write creates new file OR overwrites; treat as new-file gate if file doesn't exist yet
    if [ ! -f "$FILE_PATH" ]; then
        IS_WRITE="true"
    fi
fi

# --- Guard 1: Read-before-Edit (skip for brand-new Write of non-existent file) ---
if [ "$IS_WRITE" = "false" ]; then
    if ! unity_was_read "$FILE_PATH"; then
        unity_track_warning "gateguard" "unread: $FILE_PATH"
        echo "" >&2
        echo "  GateGuard — STAGE 1: You must Read this file before editing." >&2
        echo "  File: $FILE_PATH" >&2
        echo "" >&2
        echo "  The file may contain state, invariants, or attributes you will" >&2
        echo "  destroy with a blind edit." >&2
        unity_hook_block "GateGuard: Read $FILE_PATH before editing."
    fi
fi

# --- Guard 2: Fact-gate (first edit per file emits fact demands) ---
if ! grep -qxF "$FILE_PATH" "$FACTS_PASSED_FILE" 2>/dev/null; then
    # Has this file been denied once already?
    if grep -qxF "$FILE_PATH" "$FACTS_DENIED_FILE" 2>/dev/null; then
        # Second attempt — mark as passed and allow through
        echo "$FILE_PATH" >> "$FACTS_PASSED_FILE"
    else
        # First attempt — DENY and demand facts
        echo "$FILE_PATH" >> "$FACTS_DENIED_FILE"
        unity_track_warning "gateguard" "fact-demand: $FILE_PATH"

        # Classify file by folder — this architecture doesn't use one fixed
        # filename-suffix convention (see architecture.md's Folder Structure).
        ROLE=""
        case "$DIR" in
            */Components*)              ROLE="Component (sibling-composed on a unit)" ;;
            */Systems*)                 ROLE="System (service-locator registered)" ;;
            */Services*)                ROLE="Service infrastructure (e.g. ServiceLocator itself)" ;;
            */EventChannels*)           ROLE="SO Event Channel" ;;
            */StateMachines*)           ROLE="State machine / state" ;;
            */Commands*)                ROLE="Command (ICommand)" ;;
            */Data*)                    ROLE="ScriptableObject data definition" ;;
            */UI/Views*)                ROLE="UI View (no gameplay references allowed)" ;;
            */UI/Presenters*)           ROLE="UI Presenter (mediates View <-> gameplay)" ;;
            */Input*)                   ROLE="Input adapter" ;;
            */Utility*)                 ROLE="Pure utility (no Unity lifecycle)" ;;
        esac

        echo "" >&2
        echo "  GateGuard — STAGE 2 (FACT DEMAND)" >&2
        if [ "$IS_WRITE" = "true" ]; then
            echo "  New file: $FILE_PATH" >&2
            [ -n "$ROLE" ] && echo "  Inferred role: $ROLE" >&2
            echo "" >&2
            echo "  Before creating this file, present these facts:" >&2
            echo "" >&2
            echo "  1. Name the file(s) and line(s) that will reference this new type." >&2
            echo "  2. Confirm no existing type serves the same purpose." >&2
            echo "     Run: grep -rn 'class ${BASENAME}' Assets/" >&2
            echo "  3. Confirm which folder this lives in (Components / Systems /" >&2
            echo "     EventChannels / StateMachines / Commands / Data / UI/Views /" >&2
            echo "     UI/Presenters / Input / Utility) — the architecture validator" >&2
            echo "     classifies files by folder, not filename suffix." >&2
            echo "  4. If it's a System, confirm it registers itself with" >&2
            echo "     ServiceLocator.Register<T>() in Awake and unregisters in" >&2
            echo "     OnDestroy. If it's a Component, confirm sibling access goes" >&2
            echo "     through GetComponent (not the locator) and name the" >&2
            echo "     GameObject/prefab it will live on. If it's a UI View, confirm" >&2
            echo "     it holds no gameplay type — only a Presenter may." >&2
            echo "  5. Quote the user's current instruction verbatim." >&2
        else
            echo "  File: $FILE_PATH" >&2
            [ -n "$ROLE" ] && echo "  Inferred role: $ROLE" >&2
            echo "" >&2
            echo "  Before editing, present these facts:" >&2
            echo "" >&2
            echo "  1. List files that reference this type (callers, consumers)." >&2
            echo "     Run: grep -rn '${BASENAME}' Assets/ --include='*.cs'" >&2
            echo "  2. List scene/prefab references via GUID from the .meta file." >&2
            echo "     Run: GUID=\$(grep 'guid:' ${FILE_PATH}.meta | awk '{print \$2}')" >&2
            echo "          grep -rln \"\$GUID\" Assets/ --include='*.unity' --include='*.prefab'" >&2
            echo "  3. If renaming ANY [SerializeField] field, state the" >&2
            echo "     [FormerlySerializedAs(\"oldName\")] plan. Without it, every" >&2
            echo "     configured instance silently resets to default." >&2
            echo "  4. If changing public API, list the callers that will need updates." >&2
            echo "  5. Quote the user's current instruction verbatim." >&2
        fi
        echo "" >&2
        echo "  After presenting these facts, retry the same edit — it will pass." >&2
        echo "" >&2
        unity_hook_block "GateGuard: present facts above, then retry the edit."
    fi
fi

# --- Guard 3: View <-> Presenter counterpart heuristic (advisory, does not block) ---
# The one fixed 1:1 pairing left in this architecture (see architecture.md §5,
# MVP for UI). Components/Systems/EventChannels don't have an equivalent fixed
# counterpart, so there's nothing to check for them.
case "$DIR" in
    */UI/Views*)
        counterpart_name="${BASENAME%View}Presenter"
        candidate=$(find "$(dirname "$DIR")" -maxdepth 3 -name "${counterpart_name}.cs" 2>/dev/null | head -1)
        if [ -n "$candidate" ] && [ -f "$candidate" ] && ! unity_was_read "$candidate"; then
            echo "  SUGGESTION: Consider reading the Presenter first: ${candidate}" >&2
        fi
        ;;
    */UI/Presenters*)
        counterpart_name="${BASENAME%Presenter}View"
        candidate=$(find "$(dirname "$DIR")" -maxdepth 3 -name "${counterpart_name}.cs" 2>/dev/null | head -1)
        if [ -n "$candidate" ] && [ -f "$candidate" ] && ! unity_was_read "$candidate"; then
            echo "  SUGGESTION: Consider reading the View first: ${candidate}" >&2
        fi
        ;;
esac

exit 0
