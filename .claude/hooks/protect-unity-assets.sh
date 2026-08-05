#!/usr/bin/env bash
# H1 — PreToolUse (Edit|Write|NotebookEdit). Denies hand-editing files Unity owns:
# YAML assets keyed by GUID/fileID, and Editor-managed/generated directories.
# See .claude/rules/unity-specifics.md and .claude/rules/serialization.md.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
FILE_PATH="$(hooklib::field '.tool_input.file_path')"

if [[ -z "$FILE_PATH" ]]; then
    exit 0
fi

if hooklib::is_unity_yaml_asset "$FILE_PATH"; then
    hooklib::guard_deny "PROTECT_UNITY_ASSETS" \
"DENIED — this is a Unity YAML asset (fileID/GUID cross-references). Hand-editing it
risks silent corruption that only shows up on load.

  $FILE_PATH

Use the mcp__UnityMCP__* tools for scene/GameObject/asset changes, or edit it in the
Unity Editor. See .claude/rules/unity-specifics.md."
fi

if hooklib::is_unity_managed_dir "$FILE_PATH"; then
    hooklib::guard_deny "PROTECT_UNITY_ASSETS" \
"DENIED — this path is Editor-managed or generated, not hand-edited source.

  $FILE_PATH

ProjectSettings/, Library/, Temp/, Logs/, obj/, Build(s)/, UserSettings/, and
packages-lock.json are owned by Unity/the package manager. Editing them by hand
will be overwritten or will conflict on the next Editor write."
fi

exit 0
