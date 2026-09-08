#!/usr/bin/env python3
"""Bake Kenney's CC0 glTF models into one binary mesh pack the game can draw.

    python3 tooling/fetch_scenery.py            # fetch (cached), bake, write
    python3 tooling/fetch_scenery.py --probe    # report which pieces are reachable
    python3 tooling/fetch_scenery.py --report   # sizes and triangle counts, write nothing

WHY THIS EXISTS RATHER THAN AN IMPORTER. Unity cannot import .glb natively; the packages
that can (glTFast, UnityGLTF) bring a second material and rendering path alongside the
Graphics.RenderMeshInstanced one everything in this game already uses, plus a GUID per
model in a repo whose .meta files are all hand-written. Baking offline sidesteps all of it:
one committed file, one .meta, no importer, no prefabs, and the meshes arrive in exactly the
shape the existing instanced renderer wants.

WHERE THE MODELS COME FROM. https://github.com/shorepine/kenney mirrors Kenney's whole CC0
library with one file per path, which matters because it is the only asset host reachable
from the build container — kenney.nl, itch.io and the GitHub API are all blocked, and so is
the zip endpoint, so files must be fetched one at a time.

HOW COLOUR ARRIVES, WHICH IS TWO WAYS. Most kits use a single 'colormap' material pointing
at a 512x512 gradient atlas and give every vertex a UV into a flat patch of it; the nature
kit has no image at all and instead splits a model into one primitive per material, each
carrying a linear baseColorFactor. Both are baked down to a vertex colour here, so the game
needs no textures, no UV channel and no per-piece material.

Colour is baked HONESTLY — Kenney's own palette, not a mood. The verge is darkened at
runtime from the world theme instead, so the same pack serves all eight worlds and the
grimness of the roadside is a value to tune rather than a re-bake.
"""
import argparse
import hashlib
import json
import math
import os
import struct
import sys
import urllib.request
import zlib

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(REPO, "tooling", ".cache", "kenney")
BASE = "https://raw.githubusercontent.com/shorepine/kenney/main/3d/"
PACK = os.path.join(REPO, "Assets", "Resources", "Meshes", "scenery.bytes")
NAMES_CS = os.path.join(REPO, "Assets", "Scripts", "Core", "Art", "SceneryPieces.cs")
ASSETS_MD = os.path.join(REPO, "Assets", "Art", "ASSETS.md")

PACK_MAGIC = b"BRSP"
PACK_VERSION = 1
MeshPackVertexBytes = 14   # must match MeshPack.VertexBytes
NAME_BYTES = 24

# --- what gets baked -------------------------------------------------------
#
# Grouped by the zone that draws it, because that is what decides how large it may be and
# how hard the runtime is allowed to darken it. Names are Kenney's own file names, found by
# probing: the mirror has no directory listing and the GitHub API is blocked, so --probe
# exists to re-check this list rather than to discover it from scratch.
#
# VERGE  |x| 5-12 m   small, and pushed toward the world's stone at runtime
# FIELD  |x| 12-40 m  the middle distance: trees, fences, carts, the things you go past
# MARK   |x| 30-90 m  landmarks: castles, mills, crypts, cliffs
PIECES = [
    # ---- verge --------------------------------------------------------------
    ("verge", "graveyard", "gravestone-round"),
    ("verge", "graveyard", "gravestone-wide"),
    ("verge", "graveyard", "gravestone-broken"),
    ("verge", "graveyard", "gravestone-cross"),
    ("verge", "graveyard", "gravestone-bevel"),
    ("verge", "graveyard", "gravestone-decorative"),
    ("verge", "graveyard", "gravestone-debris"),
    ("verge", "graveyard", "grave"),
    ("verge", "graveyard", "grave-border"),
    ("verge", "graveyard", "cross"),
    ("verge", "graveyard", "debris"),
    ("verge", "graveyard", "rocks"),
    ("verge", "graveyard", "candle"),
    ("verge", "graveyard", "lantern-candle"),
    ("verge", "graveyard", "bench"),
    ("verge", "graveyard", "bench-damaged"),
    ("verge", "nature", "rock_smallA"),
    ("verge", "nature", "rock_smallB"),
    ("verge", "nature", "rock_smallC"),
    ("verge", "nature", "rock_tallA"),
    ("verge", "nature", "rock_tallB"),
    ("verge", "nature", "stump_old"),
    ("verge", "nature", "stump_oldTall"),
    ("verge", "nature", "stump_round"),
    ("verge", "nature", "log"),
    ("verge", "nature", "grass"),
    ("verge", "nature", "grass_large"),
    ("verge", "nature", "grass_leafs"),
    ("verge", "nature", "plant_bushSmall"),
    ("verge", "nature", "plant_bushDetailed"),
    ("verge", "nature", "mushroom_redGroup"),
    ("verge", "nature", "stone_smallA"),
    ("verge", "nature", "stone_tallA"),

    # ---- field --------------------------------------------------------------
    ("field", "nature", "tree_default"),
    ("field", "nature", "tree_oak"),
    ("field", "nature", "tree_tall"),
    ("field", "nature", "tree_thin"),
    ("field", "nature", "tree_small"),
    ("field", "nature", "tree_simple"),
    ("field", "nature", "tree_pineDefaultA"),
    ("field", "nature", "tree_pineTallA"),
    ("field", "nature", "tree_pineTallB"),
    ("field", "nature", "tree_pineRoundA"),
    ("field", "nature", "tree_pineSmallA"),
    ("field", "nature", "tree_plateau"),
    ("field", "nature", "rock_largeA"),
    ("field", "nature", "rock_largeB"),
    ("field", "nature", "rock_largeC"),
    ("field", "nature", "log_large"),
    ("field", "nature", "fence_simple"),
    ("field", "nature", "crops_bambooStageA"),
    ("field", "nature", "crop_carrot"),
    ("field", "nature", "crop_melon"),
    ("field", "nature", "statue_obelisk"),
    ("field", "nature", "statue_column"),
    ("field", "nature", "statue_ring"),
    ("field", "nature", "tent_detailedOpen"),
    ("field", "fantasy-town", "fence"),
    ("field", "fantasy-town", "fence-broken"),
    ("field", "fantasy-town", "fence-gate"),
    ("field", "fantasy-town", "cart"),
    ("field", "fantasy-town", "stall"),
    ("field", "fantasy-town", "planks"),
    ("field", "fantasy-town", "lantern"),
    ("field", "graveyard", "iron-fence"),
    ("field", "graveyard", "iron-fence-curve"),
    ("field", "graveyard", "iron-fence-damaged"),
    ("field", "graveyard", "brick-wall"),
    ("field", "graveyard", "brick-wall-curve"),
    ("field", "graveyard", "brick-wall-end"),
    ("field", "graveyard", "pillar-large"),
    ("field", "graveyard", "pillar-square"),
    ("field", "graveyard", "crypt-small"),
    ("field", "graveyard", "coffin"),
    ("field", "survival", "tent"),
    ("field", "survival", "barrel"),
    ("field", "survival", "fence"),

    # ---- landmarks ----------------------------------------------------------
    ("mark", "castle", "wall"),
    ("mark", "castle", "wall-corner"),
    ("mark", "castle", "wall-half"),
    ("mark", "castle", "wall-narrow"),
    ("mark", "castle", "wall-doorway"),
    ("mark", "castle", "tower-base"),
    ("mark", "castle", "tower-square"),
    ("mark", "castle", "tower-square-base"),
    ("mark", "castle", "tower-square-mid"),
    ("mark", "castle", "tower-square-top"),
    ("mark", "castle", "tower-square-roof"),
    ("mark", "castle", "tower-top"),
    ("mark", "castle", "gate"),
    ("mark", "castle", "flag"),
    ("mark", "castle", "flag-wide"),
    ("mark", "castle", "siege-tower"),
    ("mark", "castle", "stairs-stone"),
    ("mark", "fantasy-town", "wall"),
    ("mark", "fantasy-town", "wall-half"),
    ("mark", "fantasy-town", "wall-door"),
    ("mark", "fantasy-town", "wall-corner"),
    ("mark", "fantasy-town", "wall-broken"),
    ("mark", "fantasy-town", "wall-wood"),
    ("mark", "fantasy-town", "roof"),
    ("mark", "fantasy-town", "roof-corner"),
    ("mark", "fantasy-town", "roof-gable"),
    ("mark", "fantasy-town", "roof-high"),
    ("mark", "fantasy-town", "roof-point"),
    ("mark", "fantasy-town", "roof-flat"),
    ("mark", "fantasy-town", "chimney"),
    ("mark", "fantasy-town", "windmill"),
    ("mark", "fantasy-town", "watermill"),
    ("mark", "fantasy-town", "overhang"),
    ("mark", "graveyard", "crypt"),
    ("mark", "graveyard", "crypt-large"),
    ("mark", "graveyard", "crypt-door"),
    ("mark", "graveyard", "column-large"),
    ("mark", "nature", "cliff_rock"),
    ("mark", "nature", "cliff_top_rock"),
    ("mark", "nature", "statue_head"),
    ("mark", "nature", "tree_pineDefaultB"),
    ("mark", "nature", "tree_pineGroundA"),
]


# --- fetching --------------------------------------------------------------

def fetch(rel):
    """Cached GET. Returns bytes, or None when the mirror does not have it."""
    dst = os.path.join(CACHE, rel.replace("/", "__"))
    if os.path.exists(dst):
        with open(dst, "rb") as f:
            return f.read()
    os.makedirs(CACHE, exist_ok=True)
    try:
        with urllib.request.urlopen(BASE + rel, timeout=30) as r:
            data = r.read()
    except Exception:
        return None
    with open(dst, "wb") as f:
        f.write(data)
    return data


# --- the smallest glTF reader that does this job ---------------------------

COMPONENT = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
             5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def parse_glb(data):
    if data[:4] != b"glTF":
        raise ValueError("not a glb")
    off, js, binoff = 12, None, None
    while off < len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        off += 8
        if ctype == 0x4E4F534A:
            js = json.loads(data[off:off + clen])
        elif ctype == 0x004E4942:
            binoff = off
        off += clen
    if js is None:
        raise ValueError("glb has no JSON chunk")
    return js, binoff


def read_accessor(data, js, binoff, index):
    a = js["accessors"][index]
    bv = js["bufferViews"][a["bufferView"]]
    fmt, size = COMPONENT[a["componentType"]]
    n = NCOMP[a["type"]]
    stride = bv.get("byteStride") or n * size
    base = binoff + bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * n, data, base + i * stride)
            for i in range(a["count"])]


def linear_to_srgb8(c):
    c = max(0.0, min(1.0, c))
    s = 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return int(round(255 * max(0.0, min(1.0, s))))


class Atlas:
    """The kit's shared colormap. Loaded once per kit, decoded without Pillow."""

    _cache = {}

    @classmethod
    def get(cls, kit):
        if kit in cls._cache:
            return cls._cache[kit]
        png = fetch(f"{kit}/Textures/colormap.png")
        cls._cache[kit] = None if png is None else cls(png)
        return cls._cache[kit]

    def __init__(self, png):
        self.w, self.h, self.rows = decode_png(png)

    def sample(self, u, v):
        # NO V FLIP. Verified against castle/wall: flipping yields solid black because the
        # atlas is authored top-down to match these UVs. Clamped rather than wrapped, so a
        # UV exactly at 1.0 lands on the last texel instead of the first.
        x = min(self.w - 1, max(0, int(u * self.w)))
        y = min(self.h - 1, max(0, int(v * self.h)))
        row = self.rows[y]
        return row[x * 3], row[x * 3 + 1], row[x * 3 + 2]


def decode_png(data):
    """
    Minimal PNG reader. Returns (w, h, [rgb rows]).

    Handles the three forms Kenney's atlases actually ship in — 8-bit truecolour, truecolour
    with alpha, and 8-bit INDEXED, which is what the castle and town kits use and which cost
    a run to discover. Pillow is not a dependency because tooling here has to run wherever
    CI does; the whole decoder is one zlib call and five filter cases.
    """
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a png")
    pos, idat, w, palette = 8, bytearray(), None, None
    while pos < len(data):
        (ln,) = struct.unpack_from(">I", data, pos)
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or interlace != 0 or colour not in (2, 3, 6):
                raise ValueError(f"unsupported png: depth {depth} colour {colour}")
            channels = {2: 3, 3: 1, 6: 4}[colour]
        elif typ == b"PLTE":
            palette = body
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
        pos += 12 + ln
    if colour == 3 and palette is None:
        raise ValueError("indexed png with no palette")
    raw = zlib.decompress(bytes(idat))
    stride = w * channels
    out, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if f == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif f == 3:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif f == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                c = prev[i - channels] if i >= channels else 0
                b = prev[i]
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pred) & 0xFF
        elif f != 0:
            raise ValueError(f"bad png filter {f}")
        if channels == 3:
            out.append(bytes(line))
        elif channels == 4:
            out.append(bytes(b for i in range(0, stride, 4) for b in line[i:i + 3]))
        else:
            out.append(bytes(b for i in line for b in palette[i * 3:i * 3 + 3]))
        prev = line
    return w, h, out


def bake(kit, name):
    """-> (positions, normals, colours, indices) with every primitive merged."""
    data = fetch(f"{kit}/{name}.glb")
    if data is None:
        return None
    js, binoff = parse_glb(data)
    atlas = None
    pos, nrm, col, idx = [], [], [], []
    for mesh in js.get("meshes", []):
        for prim in mesh["primitives"]:
            if prim.get("mode", 4) != 4:
                continue                        # triangles only; nothing here is a strip
            attrs = prim["attributes"]
            p = read_accessor(data, js, binoff, attrs["POSITION"])
            n = (read_accessor(data, js, binoff, attrs["NORMAL"])
                 if "NORMAL" in attrs else [(0.0, 1.0, 0.0)] * len(p))
            tri = [t[0] for t in read_accessor(data, js, binoff, prim["indices"])]

            material = js["materials"][prim["material"]] if "material" in prim else {}
            pbr = material.get("pbrMetallicRoughness", {})
            if "baseColorTexture" in pbr and "TEXCOORD_0" in attrs:
                if atlas is None:
                    atlas = Atlas.get(kit)
                uv = read_accessor(data, js, binoff, attrs["TEXCOORD_0"])
                if atlas is None:
                    c = [(200, 200, 200)] * len(p)
                else:
                    c = [atlas.sample(u, v) for u, v in uv]
            else:
                f = pbr.get("baseColorFactor", [0.8, 0.8, 0.8, 1.0])
                c = [tuple(linear_to_srgb8(x) for x in f[:3])] * len(p)

            base = len(pos)
            pos += p
            nrm += n
            col += c
            idx += [i + base for i in tri]
    if not pos:
        return None
    return pos, nrm, col, idx


def weld(pos, nrm, col, idx):
    """
    Merge vertices identical in position, normal AND colour.

    Kenney's meshes are flat-shaded, so a corner shared by three faces is three vertices
    with three different normals and must stay three vertices — welding on position alone
    would smooth every hard edge in the pack and turn a castle into a blob. This only
    removes true duplicates, which are common because each primitive is exported with the
    whole model's vertex buffer.
    """
    remap, out_p, out_n, out_c, lookup = [], [], [], [], {}
    for i in range(len(pos)):
        key = (tuple(round(v, 5) for v in pos[i]),
               tuple(round(v, 3) for v in nrm[i]), col[i])
        j = lookup.get(key)
        if j is None:
            j = len(out_p)
            lookup[key] = j
            out_p.append(pos[i])
            out_n.append(nrm[i])
            out_c.append(col[i])
        remap.append(j)
    used = [remap[i] for i in idx]
    # A vertex the index buffer never reaches is dead weight; drop it and renumber.
    seen, compact, final = {}, [], []
    for v in used:
        j = seen.get(v)
        if j is None:
            j = len(compact)
            seen[v] = j
            compact.append(v)
        final.append(j)
    return ([out_p[v] for v in compact], [out_n[v] for v in compact],
            [out_c[v] for v in compact], final)


# --- the pack --------------------------------------------------------------

def build_pack(baked):
    """
    Layout, all little-endian:

        magic 'BRSP' (4) | version u16 | pieceCount u16
        pieceCount records of 64 bytes:
            name        24 bytes utf8, NUL padded
            vertexStart u32   vertexCount u32
            indexStart  u32   indexCount  u32
            boundsMin   3 x f32
            boundsMax   3 x f32          (24 + 16 + 24 = 64)
        vertex block, 14 bytes per vertex:
            position 3 x u16   quantised across the piece's own bounds
            normal   3 x i8    /127
            padding  1 x u8    so colour always starts at offset 10
            colour   4 x u8    rgba, alpha always 255
        index block, u16 per index

    Positions quantise per piece rather than per pack: a gravestone and a castle wall
    share no scale, and one global quantum would spend all its precision on the castle.
    """
    verts, idxs, records = bytearray(), bytearray(), bytearray()
    for name, (pos, nrm, col, idx) in baked:
        if len(pos) > 0xFFFF:
            raise ValueError(f"{name}: {len(pos)} vertices exceeds the u16 index space")
        lo = [min(p[i] for p in pos) for i in range(3)]
        hi = [max(p[i] for p in pos) for i in range(3)]
        span = [max(hi[i] - lo[i], 1e-6) for i in range(3)]

        raw = bytes(name.encode("utf-8"))
        if len(raw) > NAME_BYTES:
            raise ValueError(f"{name}: name over {NAME_BYTES} bytes")
        records += raw + b"\0" * (NAME_BYTES - len(raw))
        records += struct.pack("<IIII", len(verts) // MeshPackVertexBytes, len(pos), len(idxs) // 2, len(idx))
        records += struct.pack("<3f", *lo) + struct.pack("<3f", *hi)

        for i in range(len(pos)):
            q = [min(65535, max(0, int(round((pos[i][k] - lo[k]) / span[k] * 65535.0))))
                 for k in range(3)]
            n = nrm[i]
            ln = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]) or 1.0
            verts += struct.pack("<3H3bB4B", q[0], q[1], q[2],
                                 max(-127, min(127, int(round(n[0] / ln * 127)))),
                                 max(-127, min(127, int(round(n[1] / ln * 127)))),
                                 max(-127, min(127, int(round(n[2] / ln * 127)))),
                                 0, col[i][0], col[i][1], col[i][2], 255)
        for i in idx:
            idxs += struct.pack("<H", i)

    head = PACK_MAGIC + struct.pack("<HH", PACK_VERSION, len(baked))
    return bytes(head + records + verts + idxs)


CS_TEMPLATE = '''// GENERATED by tooling/fetch_scenery.py. Do not edit by hand.
//
// The names inside Assets/Resources/Meshes/scenery.bytes, mirrored into Core so a palette
// or a landmark layout can reference a piece and a TEST can prove the reference resolves —
// without the test needing the binary, a file path, or an engine.
namespace BattleRunner.Core.Art
{{
    /// <summary>Which band draws a piece, and therefore how large it may be.</summary>
    public enum SceneryZone
    {{
        Verge = 0,
        Field = 1,
        Landmark = 2
    }}

    public static class SceneryPieces
    {{
        /// <summary>Every piece in the pack, in pack order. Index doubles as the piece id.</summary>
        public static readonly string[] Names =
        {{
{names}
        }};

        /// <summary>The zone each piece belongs to, parallel to <see cref="Names"/>.</summary>
        public static readonly SceneryZone[] Zones =
        {{
{zones}
        }};

        /// <summary>Object-space height in metres, parallel to <see cref="Names"/>.</summary>
        public static readonly float[] Heights =
        {{
{heights}
        }};

        public static int Count => Names.Length;

        /// <summary>Pack index of a piece, or -1. Linear because this runs once at load.</summary>
        public static int IndexOf(string name)
        {{
            for (int i = 0; i < Names.Length; i++)
                if (Names[i] == name) return i;
            return -1;
        }}
    }}
}}
'''

ZONE_CS = {"verge": "SceneryZone.Verge", "field": "SceneryZone.Field",
           "mark": "SceneryZone.Landmark"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--probe", action="store_true", help="report reachability, write nothing")
    ap.add_argument("--report", action="store_true", help="bake and measure, write nothing")
    args = ap.parse_args()

    if args.probe:
        missing = [f"{k}/{n}" for _, k, n in PIECES if fetch(f"{k}/{n}.glb") is None]
        print(f"{len(PIECES) - len(missing)}/{len(PIECES)} reachable")
        for m in missing:
            print("  MISSING", m)
        return 1 if missing else 0

    baked, meta, missing = [], [], []
    for zone, kit, name in PIECES:
        got = bake(kit, name)
        if got is None:
            missing.append(f"{kit}/{name}")
            continue
        pos, nrm, col, idx = weld(*got)
        key = f"{kit[:2]}_{name}"
        if len(key.encode()) > NAME_BYTES:
            key = key[:NAME_BYTES]
        baked.append((key, (pos, nrm, col, idx)))
        height = max(p[1] for p in pos) - min(p[1] for p in pos)
        meta.append((key, zone, kit, name, len(pos), len(idx) // 3, height))

    if missing:
        print(f"WARNING: {len(missing)} piece(s) unreachable and skipped:", file=sys.stderr)
        for m in missing:
            print("  ", m, file=sys.stderr)

    blob = build_pack(baked)
    tris = sum(m[5] for m in meta)
    print(f"{len(baked)} pieces, {sum(m[4] for m in meta)} verts, {tris} tris, "
          f"{len(blob) / 1024:.1f} KB")
    if args.report:
        for key, zone, kit, name, v, t, h in sorted(meta, key=lambda m: -m[5]):
            print(f"  {zone:5s} {key:24s} {v:5d}v {t:5d}t  h={h:.2f}")
        return 0

    os.makedirs(os.path.dirname(PACK), exist_ok=True)
    with open(PACK, "wb") as f:
        f.write(blob)

    def col(values):
        return "\n".join("            " + v + "," for v in values)

    os.makedirs(os.path.dirname(NAMES_CS), exist_ok=True)
    with open(NAMES_CS, "w", newline="\n") as f:
        f.write(CS_TEMPLATE.format(
            names=col(f'"{m[0]}"' for m in meta),
            zones=col(ZONE_CS[m[1]] for m in meta),
            heights=col(f"{m[6]:.4f}f" for m in meta)))

    os.makedirs(os.path.dirname(ASSETS_MD), exist_ok=True)
    with open(ASSETS_MD, "w", newline="\n") as f:
        f.write("# Imported assets\n\n"
                "Every 3D model in this game that was not written as code. All of it is "
                "Kenney's, released under **Creative Commons Zero (CC0)** — free for "
                "commercial use with no attribution required. We credit anyway.\n\n"
                "- Source: <https://kenney.nl> · mirror <https://github.com/shorepine/kenney>\n"
                "- Licence: `Assets/Art/LICENSE-KENNEY-CC0.txt`\n"
                f"- Fetched via `tooling/fetch_scenery.py` (pack sha256 "
                f"`{hashlib.sha256(blob).hexdigest()[:16]}`)\n\n"
                "The `.glb` files are not committed. They are baked — vertex colours sampled "
                "from each kit's atlas, duplicate vertices welded, positions quantised — into "
                "the single binary `Assets/Resources/Meshes/scenery.bytes`. Re-run the script "
                "to change the selection.\n\n"
                "| Piece | Kit | Kenney name | Zone | Tris | Height |\n"
                "|---|---|---|---|---:|---:|\n")
        for key, zone, kit, name, v, t, h in meta:
            f.write(f"| `{key}` | {kit} | `{name}` | {zone} | {t} | {h:.2f} |\n")

    print(f"wrote {os.path.relpath(PACK, REPO)}, "
          f"{os.path.relpath(NAMES_CS, REPO)}, {os.path.relpath(ASSETS_MD, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
