#!/usr/bin/env bash
#
# sync-upstream.sh — Pull changes from the original WindBot into your fork.
#
# Daily cycle:
#   1. fetch upstream   -> downloads the original's history (does NOT touch your code)
#   2. merge            -> integrates those changes into your current branch
#   3. (you) resolve conflicts if any appear
#   4. push origin      -> uploads the result to your repo
#
# Uses merge (not rebase) on purpose: it preserves history and never forces you
# into a push --force. It's the safe path.
#
# Usage:
#   ./scripts/sync-upstream.sh            # sync the current branch
#   ./scripts/sync-upstream.sh --push     # also push to origin when done

set -euo pipefail

UPSTREAM_REMOTE="upstream"
UPSTREAM_BRANCH="master"
DO_PUSH=false

[[ "${1:-}" == "--push" ]] && DO_PUSH=true

# --- 0. Safeguard: the working tree must be clean --------------------------
if [[ -n "$(git status --porcelain)" ]]; then
  echo "✋ You have uncommitted changes. Commit or stash them before syncing."
  echo "   git status   to see what's pending."
  exit 1
fi

if ! git remote get-url "$UPSTREAM_REMOTE" >/dev/null 2>&1; then
  echo "✋ Remote '$UPSTREAM_REMOTE' does not exist."
  echo "   git remote add upstream https://code.moenext.com/nanahira/windbot.git"
  exit 1
fi

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"

# --- 1. Pull changes from the original (safe: does not modify your code) ----
echo "→ git fetch $UPSTREAM_REMOTE"
git fetch "$UPSTREAM_REMOTE"

# --- Is there anything new? ------------------------------------------------
BEHIND="$(git rev-list --count "HEAD..$UPSTREAM_REMOTE/$UPSTREAM_BRANCH")"
if [[ "$BEHIND" -eq 0 ]]; then
  echo "✓ Already up to date with $UPSTREAM_REMOTE/$UPSTREAM_BRANCH. Nothing to pull."
  exit 0
fi
echo "→ There are $BEHIND new commit(s) in $UPSTREAM_REMOTE/$UPSTREAM_BRANCH."

# --- 2. Integrate with merge -----------------------------------------------
echo "→ git merge $UPSTREAM_REMOTE/$UPSTREAM_BRANCH"
if git merge "$UPSTREAM_REMOTE/$UPSTREAM_BRANCH"; then
  echo "✓ Clean merge, no conflicts."
else
  echo ""
  echo "⚠ There are CONFLICTS. This is normal, not an error."
  echo "  1. Open the marked files (git status lists them)."
  echo "  2. Decide which version stays in each conflict."
  echo "  3. git add <file> and then: git commit"
  echo "  4. Re-run this script with --push, or push manually."
  exit 1
fi

# --- 3. Optional push ------------------------------------------------------
if $DO_PUSH; then
  echo "→ git push origin $CURRENT_BRANCH"
  git push origin "$CURRENT_BRANCH"
  echo "✓ Pushed to origin/$CURRENT_BRANCH."
else
  echo ""
  echo "✓ Done locally. To upload it:  git push origin $CURRENT_BRANCH"
  echo "  (or run this script with --push next time)"
fi
