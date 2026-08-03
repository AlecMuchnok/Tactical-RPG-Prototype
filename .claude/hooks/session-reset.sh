#!/usr/bin/env bash
# ============================================================================
# session-reset.sh — SESSION START HOOK
# Clears stale per-session hook state so gateguard/stop-validate start clean
# each session, instead of treating files read/edited/passed in a prior
# session as still valid. This replaces the old session-restore.sh, keeping
# only its state-cleanup lines — the branch/plan "session restored" report
# it used to print depended on session-save.sh, which has been removed.
#
# Also clears gateguard's and bash-gate's one-shot-per-file "already passed"
# state, which was NEVER cleared before this hook existed — without this, a
# file that passed the fact-gate once stayed exempt forever, across every
# future session.
# ============================================================================
# Trigger: SessionStart
# Exit: 0 always (advisory)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_PROFILE_LEVEL="standard"
source "${SCRIPT_DIR}/_lib.sh"

date +%s > "${UNITY_HOOK_STATE_DIR}/session-start-time"

# Read-before-Edit tracking (gateguard) and whole-file re-scan tracking (stop-validate)
rm -f "$UNITY_READS_FILE" "$UNITY_EDITS_FILE"

# gateguard's per-file fact-gate: without this, a file that passed once is
# exempt forever, across every future session.
rm -f "${UNITY_HOOK_STATE_DIR}"/gateguard-facts-*.txt

# bash-gate's per-command fact-gate: same one-shot-forever issue.
rm -f "${UNITY_HOOK_STATE_DIR}/bash-gate-denied.txt"

exit 0
