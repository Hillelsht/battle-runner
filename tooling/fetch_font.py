#!/usr/bin/env python3
"""Fetch the one font the game ships, and prove it can draw every language.

    python3 tooling/fetch_font.py            # download, verify, install
    python3 tooling/fetch_font.py --report   # verify what is installed, write nothing

WHY A FONT AT ALL. Until now this project shipped none: UiFactory resolved Unity's built-in
LegacyRuntime.ttf and that was the only typeface in the build. That was fine while the game spoke
only English, and it is not fine now. Unity configures no fallback chain, so a codepoint the
built-in font lacks renders as an empty box on device — and there is no way to discover that from a
build machine. The project has already lost content to this twice: the HUD's pips were deleted
(HudScreen) and the tutorial's arrows became ASCII ^ and v (TutorialDirector), both with comments
saying exactly that.

Russian needs Cyrillic and Hebrew needs the Hebrew block. Neither is something to hope for.

WHY ARIMO. It is metric-compatible with Arial, which is what LegacyRuntime already is — so the
English screens, every one of which was laid out by eye against fixed pixel widths, move as little
as it is possible to move them while changing typeface at all. It carries Latin, Cyrillic, Greek and
Hebrew in one 316 KB file, so there is one Font asset and one atlas rather than a fallback chain.
And it is SIL OFL 1.1, which is the same "free for commercial use, credit anyway" footing as the
Kenney models already in Assets/Art.

WHAT THIS SCRIPT ACTUALLY GUARANTEES. Not that a file downloaded — that a file downloaded AND
contains every codepoint the game can put on screen. It parses the font's own cmap table and checks
the ranges below. A font that fetched successfully and silently lacked Hebrew would be the exact
failure this script exists to make impossible.
"""
import hashlib
import os
import struct
import sys
import urllib.request

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEST = os.path.join(REPO, "Assets", "Resources", "Fonts", "Arimo-Regular.ttf")
LICENCE = os.path.join(REPO, "Assets", "Art", "LICENSE-ARIMO-OFL.txt")

# Pinned by CONTENT, not by trust in the URL. Google serves versioned paths that move; the hash is
# what says this is the file that was measured.
URL = "https://fonts.gstatic.com/s/arimo/v36/P5sfzZCDf9_T_3cV7NCUECyoxNk37cxsBw.ttf"
SHA256 = "e5717ff6c2063b0e596176ddad929b0a98a6d8db694f43a8ba26c99675625b67"
LICENCE_URL = "https://raw.githubusercontent.com/googlefonts/Arimo/main/OFL.txt"

# Every range the game can render. Keep this honest: if a string ever needs a glyph outside it,
# add the range here first and let the check fail until the font really has it.
REQUIRED = {
    "Latin letters": list(range(0x41, 0x5B)) + list(range(0x61, 0x7B)),
    "digits": list(range(0x30, 0x3A)),
    "ASCII punctuation": [0x20, 0x21, 0x25, 0x27, 0x28, 0x29, 0x2B, 0x2C, 0x2D, 0x2E,
                          0x2F, 0x3A, 0x3F, 0x5E, 0x76, 0x78],
    "Cyrillic": list(range(0x410, 0x450)) + [0x401, 0x451],
    "Hebrew": list(range(0x5D0, 0x5EB)) + [0x5F3, 0x5F4],
    "typography already in use": [0x2014, 0x00B7, 0x2019, 0x2022, 0x2026],
}


def codepoints(path):
    """Every codepoint the font maps, read out of its cmap table."""
    data = open(path, "rb").read()
    tables = struct.unpack(">H", data[4:6])[0]
    cmap = None
    for i in range(tables):
        off = 12 + 16 * i
        if data[off:off + 4] == b"cmap":
            cmap = struct.unpack(">I", data[off + 8:off + 12])[0]
    if cmap is None:
        raise SystemExit("no cmap table: this is not a usable font")

    subtables = struct.unpack(">H", data[cmap + 2:cmap + 4])[0]
    best = None
    for i in range(subtables):
        off = cmap + 4 + 8 * i
        pid, eid, rel = struct.unpack(">HHI", data[off:off + 8])
        sub = cmap + rel
        fmt = struct.unpack(">H", data[sub:sub + 2])[0]
        # Format 12 wins where it exists: it is the one that reaches past the BMP.
        if fmt == 4 and (pid, eid) in ((3, 1), (0, 3), (0, 4)):
            best = (4, sub)
        elif fmt == 12 and (pid, eid) in ((3, 10), (0, 4), (0, 6)):
            best = (12, sub)
            break
    if best is None:
        raise SystemExit("no Unicode cmap subtable")

    fmt, sub = best
    found = set()
    if fmt == 4:
        seg_x2 = struct.unpack(">H", data[sub + 6:sub + 8])[0]
        segs = seg_x2 // 2
        ends = struct.unpack(">%dH" % segs, data[sub + 14:sub + 14 + seg_x2])
        starts_at = sub + 16 + seg_x2
        starts = struct.unpack(">%dH" % segs, data[starts_at:starts_at + seg_x2])
        for lo, hi in zip(starts, ends):
            if lo == 0xFFFF:
                continue
            found.update(range(lo, min(hi, 0xFFFE) + 1))
    else:
        groups = struct.unpack(">I", data[sub + 12:sub + 16])[0]
        for i in range(groups):
            off = sub + 16 + 12 * i
            lo, hi, _ = struct.unpack(">III", data[off:off + 12])
            found.update(range(lo, hi + 1))
    return found


def verify(path):
    """True when the font can draw everything the game might ask it to."""
    have = codepoints(path)
    clean = True
    print(f"{os.path.relpath(path, REPO)}  {os.path.getsize(path):,} bytes  "
          f"{len(have):,} codepoints")
    for label, wanted in REQUIRED.items():
        missing = [c for c in wanted if c not in have]
        if missing:
            clean = False
            shown = " ".join(f"U+{c:04X}" for c in missing[:10])
            print(f"  {label:28s} MISSING {shown}")
        else:
            print(f"  {label:28s} ok ({len(wanted)} codepoints)")
    return clean


def fetch(url, expect=None):
    with urllib.request.urlopen(url, timeout=60) as response:
        blob = response.read()
    got = hashlib.sha256(blob).hexdigest()
    if expect and got != expect:
        raise SystemExit(f"sha256 mismatch for {url}\n  expected {expect}\n  got      {got}")
    return blob


def main():
    report_only = "--report" in sys.argv
    if report_only:
        if not os.path.exists(DEST):
            raise SystemExit(f"no font installed at {DEST}")
        raise SystemExit(0 if verify(DEST) else 1)

    os.makedirs(os.path.dirname(DEST), exist_ok=True)
    os.makedirs(os.path.dirname(LICENCE), exist_ok=True)

    blob = fetch(URL, SHA256)
    with open(DEST, "wb") as handle:
        handle.write(blob)
    print(f"wrote {os.path.relpath(DEST, REPO)}")

    licence = fetch(LICENCE_URL)
    with open(LICENCE, "wb") as handle:
        handle.write(licence)
    print(f"wrote {os.path.relpath(LICENCE, REPO)}")

    if not verify(DEST):
        raise SystemExit("the font downloaded but cannot draw the game")


if __name__ == "__main__":
    main()
