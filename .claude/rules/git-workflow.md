# Git Workflow Rules

`main` is protected — pushes directly to it are rejected on GitHub. Two rules follow from that:

## 1. Pull `main` before planning a new feature

Before starting requirements-gathering or a plan for new work (`/unity-workflow`, `/unity-feature`, `/unity-interview`, or any ad hoc "let's build X"), run:

```bash
git checkout main
git pull origin main
```

**Why:** planning against a stale `main` risks designing around code that's already changed upstream, and risks branching from a point that will produce an avoidable merge conflict later. This is cheap to do and expensive to skip.

If the working tree isn't clean (uncommitted changes present), stop and ask rather than pulling over them — see the general "before any command that could discard uncommitted work" rule in your operating instructions.

## 2. Create a new branch before implementing a new feature

Never commit implementation work directly on `main`. Before writing code for a new feature or fix:

```bash
git checkout -b <type>/<short-description>
```

Suggested prefixes: `feature/`, `fix/`, `chore/`. Branch names should be short and kebab-case (e.g. `feature/turn-order-system`, `fix/grid-hover-glitch`).

**Enforced, not just documented:** `.claude/hooks/require-feature-branch.sh` blocks `git commit` while `main` (or `master`) is checked out. First attempt is denied with instructions to create a branch; there is no second-attempt bypass the way other gates work here, because the fix (`git checkout -b ...`) is a single command with no judgment call attached. Escape hatch: `DISABLE_HOOK_REQUIRE_FEATURE_BRANCH=1` if you genuinely need to commit on `main` (e.g. a one-line doc fix you intend to push through a PR from a throwaway branch anyway — even then, prefer just making the branch).

## What this does NOT cover

- Merging the feature branch back to `main` (via PR) is a manual step — this project doesn't script that.
- These rules apply to feature/fix work. Read-only investigation, planning documents, and conversation don't need a branch — only the first commit does, and the hook catches that moment precisely.
