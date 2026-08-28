#!/usr/bin/env bash
# PreToolUse(Bash) gate: refuse `git push` while the docs are stale.
#
# The pre-push git hook already blocks a real push, but that failure surfaces as
# a wall of git output after the fact. This catches it one step earlier, at the
# point Claude proposes the command, and hands back the checker's own fix list.
#
# Reads the PreToolUse payload on stdin, writes a PreToolUse decision on stdout.
# Anything that is not a docs failure exits 0 silently, which allows the command.
set -uo pipefail

payload=$(cat)
command=$(printf '%s' "$payload" | jq -r '.tool_input.command // ""' 2>/dev/null) || exit 0

# Only guard pushes. `--no-verify` is the documented bypass for both hooks.
case "$command" in
  *"git push"*) ;;
  *) exit 0 ;;
esac
case "$command" in
  *--no-verify*) exit 0 ;;
esac

root=${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}
checker="$root/tooling/check_docs.py"
[ -f "$checker" ] && command -v python3 >/dev/null 2>&1 || exit 0

if output=$(python3 "$checker" 2>&1); then
  exit 0
fi

jq -n --arg reason "$output" '{
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason: ("Docs are out of date, so this push is blocked.\n\n" + $reason +
      "\nFix the docs (README.md / docs/ / CHANGELOG.md), then push again. To push anyway: git push --no-verify.")
  }
}'
exit 0
