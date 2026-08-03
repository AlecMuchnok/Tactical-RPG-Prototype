#!/usr/bin/env bash
set -euo pipefail

# =============================================================================
# validate-architecture.sh
# Checks compliance with the architecture stack in .claude/rules/architecture.md
# (component composition, SO event channels, state machines, command pattern,
# MVP for UI, service locator) via grep-based static analysis.
#
# Usage:
#   ./scripts/validate-architecture.sh [--path <dir>]
#   Defaults to Assets/ under the nearest Unity project root.
# =============================================================================

# ---------------------------------------------------------------------------
# Color support
# ---------------------------------------------------------------------------
if [[ -t 1 ]] && command -v tput &>/dev/null && [[ $(tput colors 2>/dev/null || echo 0) -ge 8 ]]; then
    RED=$(tput setaf 1); GREEN=$(tput setaf 2); YELLOW=$(tput setaf 3)
    CYAN=$(tput setaf 6); BOLD=$(tput bold); RESET=$(tput sgr0)
else
    RED=""; GREEN=""; YELLOW=""; CYAN=""; BOLD=""; RESET=""
fi

# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------
if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    cat <<EOF
${BOLD}validate-architecture.sh${RESET} - architecture stack compliance checker.

${BOLD}Usage:${RESET}
  ./scripts/validate-architecture.sh [OPTIONS]

${BOLD}Options:${RESET}
  --path <dir>   Directory to scan (default: Assets/ under Unity project root)
  -h, --help     Show this help

${BOLD}What it checks:${RESET}
  1. UI Views (Scripts/UI/Views/) don't reference gameplay types — MVP boundary
  2. No singleton patterns (static Instance, FindObjectOfType) outside ServiceLocator
  3. Coroutine usage (prefer async Awaitable)
  4. ServiceLocator.Get<T>() called from Awake — the documented ordering gotcha
  5. Systems (Scripts/Systems/) register/unregister with ServiceLocator

${BOLD}Note:${RESET}
  This is heuristic-based (grep). It may produce false positives.
  Add "// architecture:ignore" on any line to suppress a warning for that line.
EOF
    exit 0
fi

# ---------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------
SCAN_PATH=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --path) SCAN_PATH="$2"; shift 2 ;;
        *) echo "${RED}Unknown option: $1${RESET}" >&2; exit 1 ;;
    esac
done

# ---------------------------------------------------------------------------
# Find Unity project root
# ---------------------------------------------------------------------------
find_unity_root() {
    local dir="$PWD"
    while [[ "$dir" != "/" ]]; do
        if [[ -d "$dir/Assets" && -d "$dir/ProjectSettings" ]]; then
            echo "$dir"
            return 0
        fi
        dir="$(dirname "$dir")"
    done
    return 1
}

UNITY_ROOT=$(find_unity_root) || {
    echo "${RED}Error: Not inside a Unity project (no Assets/ + ProjectSettings/ found).${RESET}" >&2
    exit 1
}

if [[ -z "$SCAN_PATH" ]]; then
    SCAN_PATH="$UNITY_ROOT/Assets"
fi

# ---------------------------------------------------------------------------
# Counters
# ---------------------------------------------------------------------------
ERRORS=0
WARNINGS=0

report_issue() {
    local severity="$1" file="$2" line="$3" message="$4"
    if [[ "$severity" == "ERROR" ]]; then
        echo "${RED}ERROR${RESET}: ${BOLD}$file:$line${RESET} — $message"
        ERRORS=$((ERRORS + 1))
    else
        echo "${YELLOW}WARNING${RESET}: ${BOLD}$file:$line${RESET} — $message"
        WARNINGS=$((WARNINGS + 1))
    fi
}

# ---------------------------------------------------------------------------
# Check 1: UI Views must not reference gameplay types (MVP boundary)
# ---------------------------------------------------------------------------
echo "${BOLD}${CYAN}[1/5] Checking UI View / Presenter boundary...${RESET}"

VIEW_FILES=$(find "$SCAN_PATH" -path "*/UI/Views/*" -name "*.cs" 2>/dev/null || true)

# Gameplay types a View must never reference directly — Components, Systems,
# EventChannels, and the ServiceLocator all belong to the Presenter side of
# the boundary (architecture.md §5, MVP for UI).
GAMEPLAY_TYPE_PATTERN='\bServiceLocator\b|EventChannelSO\b'

while IFS= read -r FILE; do
    [[ -z "$FILE" ]] && continue

    LINE_NUM=$(grep -nE "$GAMEPLAY_TYPE_PATTERN" "$FILE" \
        | grep -v 'architecture:ignore' | grep -v '^\s*//' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" \
            "UI View references a gameplay-facing type (ServiceLocator/event channel) — only a Presenter may; the View should expose plain setters instead"
    fi
done <<< "$VIEW_FILES"
echo ""

# ---------------------------------------------------------------------------
# Check 2: No singletons outside the ServiceLocator
# ---------------------------------------------------------------------------
echo "${BOLD}${CYAN}[2/5] Checking for singleton patterns...${RESET}"

ALL_CS=$(find "$SCAN_PATH" -name "*.cs" -not -path "*/Editor/*" -not -path "*/Tests/*" 2>/dev/null || true)

while IFS= read -r FILE; do
    [[ -z "$FILE" ]] && continue

    # ServiceLocator.cs itself is the one sanctioned registry — skip it.
    case "$FILE" in */ServiceLocator.cs) continue ;; esac

    # Static Instance pattern
    LINE_NUM=$(grep -nE 'static\s+\w+\s+Instance\b' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "Singleton pattern detected (static Instance) — register with ServiceLocator instead"
    fi

    # FindObjectOfType outside of tests
    LINE_NUM=$(grep -nE 'FindObjectOfType|FindObjectsOfType|FindFirstObjectByType' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "FindObjectOfType usage — fetch shared systems via ServiceLocator.Get<T>(), sibling components via GetComponent"
    fi

    # DontDestroyOnLoad outside a bootstrap
    LINE_NUM=$(grep -nE 'DontDestroyOnLoad' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "DontDestroyOnLoad — prefer a bootstrap scene with an AppBootstrap"
    fi
done <<< "$ALL_CS"
echo ""

# ---------------------------------------------------------------------------
# Check 3: No coroutines
# ---------------------------------------------------------------------------
echo "${BOLD}${CYAN}[3/5] Checking for coroutine usage...${RESET}"

while IFS= read -r FILE; do
    [[ -z "$FILE" ]] && continue

    LINE_NUM=$(grep -nE 'StartCoroutine|StopCoroutine|StopAllCoroutines' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "Coroutine usage — prefer async Awaitable for new code"
    fi

    LINE_NUM=$(grep -nE 'IEnumerator\b.*\(' "$FILE" | grep -v 'architecture:ignore' | grep -v '^\s*//' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "IEnumerator method (likely coroutine) — prefer async Awaitable"
    fi

    LINE_NUM=$(grep -nE 'yield\s+return' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
    if [[ -n "$LINE_NUM" ]]; then
        report_issue "WARNING" "$FILE" "$LINE_NUM" "yield return (coroutine) — prefer await Awaitable.NextFrameAsync(token) / WaitForSecondsAsync(s, token)"
    fi
done <<< "$ALL_CS"
echo ""

# ---------------------------------------------------------------------------
# Check 4: ServiceLocator.Get<T>() called from Awake — the documented
# ordering gotcha (architecture.md §6): Unity doesn't guarantee Awake order
# across objects, so a consumer fetching in its own Awake can run before the
# service has registered. Heuristic: flag a Get< call appearing between an
# Awake( line and the next Start(/private void/public void method boundary
# is hard to do reliably in grep, so this checks the simpler, still-useful
# signal — Get< appearing anywhere in a file that also defines Awake but no
# Start, which is the shape most likely to hit the bug.
# ---------------------------------------------------------------------------
echo "${BOLD}${CYAN}[4/5] Checking ServiceLocator.Get<T>() ordering...${RESET}"

while IFS= read -r FILE; do
    [[ -z "$FILE" ]] && continue
    case "$FILE" in */ServiceLocator.cs) continue ;; esac

    HAS_GET=$(grep -cE 'ServiceLocator\.Get<' "$FILE" 2>/dev/null || true)
    HAS_AWAKE=$(grep -cE 'void\s+Awake\s*\(' "$FILE" 2>/dev/null || true)
    HAS_START=$(grep -cE 'void\s+Start\s*\(' "$FILE" 2>/dev/null || true)

    if [[ "${HAS_GET:-0}" -gt 0 && "${HAS_AWAKE:-0}" -gt 0 && "${HAS_START:-0}" -eq 0 ]]; then
        LINE_NUM=$(grep -nE 'ServiceLocator\.Get<' "$FILE" | grep -v 'architecture:ignore' | head -1 | cut -d: -f1 || true)
        if [[ -n "$LINE_NUM" ]]; then
            report_issue "WARNING" "$FILE" "$LINE_NUM" \
                "ServiceLocator.Get<T>() with an Awake but no Start — confirm this isn't fetching from inside Awake (Awake order isn't guaranteed across objects; fetch in Start instead)"
        fi
    fi
done <<< "$ALL_CS"
echo ""

# ---------------------------------------------------------------------------
# Check 5: Systems register/unregister with the ServiceLocator
# ---------------------------------------------------------------------------
echo "${BOLD}${CYAN}[5/5] Checking System registration with ServiceLocator...${RESET}"

SYSTEM_FILES=$(find "$SCAN_PATH" -path "*/Systems/*" -name "*.cs" 2>/dev/null || true)

while IFS= read -r FILE; do
    [[ -z "$FILE" ]] && continue

    HAS_REGISTER=$(grep -cE 'ServiceLocator\.Register<' "$FILE" 2>/dev/null || true)
    HAS_UNREGISTER=$(grep -cE 'ServiceLocator\.Unregister<' "$FILE" 2>/dev/null || true)

    if [[ "${HAS_REGISTER:-0}" -eq 0 ]]; then
        report_issue "WARNING" "$FILE" "1" \
            "File in Scripts/Systems/ has no ServiceLocator.Register<T>() call — confirm it's meant to be reached via the locator"
    elif [[ "${HAS_UNREGISTER:-0}" -eq 0 ]]; then
        report_issue "WARNING" "$FILE" "1" \
            "Registers with ServiceLocator but never calls Unregister<T>() in OnDestroy — it will leak a stale reference across scene reloads"
    fi
done <<< "$SYSTEM_FILES"
echo ""

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
echo "═══════════════════════════════════════════════════════════════"
TOTAL=$((ERRORS + WARNINGS))
if [[ $TOTAL -eq 0 ]]; then
    echo "${GREEN}${BOLD}Architecture check passed.${RESET} No issues found."
else
    echo "${BOLD}Architecture check: ${RED}$ERRORS error(s)${RESET}, ${YELLOW}$WARNINGS warning(s)${RESET}"
    if [[ $ERRORS -gt 0 ]]; then
        echo "Errors indicate architecture-stack violations that should be fixed."
    fi
    echo ""
    echo "Suppress false positives by adding ${CYAN}// architecture:ignore${RESET} to the line."
fi
echo "═══════════════════════════════════════════════════════════════"

exit 0
