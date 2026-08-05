#!/usr/bin/env bash
# H6 — PostToolUse (Edit|Write), advisory only. Re-reads the whole written .cs file
# against .claude/rules/*.md and reports every violation found, grouped by source
# rule. Never blocks — exit 2 just feeds the message back to Claude as feedback.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=_lib.sh
source "$SCRIPT_DIR/_lib.sh"

hooklib::read_input
FILE_PATH="$(hooklib::field '.tool_input.file_path')"

if [[ -z "$FILE_PATH" ]] || ! hooklib::is_project_cs "$FILE_PATH" || [[ ! -f "$FILE_PATH" ]]; then
    exit 0
fi

WARNINGS=""
add_warning() {
    WARNINGS="$WARNINGS
- $1"
}

# --- performance.md: forbidden calls inside Update/FixedUpdate/LateUpdate --
HOTPATH_HITS="$(awk '
    /^\s*(private|protected|public|internal)?\s*(override\s+)?void\s+(Update|FixedUpdate|LateUpdate)\s*\(/ {
        inMethod = 1; depth = 0; methodName = $0; started = 0
    }
    inMethod {
        n = gsub(/\{/, "{"); depth += n
        m = gsub(/\}/, "}"); depth -= m
        if (n > 0) started = 1
        if (started && depth > 0) {
            if ($0 ~ /GetComponent[[:space:]]*[<(]/) print NR": GetComponent in a hot-path method"
            if ($0 ~ /FindObjectOfType/) print NR": FindObjectOfType in a hot-path method"
            if ($0 ~ /Camera\.main/) print NR": Camera.main (does FindObjectOfType internally) in a hot-path method"
            if ($0 ~ /new[[:space:]]+List[[:space:]]*</) print NR": `new List<>` allocation in a hot-path method"
            if ($0 ~ /Instantiate[[:space:]]*\(/) print NR": Instantiate() in a hot-path method (pool it instead)"
        }
        if (started && depth <= 0) { inMethod = 0 }
    }
' "$FILE_PATH" 2>/dev/null || true)"
if [[ -n "$HOTPATH_HITS" ]]; then
    while IFS= read -r hit; do
        [[ -z "$hit" ]] && continue
        add_warning "performance.md — line $hit"
    done <<<"$HOTPATH_HITS"
fi

if grep -nE '\.material([^a-zA-Z0-9_]|$)' "$FILE_PATH" | grep -vE '\.sharedMaterial' >/dev/null 2>&1; then
    add_warning "performance.md — '.material' clones the material and breaks batching; use '.sharedMaterial' or a MaterialPropertyBlock."
fi

if grep -qE 'Physics\.RaycastAll' "$FILE_PATH"; then
    add_warning "performance.md — Physics.RaycastAll allocates; use Physics.RaycastNonAlloc with a pre-allocated buffer."
fi

if grep -qE '(^|[^A-Za-z0-9_.])tag[[:space:]]*==' "$FILE_PATH"; then
    add_warning "csharp-unity.md — 'tag ==' used instead of CompareTag(\"tag\")."
fi

if ! hooklib::is_editor_folder_path "$FILE_PATH" && grep -qE 'using[[:space:]]+System\.Linq' "$FILE_PATH"; then
    add_warning "performance.md — 'using System.Linq' in gameplay code; no LINQ in gameplay code, use manual loops."
fi

if grep -qE 'Debug\.Log' "$FILE_PATH" && ! grep -qE '(UNITY_EDITOR|Conditional)' "$FILE_PATH"; then
    add_warning "performance.md — Debug.Log with no [Conditional(\"UNITY_EDITOR\")] guard visible in this file; strip debug output from production."
fi

# --- serialization.md / unity-specifics.md: ?. / is null on Unity objects --
NULLOP_LINES="$(grep -nE '[A-Za-z_][A-Za-z0-9_]*\?\.' "$FILE_PATH" || true)"
if [[ -n "$NULLOP_LINES" ]]; then
    add_warning "serialization.md/unity-specifics.md — '?.' used (line(s): $(printf '%s' "$NULLOP_LINES" | cut -d: -f1 | tr '\n' ' ')). If the target is a UnityEngine.Object (MonoBehaviour, Component, ...), this bypasses Unity's destroyed-object detection and can call into a destroyed object. Use 'if (x != null) { x.Foo(); }' instead."
fi
if grep -qE 'is[[:space:]]+null' "$FILE_PATH"; then
    add_warning "serialization.md — 'is null' used; on a UnityEngine.Object this is a C# reference check that misses destroyed-but-not-GC'd objects. Use '== null'."
fi

# --- unity-specifics.md: coroutines instead of Awaitable -------------------
if grep -qE '(StartCoroutine|IEnumerator|yield[[:space:]]+return)' "$FILE_PATH"; then
    add_warning "unity-specifics.md — coroutine API used; prefer 'async Awaitable' with destroyCancellationToken over StartCoroutine/IEnumerator."
fi

# --- unity-specifics.md: #if UNITY_STANDALONE with no #else ---------------
STANDALONE_NO_ELSE="$(awk '
    /#if[[:space:]]+UNITY_STANDALONE([^A-Za-z0-9_]|$)/ && depth == 0 { tracking = 1; startLine = NR; sawElse = 0; depth = 1; next }
    tracking && /^[[:space:]]*#if/ { depth++ }
    tracking && /^[[:space:]]*#else/ && depth == 1 { sawElse = 1 }
    tracking && /^[[:space:]]*#endif/ {
        depth--
        if (depth == 0) {
            if (!sawElse) print startLine
            tracking = 0
        }
    }
' "$FILE_PATH" 2>/dev/null || true)"
if [[ -n "$STANDALONE_NO_ELSE" ]]; then
    add_warning "unity-specifics.md — #if UNITY_STANDALONE block(s) starting at line(s) $(tr '\n' ' ' <<<"$STANDALONE_NO_ELSE") with no #else; this compiles to nothing on console. Cover every target or add a fallback."
fi

# --- csharp-unity.md: class not sealed --------------------------------------
UNSEALED="$(grep -nE '^[[:space:]]*public[[:space:]]+class[[:space:]]+[A-Za-z_]' "$FILE_PATH" | grep -vE 'sealed|abstract' || true)"
if [[ -n "$UNSEALED" ]]; then
    add_warning "csharp-unity.md — class declared without 'sealed' (line(s): $(printf '%s' "$UNSEALED" | cut -d: -f1 | tr '\n' ' '))."
fi

# --- csharp-unity.md: public field (not const/static/readonly/property) ----
PUBLIC_FIELDS="$(grep -nE '^[[:space:]]*public[[:space:]]+[A-Za-z_][]A-Za-z0-9_<>[,. ]*[[:space:]]+[a-zA-Z_][A-Za-z0-9_]*[[:space:]]*(=[^;]*)?;[[:space:]]*$' "$FILE_PATH" \
    | grep -vE 'const|static|readonly|class|struct|interface|enum' \
    | grep -vF '=>' || true)"
if [[ -n "$PUBLIC_FIELDS" ]]; then
    add_warning "csharp-unity.md — public field(s) (line(s): $(printf '%s' "$PUBLIC_FIELDS" | cut -d: -f1 | tr '\n' ' ')); expose via [SerializeField] private, or a property with a named caller."
fi

# --- csharp-unity.md: private field missing _ prefix ------------------------
BAD_PRIVATE="$(grep -nE '^[[:space:]]*(\[SerializeField\][[:space:]]*)?private[[:space:]]+[A-Za-z_][]A-Za-z0-9_<>[,. ]*[[:space:]]+[a-zA-Z][A-Za-z0-9_]*[[:space:]]*(=[^;]*)?;' "$FILE_PATH" \
    | grep -vF '=>' \
    | grep -vE '[[:space:]]_[A-Za-z0-9_]*[[:space:]]*(=[^;]*)?;' || true)"
if [[ -n "$BAD_PRIVATE" ]]; then
    add_warning "csharp-unity.md — private field(s) missing the '_lowerCamelCase' prefix (line(s): $(printf '%s' "$BAD_PRIVATE" | cut -d: -f1 | tr '\n' ' '))."
fi

# --- csharp-unity.md: braceless if/for/while --------------------------------
BRACELESS="$(grep -nE '^[[:space:]]*(if|for|while)[[:space:]]*\(.*\)[[:space:]]*$' "$FILE_PATH" || true)"
if [[ -n "$BRACELESS" ]]; then
    add_warning "csharp-unity.md — if/for/while with no braces on its own line (line(s): $(printf '%s' "$BRACELESS" | cut -d: -f1 | tr '\n' ' ')); braces always, even for single statements."
fi

# --- csharp-unity.md: single-letter loop variable ---------------------------
if grep -qE 'for[[:space:]]*\([[:space:]]*(int|var)[[:space:]]+[a-zA-Z][[:space:]]*=' "$FILE_PATH"; then
    add_warning "csharp-unity.md — single-letter loop variable; use a descriptive name (e.g. 'enemyIndex', not 'i')."
fi

# --- architecture.md: Views referencing gameplay Manager/System types ------
if grep -qE '[/\\]UI[/\\]Views[/\\]' <<<"$FILE_PATH"; then
    if grep -qE '\[SerializeField\][[:space:]]*private[[:space:]]+[A-Za-z_]*(Manager|System)\b' "$FILE_PATH"; then
        add_warning "architecture.md — a View holds a direct reference to a *Manager/*System type; Views must not touch gameplay types. Route this through a Presenter and an event channel."
    fi
fi

# --- architecture.md: new script landing in Assets/Scripts/ root ----------
NORMALIZED_PATH="$(hooklib::normalize_path "$FILE_PATH")"
case "$NORMALIZED_PATH" in
    */Assets/Scripts/*.cs)
        REL="${NORMALIZED_PATH##*/Assets/Scripts/}"
        if [[ "$REL" != */* ]]; then
            add_warning "architecture.md — this file sits directly in Assets/Scripts/ instead of a canonical subfolder (Components/, Systems/, Services/, EventChannels/, StateMachines/, Commands/, Data/, UI/Views/, UI/Presenters/, Input/, Utility/). See the Folder Structure section."
        fi
        ;;
esac

if [[ -n "$WARNINGS" ]]; then
    hooklib::advise "LINT_CSHARP_RULES" \
"Rule check for $FILE_PATH (advisory — edit was applied, review before continuing):$WARNINGS"
fi

exit 0
