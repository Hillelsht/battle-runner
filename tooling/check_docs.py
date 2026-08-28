#!/usr/bin/env python3
"""Verify the docs still describe the code.

Prose can't be machine-checked, but the facts embedded in it can, and those are
what actually rot: a test count that drifts, a Unity version bumped in
ProjectSettings but not in the README, a doc added to docs/ that nothing links
to, a script directory the code layout never mentions.

Two modes:
  static     (default) cross-check documented facts against the repo
  staleness  (--range A..B) require docs to move when code moves

Usage:
  python3 tooling/check_docs.py
  python3 tooling/check_docs.py --range origin/main..HEAD

Exit code 0 = clean, 1 = problems found (each printed with how to fix it).
"""
import argparse
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
README = os.path.join(REPO, "README.md")
DOCS_DIR = os.path.join(REPO, "docs")
CHANGELOG = os.path.join(REPO, "CHANGELOG.md")

# Code changes here oblige a docs change in the same push.
CODE_PREFIXES = ("Assets/", "Packages/", "ProjectSettings/", ".github/workflows/", "tooling/")
DOC_PREFIXES = ("README.md", "docs/", "CHANGELOG.md")
SKIP_TOKEN = "[skip-docs]"
# The token waives the rule only when it stands alone on its own line, the way a
# git trailer does. Matching it anywhere in the message would mean any commit that
# merely *mentions* the escape hatch — this file's own commit, for one — disarms it.
SKIP_PATTERN = re.compile(r"^\s*\[skip-docs\]\s*$", re.M)

problems = []


def problem(what, fix):
    problems.append((what, fix))


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def count_tests():
    total = 0
    tests_dir = os.path.join(REPO, "Assets", "Tests")
    for dirpath, _, filenames in os.walk(tests_dir):
        for name in filenames:
            if name.endswith(".cs"):
                total += len(re.findall(r"^\s*\[Test\]", read(os.path.join(dirpath, name)), re.M))
    return total


def current_text(path, text):
    """A changelog records history, so only its pre-release preamble describes today."""
    if os.path.abspath(path) != os.path.abspath(CHANGELOG):
        return text
    match = re.search(r"^##\s*\[?v?\d+\.\d+\.\d+", text, re.M)
    return text[:match.start()] if match else text


def check_test_count(docs):
    actual = count_tests()
    if actual == 0:
        problem("No [Test] methods found under Assets/Tests",
                "If the suite moved, update check_docs.py's count_tests().")
        return
    for path, text in docs.items():
        scoped = current_text(path, text)
        for match in re.finditer(r"\b(\d+)\s+tests\b", scoped):
            claimed = int(match.group(1))
            if claimed != actual:
                line = scoped[:match.start()].count("\n") + 1
                problem(f"{rel(path)}:{line} claims {claimed} tests, but the suite has {actual}",
                        f"Change '{claimed} tests' to '{actual} tests'.")


def check_unity_version(docs):
    version_file = os.path.join(REPO, "ProjectSettings", "ProjectVersion.txt")
    if not os.path.exists(version_file):
        problem("ProjectSettings/ProjectVersion.txt is missing",
                "Restore it; every build pins the editor version from this file.")
        return
    match = re.search(r"m_EditorVersion:\s*(\S+)", read(version_file))
    if not match:
        problem("ProjectVersion.txt has no m_EditorVersion line", "Restore the pin.")
        return
    version = match.group(1)
    if version not in read(README):
        problem(f"README does not mention the pinned Unity version {version}",
                f"State {version} in the Requirements section so contributors install the right editor.")


def check_doc_index():
    if not os.path.isdir(DOCS_DIR):
        return
    readme = read(README)
    on_disk = sorted(n for n in os.listdir(DOCS_DIR) if n.endswith(".md"))
    for name in on_disk:
        if f"docs/{name}" not in readme:
            problem(f"docs/{name} exists but README never links to it",
                    "Add a row for it to the Architecture table in README.md.")
    for link in set(re.findall(r"\(docs/([A-Za-z0-9._-]+\.md)\)", readme)):
        if not os.path.exists(os.path.join(DOCS_DIR, link)):
            problem(f"README links to docs/{link}, which does not exist",
                    "Fix the link or add the file.")


def check_code_layout():
    readme = read(README)
    scripts_dir = os.path.join(REPO, "Assets", "Scripts")
    if not os.path.isdir(scripts_dir):
        return
    for name in sorted(os.listdir(scripts_dir)):
        if name.endswith(".meta") or not os.path.isdir(os.path.join(scripts_dir, name)):
            continue
        if f"Assets/Scripts/{name}" not in readme:
            problem(f"Assets/Scripts/{name}/ is not described in the README code layout",
                    f"Add an 'Assets/Scripts/{name}' line to the code layout block.")


def check_changelog():
    if not os.path.exists(CHANGELOG):
        problem("CHANGELOG.md is missing",
                "Create it; it is where release status lives and what the README points at.")
        return
    text = read(CHANGELOG)
    versions = re.findall(r"^##\s*\[?v?(\d+\.\d+\.\d+)\]?", text, re.M)
    if not versions:
        problem("CHANGELOG.md has no '## vX.Y.Z' release heading",
                "Add a heading per release, newest first.")
        return
    latest = versions[0]
    readme = read(README)
    readme_versions = re.findall(r"\bv(\d+\.\d+\.\d+)\b", readme)
    if readme_versions and readme_versions[0] != latest:
        problem(f"README's first version reference is v{readme_versions[0]} "
                f"but CHANGELOG's newest release is v{latest}",
                f"Point the README's release links at v{latest}.")


def changed_files(rev_range):
    out = subprocess.run(["git", "diff", "--name-only", rev_range],
                         cwd=REPO, capture_output=True, text=True)
    if out.returncode != 0:
        return None
    return [line.strip() for line in out.stdout.splitlines() if line.strip()]


def commit_messages(rev_range):
    out = subprocess.run(["git", "log", "--format=%B", rev_range],
                         cwd=REPO, capture_output=True, text=True)
    return out.stdout if out.returncode == 0 else ""


def check_staleness(rev_range):
    files = changed_files(rev_range)
    if files is None:
        print(f"  (skipped staleness: '{rev_range}' is not a resolvable range)")
        return
    if not files:
        return
    if SKIP_PATTERN.search(commit_messages(rev_range)):
        print(f"  (staleness waived: a commit message carries {SKIP_TOKEN} on its own line)")
        return

    touched_code = [f for f in files if f.startswith(CODE_PREFIXES)]
    touched_docs = [f for f in files if f.startswith(DOC_PREFIXES)]
    if touched_code and not touched_docs:
        preview = ", ".join(touched_code[:4]) + (" ..." if len(touched_code) > 4 else "")
        problem(f"{len(touched_code)} code file(s) changed with no docs update ({preview})",
                "Record what changed in CHANGELOG.md, or update README/docs. "
                f"If this genuinely needs no docs, put {SKIP_TOKEN} on its own line "
                "in a commit message.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--range", dest="rev_range",
                        help="commit range to check for docs staleness, e.g. origin/main..HEAD")
    args = parser.parse_args()

    if not os.path.exists(README):
        print("FAIL: README.md is missing")
        return 1

    docs = {README: read(README)}
    if os.path.isdir(DOCS_DIR):
        for name in sorted(os.listdir(DOCS_DIR)):
            if name.endswith(".md"):
                path = os.path.join(DOCS_DIR, name)
                docs[path] = read(path)
    if os.path.exists(CHANGELOG):
        docs[CHANGELOG] = read(CHANGELOG)

    check_test_count(docs)
    check_unity_version(docs)
    check_doc_index()
    check_code_layout()
    check_changelog()
    if args.rev_range:
        check_staleness(args.rev_range)

    if problems:
        print(f"docs check FAILED — {len(problems)} problem(s):\n")
        for what, fix in problems:
            print(f"  x {what}")
            print(f"    -> {fix}\n")
        return 1

    print(f"docs check clean ({count_tests()} tests, docs indexed, layout and changelog consistent)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
