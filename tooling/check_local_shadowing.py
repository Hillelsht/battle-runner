#!/usr/bin/env python3
"""Catch C# locals declared twice in one scope, in the assemblies no local gate compiles.

    python3 tooling/check_local_shadowing.py

WHY THIS EXISTS. `dotnet test` compiles BattleRunner.Core and nothing else. The Gameplay,
Meta, Data and Editor assemblies reference UnityEngine and cannot be built without an editor,
so the FIRST compiler that ever sees them is the one in CI, six minutes after a push. Two
commits went out with `Matrix4x4[] into` declared in a method that already had a `float into`
eighty lines further down the same block — CS0128 plus a CS1503 cascade, both obvious on
sight, neither catchable by any gate that existed.

This is not a C# parser and does not pretend to be one. It tracks brace depth inside method
bodies and flags a local declaration whose name is already live in an enclosing or equal
scope. That is exactly the error above and a family of near misses around it, and it is the
one class of compile error that a text-level check can find honestly.

WHAT IT DELIBERATELY DOES NOT DO. It says nothing about types, members, overloads or
generics; those need a compiler. CI is still the compiler. This just stops the cheapest
mistake from costing a round trip.
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOTS = [os.path.join(REPO, "Assets", "Scripts")]

# A local declaration: an optional `const`, a type, a name, then `=` or `;`.
#
# NOT a trailing `)`. The first version accepted that too, meaning to catch a parameter on a
# continuation line — and it caught them so well that every method parameter was recorded in
# the CLASS scope and never popped, so the next method to reuse the name was reported. The
# error class this file is for is LOCALS, and a local is followed by `=` or `;`.
DECL = re.compile(
    r"^\s*(?:const\s+)?"
    r"(?P<type>[A-Za-z_][\w.<>,\[\]\s]*?[\w>\]])\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*"
    r"(?:=[^=]|;)"
)

# Lines that look like declarations but are not, or that this check cannot reason about.
SKIP = re.compile(
    r"^\s*(?:return|throw|new|if|else|for|foreach|while|switch|case|do|using|namespace|"
    r"public|private|protected|internal|static|abstract|sealed|override|virtual|partial|"
    r"\[|//|/\*|\*|#)"
)

# C# keywords that can appear where a type would and never start a local declaration.
NOT_A_TYPE = {"return", "throw", "new", "await", "yield", "in", "out", "ref", "is", "as"}


def strip_strings_and_comments(text):
    """Blank out string literals and comments so their contents cannot look like code."""
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == '"':
            out.append('"')
            i += 1
            while i < n and text[i] != '"':
                if text[i] == "\\":
                    out.append(" ")
                    i += 1
                out.append(" ")
                i += 1
            out.append('"' if i < n else "")
            i += 1
        elif c == "'":
            out.append("'")
            i += 1
            while i < n and text[i] != "'":
                if text[i] == "\\":
                    out.append(" ")
                    i += 1
                out.append(" ")
                i += 1
            out.append("'" if i < n else "")
            i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                out.append(" ")
                i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                out.append("\n" if text[i] == "\n" else " ")
                i += 1
            out.append("  ")
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def check(path):
    raw = open(path, encoding="utf-8").read()
    clean = strip_strings_and_comments(raw)
    raw_lines = raw.split("\n")
    problems = []

    depth = 0
    # scopes[d] maps a name live at depth d to the line it was declared on.
    scopes = [{}]

    for lineno, line in enumerate(clean.split("\n"), start=1):
        opens = line.count("{")
        closes = line.count("}")

        # A declaration on a line that also opens a scope belongs to the OUTER scope
        # (`for (int i ...) {`), so look at it before descending.
        if not SKIP.match(line):
            m = DECL.match(line)
            if m:
                type_ = m.group("type").strip()
                name = m.group("name")
                first = type_.split()[0] if type_.split() else ""
                if first not in NOT_A_TYPE and "=>" not in line and "(" not in type_:
                    for d in range(len(scopes)):
                        if name in scopes[d]:
                            problems.append(
                                (lineno, name, scopes[d][name], raw_lines[lineno - 1].strip()))
                            break
                    else:
                        scopes[-1][name] = lineno

        for _ in range(opens):
            depth += 1
            scopes.append({})
        for _ in range(closes):
            if len(scopes) > 1:
                scopes.pop()
            depth = max(0, depth - 1)
        # A method body closing takes its locals with it; at depth 1 (class body) nothing
        # local should survive, which the pop above already guarantees.

    return problems


def main():
    total = 0
    files = 0
    for root in ROOTS:
        for dirpath, _, names in os.walk(root):
            for name in names:
                if not name.endswith(".cs"):
                    continue
                path = os.path.join(dirpath, name)
                files += 1
                for lineno, var, first, text in check(path):
                    rel = os.path.relpath(path, REPO)
                    print(f"  x {rel}:{lineno}: '{var}' is already declared at line {first}")
                    print(f"      {text}")
                    total += 1

    if total:
        print(f"\nlocal-shadowing check FAILED — {total} problem(s) in {files} files")
        print("  CS0128 in the making. Rename one of the two, or move the inner one.")
        return 1
    print(f"local scopes clean ({files} files checked)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
