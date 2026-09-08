#!/usr/bin/env python3
"""Lints the hand-written Unity serialized files before they ever reach the editor.

Checks (validation report G3):
- header: %YAML 1.1 + %TAG !u! lines, no BOM, no tabs anywhere
- every scene component listed in m_Component has a matching document, and vice versa
- every `guid:` referenced from .unity/.asset/.mat files exists in some .meta
- no duplicate GUIDs across .meta files; no duplicate fileID anchors within a document set
- EditorBuildSettings scene GUID matches the scene's .meta

Exit code 0 = clean. Run from repo root: python3 tooling/lint_unity_yaml.py
"""
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROBLEMS = []

BUILTIN_GUIDS = {
    "0000000000000000f000000000000000",  # built-in extra resources (Default-Skybox etc.)
    "0000000000000000e000000000000000",  # built-in editor resources
}


def problem(msg):
    PROBLEMS.append(msg)


def collect_meta_guids():
    guids = {}
    for dirpath, _, filenames in os.walk(os.path.join(REPO, "Assets")):
        for fname in filenames:
            if not fname.endswith(".meta"):
                continue
            path = os.path.join(dirpath, fname)
            with open(path, encoding="utf-8") as f:
                match = re.search(r"^guid: ([0-9a-f]{32})$", f.read(), re.M)
            if not match:
                problem(f"{rel(path)}: no guid line")
                continue
            guid = match.group(1)
            if guid in guids:
                problem(f"duplicate GUID {guid}: {rel(path)} and {guids[guid]}")
            guids[guid] = rel(path)
    return guids


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def check_text_basics(path, text):
    if text.startswith("﻿") or text.startswith("\xef\xbb\xbf"):
        problem(f"{rel(path)}: starts with a BOM")
    if "\t" in text:
        problem(f"{rel(path)}: contains tab characters")
    lines = text.splitlines()
    if not lines or lines[0] != "%YAML 1.1":
        problem(f"{rel(path)}: first line is not '%YAML 1.1'")
    elif len(lines) < 2 or lines[1] != "%TAG !u! tag:unity3d.com,2011:":
        problem(f"{rel(path)}: second line is not the %TAG !u! directive")


def check_guid_refs(path, text, known_guids):
    for match in re.finditer(r"guid: ([0-9a-f]{32})", text):
        guid = match.group(1)
        if guid in known_guids or guid in BUILTIN_GUIDS:
            continue
        problem(f"{rel(path)}: references unknown guid {guid}")


def check_scene(path, text):
    anchors = re.findall(r"^--- !u!\d+ &(\d+)", text, re.M)
    if len(anchors) != len(set(anchors)):
        problem(f"{rel(path)}: duplicate fileID anchors")
    anchor_set = set(anchors)

    listed = re.findall(r"^  - component: \{fileID: (\d+)\}", text, re.M)
    for file_id in listed:
        if file_id not in anchor_set:
            problem(f"{rel(path)}: m_Component references missing document {file_id}")

    for match in re.finditer(r"^  m_GameObject: \{fileID: (\d+)\}", text, re.M):
        if match.group(1) not in anchor_set and match.group(1) != "0":
            problem(f"{rel(path)}: component points at missing GameObject {match.group(1)}")

    if "SceneRoots:" not in text:
        problem(f"{rel(path)}: missing SceneRoots document")


def check_build_settings(known_guids):
    path = os.path.join(REPO, "ProjectSettings", "EditorBuildSettings.asset")
    with open(path, encoding="utf-8") as f:
        text = f.read()
    for scene_path, guid in re.findall(r"path: (\S+)\n\s+guid: ([0-9a-f]{32})", text):
        meta_path = os.path.join(REPO, scene_path + ".meta")
        if not os.path.exists(meta_path):
            problem(f"EditorBuildSettings: scene {scene_path} has no .meta")
            continue
        with open(meta_path, encoding="utf-8") as f:
            match = re.search(r"^guid: ([0-9a-f]{32})$", f.read(), re.M)
        if not match or match.group(1) != guid:
            problem(f"EditorBuildSettings: guid mismatch for {scene_path}")


def check_audio_clip_coupling():
    """
    Core names every clip it will load; the synthesiser decides which files exist. Nothing
    at compile time connects the two, and a name that drifts on either side produces exactly
    one symptom: silence, with a warning nobody reads. Same class of coupling as the crowd
    scale below, so it is pinned in the same place.
    """
    cue_path = os.path.join(REPO, "Assets", "Scripts", "Core", "Audio", "AudioCue.cs")
    synth_path = os.path.join(REPO, "tooling", "synth_audio.py")
    if not (os.path.exists(cue_path) and os.path.exists(synth_path)):
        return

    with open(cue_path, encoding="utf-8") as f:
        cue_src = f.read()
    wanted = set(re.findall(r'new CueMix\("([^"]+)"', cue_src))
    for match in re.finditer(r'public const string \w+ = "([^"]+)";', cue_src):
        wanted.add(match.group(1))
    wanted.discard("Audio/")            # the resource folder, not a clip

    with open(synth_path, encoding="utf-8") as f:
        made = set(re.findall(r'\(\s*"([^"]+)",\s*\w+\),', f.read()))
    if not wanted or not made:
        problem("audio coupling check found no clip names — "
                "if the tables moved, update check_audio_clip_coupling()")
        return

    for name in sorted(wanted - made):
        problem(f"Core asks for the clip '{name}' but synth_audio.py does not make it — "
                "add it to SOUNDS, or fix the name in AudioCue.cs")
    for name in sorted(made - wanted):
        problem(f"synth_audio.py makes '{name}' but nothing in Core plays it — "
                "add a cue for it, or drop it from SOUNDS")

    audio_dir = os.path.join(REPO, "Assets", "Resources", "Audio")
    if os.path.isdir(audio_dir):
        on_disk = {f[:-4] for f in os.listdir(audio_dir) if f.endswith(".wav")}
        for name in sorted(wanted - on_disk):
            problem(f"'{name}.wav' is missing from Assets/Resources/Audio — "
                    "run: python3 tooling/synth_audio.py")


def check_crowd_scale_coupling():
    """CrowdRenderer bakes the bob phase into the instance SCALE and the shader decodes
    it back out. The two constants live in different languages in different files, and
    if they drift the phase decodes wrong: every unit's bob desynchronises from its
    shadow and the march turns back into noise. Nothing at compile time connects them,
    so pin them here.
    """
    cs_path = os.path.join(REPO, "Assets/Scripts/Gameplay/Crowd/CrowdRenderer.cs")
    sh_path = os.path.join(REPO, "Assets/Resources/CrowdInstanced.shader")
    if not (os.path.exists(cs_path) and os.path.exists(sh_path)):
        return

    with open(cs_path, encoding="utf-8") as f:
        cs = f.read()
    with open(sh_path, encoding="utf-8") as f:
        sh = f.read()

    def grab(text, pattern, where):
        m = re.search(pattern, text)
        if not m:
            PROBLEMS.append(f"{where}: could not find {pattern!r} — the crowd scale "
                            "coupling check can no longer verify itself")
            return None
        return float(m.group(1))

    cs_min = grab(cs, r"ScaleMin\s*=\s*([0-9.]+)f", "CrowdRenderer.cs")
    cs_span = grab(cs, r"ScaleSpan\s*=\s*([0-9.]+)f", "CrowdRenderer.cs")
    sh_min = grab(sh, r"CROWD_SCALE_MIN\s+([0-9.]+)", "CrowdInstanced.shader")
    sh_span = grab(sh, r"CROWD_SCALE_SPAN\s+([0-9.]+)", "CrowdInstanced.shader")

    if None in (cs_min, cs_span, sh_min, sh_span):
        return
    if abs(cs_min - sh_min) > 1e-6 or abs(cs_span - sh_span) > 1e-6:
        PROBLEMS.append(
            f"crowd scale drift: CrowdRenderer.cs writes {cs_min}+phase*{cs_span} but "
            f"CrowdInstanced.shader decodes {sh_min}+phase*{sh_span}. The bob phase would "
            "decode wrong for every unit. Change both together.")


def main():
    known_guids = collect_meta_guids()

    for dirpath, _, filenames in os.walk(os.path.join(REPO, "Assets")):
        for fname in filenames:
            path = os.path.join(dirpath, fname)
            if fname.endswith((".unity", ".mat", ".asset")):
                with open(path, encoding="utf-8") as f:
                    text = f.read()
                check_text_basics(path, text)
                check_guid_refs(path, text, known_guids)
                if fname.endswith(".unity"):
                    check_scene(path, text)

    check_build_settings(known_guids)
    check_audio_clip_coupling()
    check_crowd_scale_coupling()

    if PROBLEMS:
        print(f"LINT FAILED — {len(PROBLEMS)} problem(s):")
        for p in PROBLEMS:
            print(f"  - {p}")
        sys.exit(1)
    print("lint clean")


if __name__ == "__main__":
    main()
