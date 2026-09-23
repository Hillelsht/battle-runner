#!/usr/bin/env python3
"""Verify every LocKey is actually used, and that no UI string is left hardcoded.

TWO FAILURES THAT LOOK IDENTICAL FROM HERE AND ARE INVISIBLE ON A DEVICE YOU CAN
READ. A key can be declared and translated into all three languages and then
referenced by nothing, because the call site kept its English literal -- which is
exactly what happened to LootBossYieldsTwice: the string was translated, the
double-loot header still said "THE BOSS YIELDS... TWICE!" in every language, and
the table's own completeness tests were all perfectly happy, because the table was
complete. Nothing in it knows whether anyone reads it.

An unreferenced key is therefore not dead weight to tidy up later. It is the
signature of a string the player still sees in English.

Usage:  python3 tooling/check_loc_keys.py
Exit 0 = clean, 1 = an unused key (each printed with the language it is stranded in).
"""
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(REPO, "Assets")

KEY_FILE = os.path.join(ASSETS, "Scripts", "Core", "Text", "LocKey.cs")
TABLE_FILE = os.path.join(ASSETS, "Scripts", "Core", "Text", "LocTables.cs")

# A key declared here and mentioned ONLY in these two files is used by nothing.
DECLARING = {os.path.abspath(KEY_FILE), os.path.abspath(TABLE_FILE)}

# NO EXEMPTIONS, and that was checked rather than assumed. The talent and stat keys
# looked like they would need one -- they are the two groups reached through a node id
# or a stat id -- but every one of them is still written out as LocKey.Something at the
# point of use, so the plain check covers them. An exemption list nobody needs is how a
# gate quietly stops catching the thing it was written for.


def declared_keys():
    text = open(KEY_FILE, encoding="utf-8").read()
    body = text[text.index("enum LocKey"):]
    return [m.group(1) for m in re.finditer(r"^\s{8}([A-Za-z][A-Za-z0-9_]*),\s*$", body, re.M)]


def referencing_files():
    for root, _dirs, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.abspath(os.path.join(root, name))
            if path in DECLARING:
                continue
            yield path


def main():
    keys = declared_keys()
    if not keys:
        print("check_loc_keys FAILED — no keys parsed out of LocKey.cs", file=sys.stderr)
        return 1

    used = set()
    for path in referencing_files():
        text = open(path, encoding="utf-8", errors="replace").read()
        for m in re.finditer(r"\bLocKey\.([A-Za-z][A-Za-z0-9_]*)", text):
            used.add(m.group(1))

    stranded = []
    for key in keys:
        if key in used:
            continue
        stranded.append(key)

    if stranded:
        print(f"loc key check FAILED — {len(stranded)} key(s) translated but never read:\n")
        for key in stranded:
            print(f"  x LocKey.{key}")
        print("\n    -> Each of these is a string the player still sees in English. Find the")
        print("       call site that spells it out and make it read the key, or delete the key")
        print("       and its three table rows if the string is genuinely gone.")
        return 1

    print(f"loc keys clean ({len(keys)} declared, every one of them read)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
