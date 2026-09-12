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
command -v python3 >/dev/null 2>&1 || exit 0

deny() {
  jq -n --arg reason "$1" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: $reason
    }
  }'
  exit 0
}

# Brackets first: a file that cannot parse makes every other check meaningless,
# and this is the failure that costs a five-minute headless-editor round trip to
# discover. It has already happened once (ContentFactory.cs, CS1513).
braces="$root/tooling/check_csharp_braces.py"
if [ -f "$braces" ] && ! output=$(python3 "$braces" 2>&1); then
  deny "C# brackets do not balance, so this push is blocked.

$output

Fix the file, then push again. To push anyway: git push --no-verify."
fi

# Then locals: the Gameplay assembly has no local compiler, so a name collision there
# costs a full CI round trip to discover. This finds the one class of it that a text
# check can find honestly.
shadow="$root/tooling/check_local_shadowing.py"
if [ -f "$shadow" ] && ! output=$(python3 "$shadow" 2>&1); then
  deny "A local variable is declared twice in one scope, so this push is blocked.

$output

Rename one of them, then push again. To push anyway: git push --no-verify."
fi

checker="$root/tooling/check_docs.py"
[ -f "$checker" ] || exit 0

if output=$(python3 "$checker" 2>&1); then
  exit 0
fi

deny "Docs are out of date, so this push is blocked.

$output

Fix the docs (README.md / docs/ / CHANGELOG.md), then push again. To push anyway: git push --no-verify."
