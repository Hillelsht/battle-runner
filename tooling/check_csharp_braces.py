#!/usr/bin/env python3
"""Refuse a push whose C# cannot possibly parse.

Unity is the only compiler for everything outside BattleRunner.Core, and a headless
editor round trip costs about five minutes. A missing brace is the cheapest possible
way to spend that: it is not a design mistake or a subtle API misuse, it is a text
edit that dropped a character, and it has already cost this project one CI failure
(ContentFactory.cs, error CS1513).

This is deliberately NOT a parser. It strips comments, strings, chars and verbatim
strings, then counts the three bracket kinds. That catches a truncated file, an
over-eager splice and a bad merge, and it stays fast enough to run on every push.
Anything subtler is the compiler's job.

Usage:
  python3 tooling/check_csharp_braces.py            # every tracked .cs file
  python3 tooling/check_csharp_braces.py a.cs b.cs  # only these

Exit code 0 = balanced, 1 = at least one file cannot compile.
"""
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Order matters: verbatim strings before ordinary ones, or the @"" escape rules
# ("" is a literal quote inside them) are applied with the wrong grammar.
_BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.S)
_LINE_COMMENT = re.compile(r"//[^\n]*")
_VERBATIM = re.compile(r'@"(?:[^"]|"")*"', re.S)
_STRING = re.compile(r'"(?:\\.|[^"\\\n])*"')
_CHAR = re.compile(r"'(?:\\.|[^'\\])'")

PAIRS = (("{", "}", "braces"), ("(", ")", "parens"), ("[", "]", "brackets"))


def strip(source: str) -> str:
    source = _BLOCK_COMMENT.sub("", source)
    source = _VERBATIM.sub('""', source)
    source = _LINE_COMMENT.sub("", source)
    source = _STRING.sub('""', source)
    source = _CHAR.sub("''", source)
    return source


def tracked_sources():
    out = subprocess.run(
        ["git", "-C", REPO, "ls-files", "*.cs"],
        capture_output=True, text=True, check=False).stdout
    return [os.path.join(REPO, line) for line in out.split() if line.endswith(".cs")]


def main() -> int:
    paths = sys.argv[1:] or tracked_sources()
    problems = []

    for path in paths:
        try:
            with open(path, encoding="utf-8") as handle:
                code = strip(handle.read())
        except OSError as error:
            problems.append(f"{path}: cannot read ({error})")
            continue

        for opener, closer, name in PAIRS:
            opened, closed = code.count(opener), code.count(closer)
            if opened == closed:
                continue
            missing = "closing" if opened > closed else "opening"
            rel = os.path.relpath(path, REPO)
            problems.append(
                f"{rel}: {abs(opened - closed)} {missing} {name} "
                f"({opened} '{opener}' vs {closed} '{closer}')")

    if problems:
        print(f"C# bracket check FAILED — {len(problems)} problem(s):\n")
        for problem in problems:
            print(f"  x {problem}")
        print("\nA file this unbalanced cannot compile. Unity CI would take about five "
              "minutes to tell you the same thing.")
        return 1

    print(f"C# brackets balanced ({len(paths)} files checked)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
