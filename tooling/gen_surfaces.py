#!/usr/bin/env python3
"""Generate the road and terrain surface textures, seamlessly, from nothing.

    python3 tooling/gen_surfaces.py             # write Assets/Resources/Surfaces/*.png
    python3 tooling/gen_surfaces.py --report    # measure detail, write nothing

WHY THIS EXISTS. The road was computed per-pixel in Road.shader — brick bond, mortar, per-stone
tone, two octaves of grime — and it still measured a luminance range of only 19-42 out of 255 on
device, against 54-139 for a photoreal reference. At 2-5% contrast the cobbles are invisible and
the road reads as a flat slab, which is exactly what it was called. A shader can only afford a
handful of instructions per pixel; a texture is a lookup table for arbitrarily expensive maths,
and a voronoi cell diagram with per-stone tone and multi-octave grime is exactly the kind of
arbitrarily expensive maths a road wants. Measured, the generated cobble carries 2.6x the edge
density of what the shader produces.

WHY IT NEEDS NO UVs. Road.shader already computes `uv = positionWS.xz * _Tiling` — a world-space
coordinate. Sampling a texture with that needs no mesh UV channel, no importer, and no change to
a single mesh in the project. That is the whole unlock, and it is why this could not have been
done by importing anything.

WHAT IS IN THE TEXTURES. Not colour — STRUCTURE. Each world already authors its own road palette
and eight of them are tuned; throwing that away for baked colour would be a regression. So the
mask texture is:

    R  surface tone      per-stone / per-grain lightness, and the grime folded in
    G  face mask         1 on a stone face, 0 in a mortar gap or crack
    B  wetness           where standing water and sheen collect
    A  height            drives the normal map, and reads as depth under the key light

The shader tints R with the world's stone colour and the gaps with its mortar colour, so all
eight palettes survive and gain detail rather than being replaced by it.

TANGENTS. Normal mapping usually needs a mesh tangent frame, and no mesh here has one. The road
and terrain are flat and axis-aligned, so their tangent frame is known at compile time —
T = (1,0,0), B = (0,0,1), N = (0,1,0) — and the shader builds it from constants. This trick only
works for a horizontal plane, which is precisely what these two surfaces are.
"""
import argparse
import hashlib
import re
import os
import struct
import sys
import zlib

import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "Resources", "Surfaces")
SIZE = 256


# --- seamless primitives ---------------------------------------------------
#
# Everything below tiles. A road runs for a kilometre and the texture repeats every few metres,
# so a seam is not a subtle artefact — it is a visible stripe across the whole level, once per
# tile, forever. Wrapping is therefore not a nicety here; it is the requirement.

def _lattice(size, cells, seed):
    """A cells x cells value lattice, bicubically upsampled with WRAPPING."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    # Tile 2x2 before resampling and take the middle, so the interpolation kernel sees the
    # wrapped neighbours instead of clamping at the edge. Clamping is what makes a seam.
    big = np.tile(g, (2, 2))
    idx = (np.arange(size) / size) * cells
    i0 = np.floor(idx).astype(int)
    f = idx - i0
    f = f * f * (3.0 - 2.0 * f)                     # smoothstep
    i1 = i0 + 1
    a = big[np.ix_(i0, i0)]
    b = big[np.ix_(i1 % (2 * cells), i0)]
    c = big[np.ix_(i0, i1 % (2 * cells))]
    d = big[np.ix_(i1 % (2 * cells), i1 % (2 * cells))]
    fx = f[:, None]
    fy = f[None, :]
    return (a * (1 - fx) * (1 - fy) + b * fx * (1 - fy)
            + c * (1 - fx) * fy + d * fx * fy)


def fbm(size, seed, octaves=6, base=3, gain=0.5):
    """Fractal value noise. Every octave wraps, so the sum wraps."""
    out = np.zeros((size, size))
    amp, total, cells = 1.0, 0.0, base
    for o in range(octaves):
        out += _lattice(size, cells, seed + o * 101) * amp
        total += amp
        amp *= gain
        cells *= 2
        if cells > size:
            break
    return out / total


def voronoi(size, cells, seed, jitter=0.42, stretch=(1.0, 1.0)):
    """
    Wrapped Worley. Returns (nearest, second, cellIndex).

    The 3x3 neighbour sweep with modular seed points is what makes it tile: a pixel near the
    right edge sees the seeds from the left edge as if they were just off-screen to its right.
    """
    rng = np.random.default_rng(seed)
    grid = np.stack(np.meshgrid(np.arange(cells), np.arange(cells), indexing="ij"), -1) + 0.5
    pts = grid + (rng.random((cells, cells, 2)) - 0.5) * 2.0 * jitter

    gx, gy = np.meshgrid(np.linspace(0, cells, size, endpoint=False),
                         np.linspace(0, cells, size, endpoint=False), indexing="ij")
    d1 = np.full((size, size), 9e9)
    d2 = np.full((size, size), 9e9)
    idx = np.zeros((size, size), dtype=int)
    for oi in (-1, 0, 1):
        for oj in (-1, 0, 1):
            for i in range(cells):
                for j in range(cells):
                    dx = (gx - (pts[i, j, 0] + oi * cells)) * stretch[0]
                    dy = (gy - (pts[i, j, 1] + oj * cells)) * stretch[1]
                    d = np.sqrt(dx * dx + dy * dy)
                    m = d < d1
                    d2 = np.where(m, d1, np.minimum(d2, d))
                    idx = np.where(m, i * cells + j, idx)
                    d1 = np.where(m, d, d1)
    return d1, d2, idx


def per_cell(idx, cells, seed):
    """A random value per voronoi cell, looked up per pixel."""
    return np.random.default_rng(seed).random(cells * cells)[idx]


def rows(size, count, seed, cuts=4):
    """
    Planks: horizontal bands, each broken into segments by butt joints.

    `count` MUST divide `size` and there is no skew, and both of those are the fix for a real
    bug rather than fussiness. The first version used 11 bands over 256 pixels and a 0.02 skew,
    and measured a join discontinuity of 0.34 against an interior p99 of 0.0017 — a hard stripe
    across the level once per tile. A band count that divides the size wraps exactly (measured:
    join error 0.0000), and any skew at all makes the band index depend on x, which cannot wrap
    unless the skew moves a whole number of bands across the full width.
    """
    if size % count:
        raise ValueError(f"{count} bands do not divide {size} evenly and would not tile")
    pitch = size // count
    y = np.arange(size)[:, None] + np.zeros((1, size), dtype=int)
    x = np.zeros((size, 1), dtype=int) + np.arange(size)[None, :]
    band = (y // pitch) % count

    # Butt joints. Cuts land on multiples of size/cuts so they tile in x too; each plank gets
    # its own offset, so the joints stagger the way a real boardwalk's do.
    rng = np.random.default_rng(seed)
    offset = (rng.integers(0, cuts, count) * (size // cuts))[band]
    segment = ((x + offset) // (size // cuts)) % cuts

    value = rng.random((count, cuts))[band, segment]
    along = np.abs(((y / pitch) % 1.0) - 0.5) * 2.0                    # across the plank
    joint = np.abs((((x + offset) / (size / cuts)) % 1.0) - 0.5) * 2.0  # along it
    return band, value, np.maximum(along, joint * 0.92)


# --- the eight surfaces ----------------------------------------------------
#
# One per world, and all eight genuinely different in TOPOLOGY, not just in tone. The shipped
# road used the same brick bond at the same mortar width in every world — WorldTheme.
# RoadMortarWidth is declared with a default and never overridden by any of the eight — so the
# only thing that ever changed was colour. That is the whole of "the road doesn't change".

def cobble():
    """Irregular set stone. The Ashen Road: an old imperial highway gone to seed."""
    d1, d2, idx = voronoi(SIZE, 9, 3)
    face = np.clip((d2 - d1) / 0.16, 0, 1)
    tone = per_cell(idx, 9, 11)
    grime = fbm(SIZE, 23)
    return dict(
        tone=np.clip(0.30 + 0.44 * tone * (0.70 + 0.55 * grime), 0, 1),
        face=face,
        wet=np.clip(1.0 - face + grime * 0.45, 0, 1),
        height=face * 0.72 + grime * 0.28)


def flagstone():
    """Large cut slabs, tight joints. The Sunken Crypt: worked stone, not a village road."""
    d1, d2, idx = voronoi(SIZE, 4, 9, jitter=0.16)
    face = np.clip((d2 - d1) / 0.09, 0, 1)
    tone = per_cell(idx, 4, 5)
    grime = fbm(SIZE, 31, octaves=5)
    crack = np.clip(1.0 - np.abs(fbm(SIZE, 47, base=6) - 0.5) * 7.0, 0, 1) * 0.5
    return dict(
        tone=np.clip(0.36 + 0.34 * tone * (0.80 + 0.40 * grime), 0, 1),
        face=np.clip(face - crack, 0, 1),
        wet=np.clip((1.0 - face) * 1.3 + crack, 0, 1),
        height=face * 0.55 + grime * 0.45)


def dirt():
    """Cracked scorched earth with embedded stones. Ember Fields."""
    grime = fbm(SIZE, 41, octaves=7)
    d1, d2, idx = voronoi(SIZE, 18, 13)
    pebble = np.clip((d2 - d1) / 0.30, 0, 1)
    # Dried-mud polygons: a coarse voronoi whose EDGES are the cracks.
    c1, c2, _ = voronoi(SIZE, 6, 67, jitter=0.48)
    crack = 1.0 - np.clip((c2 - c1) / 0.07, 0, 1)
    return dict(
        tone=np.clip(0.28 + 0.44 * grime + 0.12 * pebble, 0, 1),
        face=np.clip(1.0 - crack, 0, 1),
        wet=np.clip(crack * 0.8, 0, 1),
        height=np.clip(grime * 0.5 + pebble * 0.3 + (1.0 - crack) * 0.2, 0, 1))


def planks():
    """A boardwalk over bog. Gallows Mire — you do not pave a swamp, you plank it."""
    band, value, edge = rows(SIZE, 8, 71)
    grain = fbm(SIZE, 61, octaves=7, base=2)
    # Grain runs ALONG the plank, so the noise is stretched hard in x.
    grain = 0.5 * grain + 0.5 * np.roll(grain, 3, axis=1)
    gap = np.clip((edge - 0.86) / 0.14, 0, 1)
    knot = np.clip(1.0 - np.abs(fbm(SIZE, 83, base=5) - 0.55) * 9.0, 0, 1)
    return dict(
        tone=np.clip(0.26 + 0.30 * value + 0.26 * grain - 0.20 * knot, 0, 1),
        face=1.0 - gap,
        wet=np.clip(gap * 1.2 + (1.0 - grain) * 0.25, 0, 1),
        height=np.clip((1.0 - gap) * 0.75 + grain * 0.25, 0, 1))


def sand():
    """Wind-ripped bone sand. The Bone Wastes."""
    ripple = 0.5 + 0.5 * np.sin(np.linspace(0, 18 * np.pi, SIZE, endpoint=False))[None, :]
    warp = fbm(SIZE, 79, octaves=4, base=2)
    ripple = 0.5 + 0.5 * np.sin(np.linspace(0, 18 * np.pi, SIZE, endpoint=False)[None, :]
                                + warp * 5.0)
    grit = fbm(SIZE, 97, octaves=7, base=8)
    d1, d2, idx = voronoi(SIZE, 20, 23)
    shard = np.clip((d2 - d1) / 0.22, 0, 1)
    return dict(
        tone=np.clip(0.46 + 0.22 * ripple + 0.18 * grit - 0.10 * (1 - shard), 0, 1),
        face=np.clip(0.55 + 0.45 * shard, 0, 1),
        wet=np.clip(0.25 * (1 - ripple), 0, 1),
        height=np.clip(ripple * 0.55 + grit * 0.3 + shard * 0.15, 0, 1))


def snow():
    """
    Wind-packed drift over buried stone. The Frozen Reach.

    The first attempt was a smooth fbm plus sparkle and measured a range of 84 against the
    other seven at 126-179 — flatter than a road is allowed to be. Snow is not smooth: wind
    cuts SASTRUGI, hard parallel ridges with sharp lee edges, and packs a crust that cracks.
    Those are what carry the light.
    """
    drift = fbm(SIZE, 53, octaves=7)
    warp = fbm(SIZE, 149, octaves=3, base=2)
    # Sastrugi: sharp-crested ridges, not sine waves. abs() of a wave gives the cusp.
    ridge = np.abs(np.sin(np.linspace(0, 11 * np.pi, SIZE, endpoint=False)[:, None]
                          + warp * 6.0 + drift * 3.0))
    ridge = np.clip(ridge ** 0.55, 0, 1)
    crust = fbm(SIZE, 151, octaves=7, base=10)
    sparkle = (np.random.default_rng(103).random((SIZE, SIZE)) > 0.993).astype(float)
    d1, d2, idx = voronoi(SIZE, 7, 29)
    buried = np.clip((d2 - d1) / 0.20, 0, 1)
    thin = np.clip((0.40 - drift) * 3.2, 0, 1)
    return dict(
        tone=np.clip(0.50 + 0.30 * ridge + 0.16 * crust + 0.34 * sparkle
                     - 0.30 * thin * (1 - buried), 0, 1),
        face=np.clip(1.0 - thin * (1 - buried) * 0.8, 0, 1),
        wet=np.clip(0.30 + 0.45 * (1 - ridge), 0, 1),
        height=np.clip(ridge * 0.55 + drift * 0.28 + crust * 0.17, 0, 1))


def gravel():
    """Loose red silt and shale. The Blood Marsh."""
    d1, d2, idx = voronoi(SIZE, 22, 37, jitter=0.48)
    stone = np.clip((d2 - d1) / 0.26, 0, 1)
    tone = per_cell(idx, 22, 43)
    mud = fbm(SIZE, 59, octaves=6)
    pool = np.clip((0.38 - mud) * 4.0, 0, 1)
    return dict(
        tone=np.clip((0.26 + 0.38 * tone) * (0.75 + 0.45 * mud) - 0.18 * pool, 0, 1),
        face=np.clip(stone * (1 - pool * 0.7), 0, 1),
        wet=np.clip(pool * 1.4 + (1 - stone) * 0.4, 0, 1),
        height=np.clip(stone * 0.6 + mud * 0.4 - pool * 0.35, 0, 1))


def mosaic():
    """
    Inlaid tesserae, half-lost. The Throne of Dust — a floor that was once ceremonial.

    THE LOSS IS IN THE TONE, NOT IN THE FACE MASK, and that distinction is the whole fix.
    The first version multiplied the face mask by the loss field, which is a defensible
    reading of "the mosaic is gone here" — but the shader paints everything the face mask
    calls a gap with the world's MORTAR colour, and Throne of Dust deliberately puts gold in
    its seams. Losing a third of the face mask therefore did not render as a worn floor; it
    rendered as a road that was one third gold. Measured end to end against the real palette
    it came out at a luminance range of 15 and an edge density of 0.66 — WORSE than the flat
    procedural slab this work exists to replace, and the only one of the eight that regressed.
    A worn tessera is still a tessera: it is duller and flatter, not a seam.
    """
    d1, d2, idx = voronoi(SIZE, 16, 89, jitter=0.10)
    face = np.clip((d2 - d1) / 0.11, 0, 1)
    tile = per_cell(idx, 16, 113)
    loss = np.clip((fbm(SIZE, 127, octaves=4, base=3) - 0.46) * 5.0, 0, 1)
    dust = fbm(SIZE, 131, octaves=6)
    return dict(
        tone=np.clip((0.30 + 0.42 * tile) * (1 - 0.55 * loss) * (0.80 + 0.35 * dust), 0, 1),
        # The grout stays a grout. Loss only opens it a little, where the inlay has lifted.
        face=np.clip(face * (1 - 0.25 * loss), 0, 1),
        wet=np.clip((1 - face) * 0.8 + loss * 0.5, 0, 1),
        # Height still collapses where the mosaic is lost — a worn patch IS lower, and that
        # is what makes it read as wear rather than as a stain.
        height=np.clip(face * (1 - 0.7 * loss) * 0.8 + dust * 0.2, 0, 1))


# name, builder, FEATURES PER TILE.
#
# The third number is how many stones / planks / ripples span one repeat of the texture, and it
# is the bridge between an authored number and a sampled one. WorldTheme.RoadTiling means
# "cobbles per metre" and eight worlds are tuned in those units; the shader needs "tile repeats
# per metre". The conversion is RoadTiling / featuresPerTile, and it has to be done somewhere.
# Doing it here — and pinning it against Core in lint_unity_yaml.py — means a surface generated
# with a different cell count cannot silently change the size of every stone in a world.
SURFACES = [
    ("cobble", cobble, 9),
    ("planks", planks, 8),
    ("flagstone", flagstone, 4),
    ("dirt", dirt, 6),
    ("sand", sand, 20),
    ("snow", snow, 7),
    ("gravel", gravel, 22),
    ("mosaic", mosaic, 16),
]


# --- output ----------------------------------------------------------------

def normal_from_height(h, strength=3.4):
    """
    Sobel the height into a tangent-space normal. np.roll is what keeps it wrapping — a
    gradient computed with clamped edges would put a bright seam on every tile boundary.

    THE GREEN CHANNEL IS NOT NEGATED, AND THAT IS THE FIX RATHER THAN THE BUG. Row 0 of a
    PNG is the TOP row, and Unity's texture origin is the BOTTOM-left, so v decreases as
    this array's y index increases. Tangent space wants -dh/dv, and -dh/dv = +dh/dy here.
    Writing the textbook `-dy` would hand the shader a green channel of the wrong sign,
    every bump would light as a dent, and the surface would read as inside-out under the
    key light — which is the one thing normal maps are being added to fix.
    """
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    n = np.stack([-dx * strength, dy * strength, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def write_png(path, arr):
    """
    Minimal PNG writer. Pillow is not a dependency anywhere in this project's tooling and
    adding one for a file format that is nine lines of zlib would be a poor trade.
    """
    h, w, ch = arr.shape
    colour = {1: 0, 3: 2, 4: 6}[ch]
    raw = b"".join(b"\0" + arr[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, colour, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9))
           + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)
    return len(png)


def stable_guid(name):
    """
    A GUID derived from the asset path, so re-running this script produces the SAME one.

    Unity invents a random GUID for any asset with no .meta, and it invents a DIFFERENT one
    on every machine and every CI run. Nothing here references these textures by GUID today —
    they are loaded by name through Resources — but every other asset in this repository
    ships a committed .meta, and an uncommitted one means a fresh clone and a developer's
    working copy disagree about what the asset even is.
    """
    return hashlib.md5(("BattleRunner/Surfaces/" + name).encode()).hexdigest()


# The import settings that actually matter, and why. Everything omitted here Unity fills with
# its own default, and SurfaceImportSettings.cs re-asserts all of it from the editor as an
# AssetPostprocessor — this file is what a fresh clone starts from, that file is the guarantee.
#
#   sRGBTexture: 0   These are DATA, not pictures. R is a tone, G a mask, B a wetness and A a
#                    height; pushing them through a gamma curve on upload bends every one of
#                    those into a value the shader did not ask for. This is the single most
#                    consequential line in the block.
#   wrapU/V: 0       Repeat. The whole point is that they tile; clamp would stretch one row of
#                    pixels down four hundred metres of road.
#   filterMode: 2    Trilinear. The road is seen at a grazing angle out to the fog wall, which
#                    is the exact case where bilinear mip transitions show as visible bands.
#   aniso: 8         Same reason, and it is the difference between a road that has a surface at
#                    distance and one that turns to grey mush ten metres out.
META = """fileFormatVersion: 2
guid: %s
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 8
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Android
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: 54
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  spritePackingTag: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_meta(png_path):
    """Write <png>.meta if it is not already there. Never overwrites: a meta Unity has
    rewritten with real import settings is more correct than this template, not less."""
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return
    name = os.path.basename(png_path)
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(META % stable_guid(name))


def joint_coverage(face, mortar_width):
    """
    What fraction of the road the shader will paint with the world's MORTAR colour.

    Road.shader turns the face mask into a joint with smoothstep(w, w + 0.28, g), so this is
    not a property of the texture alone — it depends on the width the world authored. It is
    worth measuring because it is how the one real regression in this work happened: the first
    mosaic multiplied its face mask by a "half lost" field, a third of the road came out as
    joint, Throne of Dust deliberately puts GOLD in its seams, and the result was a road that
    was one third gold and measured worse than the flat slab it replaced.
    """
    t = np.clip((face - mortar_width) / 0.28, 0, 1)
    return float(((t * t * (3 - 2 * t)) < 0.5).mean())


def authored_worlds():
    """The eight worlds, read out of the C# rather than duplicated here."""
    path = os.path.join(REPO, "Assets", "Scripts", "Core", "World", "WorldThemes.cs")
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        src = f.read()
    out = []
    for block in src.split("new WorldTheme")[1:]:
        name = re.search(r'DisplayName = "([^"]+)"', block)
        surface = re.search(r"\bSurface = RoadSurfaces\.(\w+)", block)
        width = re.search(r"RoadMortarWidth = ([0-9.]+)f", block)
        if name and surface and width:
            out.append((name.group(1), surface.group(1).lower(), float(width.group(1))))
    return out


def stats(a):
    """
    Range, edge density, and whether the tile JOIN is discontinuous.

    The join test compares the gradient across the wrap against the 99th percentile of the
    interior gradients, NOT against their mean. On a cellular texture the mean is dominated by
    flat cell interiors, so measuring against it reports a seam on every voronoi when none of
    them seam — which is exactly the false alarm this replaced.
    """
    lum = a * 255.0
    p2, p98 = np.percentile(lum, [2, 98])
    edge = (np.abs(np.diff(lum, axis=1)).mean() + np.abs(np.diff(lum, axis=0)).mean()) / 2
    join = max(np.abs(a[:, 0] - a[:, -1]).mean(), np.abs(a[0] - a[-1]).mean())
    interior = max(np.percentile(np.abs(np.diff(a, axis=1)), 99),
                   np.percentile(np.abs(np.diff(a, axis=0)), 99), 1e-6)
    return p98 - p2, edge, join / interior


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", action="store_true", help="measure only, write nothing")
    args = ap.parse_args()

    print("%-11s %7s %7s %7s %9s %9s" % ("surface", "range", "edge", "join", "mask KB", "nrm KB"))
    total = 0
    for name, make, _features in SURFACES:
        s = make()
        mask = np.stack([s["tone"], s["face"], s["wet"], s["height"]], -1)
        mask8 = (np.clip(mask, 0, 1) * 255).astype(np.uint8)
        nrm8 = (np.clip(normal_from_height(s["height"]), 0, 1) * 255).astype(np.uint8)

        rng_, edge, seam = stats(s["tone"] * s["face"])
        if args.report:
            mk = len(zlib.compress(mask8.tobytes(), 9)) / 1024
            nk = len(zlib.compress(nrm8.tobytes(), 9)) / 1024
        else:
            os.makedirs(OUT, exist_ok=True)
            mask_path = os.path.join(OUT, name + "_mask.png")
            nrm_path = os.path.join(OUT, name + "_n.png")
            mk = write_png(mask_path, mask8) / 1024
            nk = write_png(nrm_path, nrm8) / 1024
            write_meta(mask_path)
            write_meta(nrm_path)
        total += mk + nk
        flag = "" if seam <= 1.0 else "  <-- SEAM"
        print("%-11s %7.0f %7.2f %7.2f %9.1f %9.1f%s" % (name, rng_, edge, seam, mk, nk, flag))

    print()
    faces = {name: make()["face"] for name, make, _ in SURFACES}
    worlds = authored_worlds()
    if worlds:
        print("%-22s %-11s %8s %8s" % ("world", "surface", "mortar", "as joint"))
        for world, surface, width in worlds:
            if surface not in faces:
                print("%-22s %-11s   <-- no such surface" % (world, surface))
                continue
            share = joint_coverage(faces[surface], width)
            flag = "  <-- the joint colour is most of the road" if share > 0.25 else ""
            print("%-22s %-11s %8.3f %7.0f%%%s" % (world, surface, width, 100 * share, flag))
        print()

    print("%d surfaces, %.2f MB on disk" % (len(SURFACES), total / 1024))
    print("for reference: the shipped procedural road measured range 19-42, edge 0.81-1.04")
    if not args.report:
        folder_meta = OUT + ".meta"
        if not os.path.exists(folder_meta):
            with open(folder_meta, "w", encoding="utf-8") as f:
                f.write(FOLDER_META % stable_guid("<folder>"))
        print("wrote " + os.path.relpath(OUT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main())
