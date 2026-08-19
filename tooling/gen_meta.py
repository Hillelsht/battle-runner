#!/usr/bin/env python3
"""Generate Unity .meta files for every asset under Assets/.

GUIDs are deterministic: md5("battle-runner:" + posix path relative to repo root).
Existing .meta files are left untouched, so hand-tuned metas survive re-runs.
Run from the repo root:  python3 tooling/gen_meta.py [--print PATH]
"""
import hashlib
import os
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(REPO_ROOT, "Assets")

TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

def guid_for(rel_path: str) -> str:
    return hashlib.md5(f"battle-runner:{rel_path}".encode()).hexdigest()

def importer_block(rel_path: str, is_dir: bool) -> str:
    if is_dir:
        return "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + TAIL
    ext = os.path.splitext(rel_path)[1].lower()
    if ext == ".cs":
        return ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
                "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n" + TAIL)
    if ext == ".asmdef":
        return "AssemblyDefinitionImporter:\n  externalObjects: {}\n" + TAIL
    if ext == ".shader":
        return ("ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n"
                "  nonModifiableTextures: []\n  preprocessorOverride: 0\n" + TAIL)
    if ext == ".mat":
        return ("NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n" + TAIL)
    if ext == ".unity":
        return "DefaultImporter:\n  externalObjects: {}\n" + TAIL
    return "DefaultImporter:\n  externalObjects: {}\n" + TAIL

def write_meta(abs_path: str, rel_path: str, is_dir: bool) -> bool:
    meta_path = abs_path + ".meta"
    if os.path.exists(meta_path):
        return False
    body = f"fileFormatVersion: 2\nguid: {guid_for(rel_path)}\n" + importer_block(rel_path, is_dir)
    with open(meta_path, "w", newline="\n") as f:
        f.write(body)
    return True

def main() -> None:
    if len(sys.argv) == 3 and sys.argv[1] == "--print":
        print(guid_for(sys.argv[2]))
        return
    created = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames.sort()
        for d in dirnames:
            abs_p = os.path.join(dirpath, d)
            rel_p = os.path.relpath(abs_p, REPO_ROOT).replace(os.sep, "/")
            created += write_meta(abs_p, rel_p, True)
        for fname in sorted(filenames):
            if fname.endswith(".meta"):
                continue
            abs_p = os.path.join(dirpath, fname)
            rel_p = os.path.relpath(abs_p, REPO_ROOT).replace(os.sep, "/")
            created += write_meta(abs_p, rel_p, False)
    print(f"created {created} .meta files")

if __name__ == "__main__":
    main()
