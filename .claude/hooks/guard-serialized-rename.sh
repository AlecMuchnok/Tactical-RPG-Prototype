#!/usr/bin/env bash
# H2 — PreToolUse (Edit). Denies renaming a serialized field without adding
# [FormerlySerializedAs("<oldName>")], since Unity silently resets every
# configured value and prefab override for the field otherwise.
# See .claude/rules/serialization.md.
#
# Deliberately conservative: only looks at the single Edit's old_string/new_string,
# so a rename split across two separate tool calls will trip this on the first call.
# Escape hatch: a `// serialization:ignore` comment anywhere in the edit.
#
# No PCRE available (grep -P unsupported in this locale) — every pattern below is
# POSIX ERE. Multi-line attribute+declaration pairs are handled by joining lines
# with a space before matching, then `[^;]*`/`[^{]*` character classes act as a
# non-greedy stop at the next terminator.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
FILE_PATH="$(hooklib::field '.tool_input.file_path')"

if [[ -z "$FILE_PATH" ]] || ! hooklib::is_project_cs "$FILE_PATH"; then
    exit 0
fi

OLD_STRING="$(hooklib::field '.tool_input.old_string')"
NEW_STRING="$(hooklib::field '.tool_input.new_string')"

if [[ -z "$OLD_STRING" ]]; then
    exit 0
fi

if grep -qF 'serialization:ignore' <<<"$OLD_STRING$NEW_STRING"; then
    exit 0
fi

# extract_field_names <text> — one serialized field/property name per line.
extract_field_names() {
    local text joined
    text="$1"
    joined="$(printf '%s' "$text" | tr '\n' ' ')"

    # [SerializeField] private T _name; / = ...;
    # `{ grep ... || true; }` keeps a zero-match grep from tripping `set -e -o
    # pipefail` on the calling `$(...)` — a legitimate "found nothing" result,
    # not a script error.
    { grep -oE '\[SerializeField\][^;]*;' <<<"$joined" 2>/dev/null || true; } | while IFS= read -r match; do
        match="${match%%=*}"
        match="${match%;}"
        awk '{print $NF}' <<<"$match"
    done

    # [field: SerializeField] public T Name { ... }
    { grep -oE '\[field:[[:space:]]*SerializeField\][^{]*\{' <<<"$joined" 2>/dev/null || true; } | while IFS= read -r match; do
        match="${match%\{}"
        awk '{print $NF}' <<<"$match"
    done

    # bare public field: public T name; / = ...; (line-by-line — excludes methods via no '(' and requires ';' terminator)
    { printf '%s\n' "$text" | { grep -E '^[[:space:]]*public[[:space:]]+[A-Za-z_][]A-Za-z0-9_<>[,. ]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*(=[^;]*)?;[[:space:]]*$' 2>/dev/null || true; } | { grep -vF '(' || true; } | { grep -vF '=>' || true; }; } | while IFS= read -r line; do
        line="${line%%=*}"
        line="${line%;}"
        awk '{print $NF}' <<<"$line"
    done
}

# extract_formerly_names <text> — names already covered by [FormerlySerializedAs("...")] in new_string.
extract_formerly_names() {
    { grep -oE 'FormerlySerializedAs\("[^"]*"\)' <<<"$1" 2>/dev/null || true; } | sed -E 's/FormerlySerializedAs\("([^"]*)"\)/\1/'
}

OLD_NAMES="$(extract_field_names "$OLD_STRING" | sort -u)"
NEW_NAMES="$(extract_field_names "$NEW_STRING" | sort -u)"
FORMERLY_NAMES="$(extract_formerly_names "$NEW_STRING" | sort -u)"

if [[ -z "$OLD_NAMES" ]]; then
    exit 0
fi

MISSING=""
while IFS= read -r name; do
    [[ -z "$name" ]] && continue
    if grep -qxF "$name" <<<"$NEW_NAMES"; then
        continue  # field still present under the same name
    fi
    if [[ -n "$FORMERLY_NAMES" ]] && grep -qxF "$name" <<<"$FORMERLY_NAMES"; then
        continue  # rename already covered
    fi
    MISSING="$MISSING$name"$'\n'
done <<<"$OLD_NAMES"

if [[ -n "$MISSING" ]]; then
    NAMES_LIST="$(printf '%s' "$MISSING" | sed '/^$/d')"
    hooklib::guard_deny "GUARD_SERIALIZED_RENAME" \
"DENIED — serialized field(s) removed/renamed without [FormerlySerializedAs].

  $FILE_PATH
  field(s): $(printf '%s' "$NAMES_LIST" | tr '\n' ' ')

If this is a rename, add above the new field:
  [FormerlySerializedAs(\"<oldName>\")]

Without it, every Inspector value and prefab override for this field silently
resets to default — see .claude/rules/serialization.md.

If the field was genuinely deleted (not renamed), add a comment containing
'serialization:ignore' anywhere in this edit to bypass this check."
fi

exit 0
