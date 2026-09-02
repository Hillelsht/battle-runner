#!/usr/bin/env python3
"""Verify every assembly definition declares the packages its code actually uses.

Unity assembly references are NOT transitive: an asmdef that references A cannot
use types from the assemblies A references. Miss one and the only symptom is a
CS0234 "namespace does not exist" from a headless editor sixteen minutes later,
which is an expensive way to learn you forgot a line of JSON.

This is a text check, not a compile. It looks for package namespaces and a few
distinctive type names in the .cs files under each asmdef, and asserts the asmdef
names the assembly that provides them. False negatives are fine — anything it does
catch, it catches instantly.

Usage:  python3 tooling/check_asmdef_refs.py
Exit 0 = clean, 1 = a missing reference (each printed with the line to add).
"""
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(REPO, "Assets")

# (human-readable trigger, compiled pattern, required assembly name).
# Patterns match anywhere in the file, so a fully-qualified use counts the same
# as a using directive.
RULES = [
    ("UnityEngine.Rendering.Universal",
     re.compile(r"\bUnityEngine\.Rendering\.Universal\b"),
     "Unity.RenderPipelines.Universal.Runtime"),
    ("Volume / VolumeProfile / VolumeComponent",
     re.compile(r"\bVolume(?:Profile|Component|Parameter)?\b"),
     "Unity.RenderPipelines.Core.Runtime"),
    ("UnityEngine.InputSystem",
     re.compile(r"\bUnityEngine\.InputSystem\b"),
     "Unity.InputSystem"),
    ("UnityEngine.UI",
     re.compile(r"\bUnityEngine\.UI\b"),
     "UnityEngine.UI"),
]

# Assemblies Unity supplies to every script without an asmdef reference.
ALWAYS_AVAILABLE = {"UnityEngine", "UnityEditor"}


def find_asmdefs():
    """Map each asmdef path to the directory it governs."""
    found = []
    for dirpath, _, filenames in os.walk(ASSETS):
        for name in filenames:
            if name.endswith(".asmdef"):
                found.append(os.path.join(dirpath, name))
    return found


def owning_asmdef(cs_path, asmdef_dirs):
    """The nearest asmdef at or above this file — the one that compiles it."""
    best = None
    for asmdef_path, directory in asmdef_dirs:
        if cs_path.startswith(directory + os.sep):
            if best is None or len(directory) > len(best[1]):
                best = (asmdef_path, directory)
    return best[0] if best else None


def main():
    asmdefs = find_asmdefs()
    asmdef_dirs = [(p, os.path.dirname(p)) for p in asmdefs]

    parsed = {}
    for path in asmdefs:
        with open(path, encoding="utf-8") as handle:
            parsed[path] = json.load(handle)

    # asmdef path -> {required assembly -> (trigger, example file)}
    needed = {}

    for dirpath, _, filenames in os.walk(ASSETS):
        for name in filenames:
            if not name.endswith(".cs"):
                continue
            cs_path = os.path.join(dirpath, name)
            owner = owning_asmdef(cs_path, asmdef_dirs)
            if owner is None:
                continue
            if parsed[owner].get("noEngineReferences"):
                continue  # engine-free by design; it uses none of this

            with open(cs_path, encoding="utf-8") as handle:
                text = handle.read()

            for trigger, pattern, assembly in RULES:
                if assembly in ALWAYS_AVAILABLE:
                    continue
                if pattern.search(text):
                    rel = os.path.relpath(cs_path, REPO).replace(os.sep, "/")
                    needed.setdefault(owner, {}).setdefault(assembly, (trigger, rel))

    problems = []
    for asmdef_path, requirements in sorted(needed.items()):
        declared = set(parsed[asmdef_path].get("references") or [])
        rel_asmdef = os.path.relpath(asmdef_path, REPO).replace(os.sep, "/")
        for assembly, (trigger, example) in sorted(requirements.items()):
            if assembly not in declared:
                problems.append(
                    f"  x {rel_asmdef} uses {trigger} ({example})\n"
                    f"    but does not reference \"{assembly}\"\n"
                    f"    -> add \"{assembly}\" to its \"references\" array")

    if problems:
        print(f"asmdef reference check FAILED — {len(problems)} problem(s):\n")
        print("\n\n".join(problems))
        print("\nUnity assembly references are not transitive; each assembly must")
        print("name every package assembly whose types it uses.")
        return 1

    print(f"asmdef references clean ({len(asmdefs)} assemblies checked)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
