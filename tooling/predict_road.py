#!/usr/bin/env python3
"""Predict what the road LOOKS LIKE ON SCREEN, before building an APK.

    python3 tooling/predict_road.py                  # every world, current settings
    python3 tooling/predict_road.py --world 1        # one world
    python3 tooling/predict_road.py --rungs          # model the grid that used to be there
    python3 tooling/predict_road.py --png out.png    # write the predicted frame

WHY THIS EXISTS — and it is the most important comment in this file.

The last road pass measured the generated cobble texture and reported a luminance range of 178.
It arrives on screen as 21. Every number reported was true of the INPUT and irrelevant to the
OUTPUT, and the road shipped flat for a second time. `gen_surfaces.py --report` measures a file;
this measures a frame. They are different questions and only the second one is the one the
player asks.

Between the texture and the eye sit: world-space tiling, perspective foreshortening (a 4 cm
cobble is a third of a pixel at 30 m), a normal map under a directional light, a fog lerp, four
emissive lane decals drawn on top, exposure, contrast, saturation, a shadows/midtones/highlights
split, Neutral tonemapping, bloom, and an sRGB encode. Each one of those compresses. The texture
does not have to survive the first; it has to survive all thirteen.

WHAT THIS IS NOT. It is not a renderer and it will not match a device pixel for pixel. It is a
model of the value chain with ONE fitted parameter, and its job is to answer comparative
questions — "does deleting the rungs raise the stone's contrast, and by how much?" — which is
exactly the question that was got wrong.

CALIBRATION, STATED HONESTLY. The device frames themselves are screenshots in a conversation,
not files in this repo, so this cannot be fitted per-pixel against them. What IS recorded, in
docs/10-look.md and in the plan, is their measured aggregates:

    shipped baseline (before the generated surfaces)   range 19-42   edge 0.81-1.04
    v0.19.0 as measured on device, grid included       range 25-29   edge 0.53-0.84
    v0.19.0 as measured on device, stone only          range 21-24   edge 0.30-0.61

`--calibrate` fits the single free parameter — SCREEN_BLUR, the combined softening of texture
filtering, the device's own downscale into a screenshot, and bloom's lowest mip — so that the
model reproduces the middle row under v0.19.0's settings. Everything else in the file is read
from the project: the shader is transcribed from Road.shader, the grade from EnvironmentLook.cs,
the palette from WorldThemes.cs. Nothing here is a free knob that can be turned until the answer
is pleasing, which is the failure mode this file exists to prevent.

A PREDICTION IS STILL A PREDICTION. The next round of device screenshots is the arbiter. If they
disagree with this model, the model is wrong and gets fixed — it does not get to overrule them.
"""

import argparse
import math
import os
import re
import sys

import numpy as np
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SURFACES = os.path.join(REPO, "Assets", "Resources", "Surfaces")
THEMES = os.path.join(REPO, "Assets", "Scripts", "Core", "World", "WorldThemes.cs")
SURFACE_CS = os.path.join(REPO, "Assets", "Scripts", "Core", "World", "RoadSurface.cs")
TRACK_CS = os.path.join(REPO, "Assets", "Scripts", "Gameplay", "Track", "TrackController.cs")

# --- the one fitted parameter -------------------------------------------------
#
# Screen-space softening in pixels, standard deviation. Fitted by --calibrate so the model
# reproduces the stone-only EDGE DENSITY recorded from the v0.19.0 device frames.
#
# IT DOES NOT FIT BOTH STATISTICS, AND THAT IS RECORDED HERE RATHER THAN HIDDEN. At the
# fitted blur the model's edge density lands on the device's 0.30-0.61, and its luminance
# RANGE lands at 38.8 against a device 21-24 — about 1.7x too wide. Blur is a local operator
# and the range is carried by the metres-wide grime term, so no value of this parameter can
# move both. The gap is real model error: most likely the device's own display transform and
# the screenshot's encode compress the low end further than the transcribed grade does.
#
# 6.88 px is also far more softening than texture filtering plus a screenshot downscale can
# physically account for, so this parameter is absorbing model error as well as blur. Saying
# so is the point: it is why the absolute numbers below are NOT trustworthy and why the tool
# reports RATIOS against the v0.19.0 baseline instead. A ratio survives a systematic error
# that a level does not, and "does this change raise the stone's contrast, and by how much"
# is the only question that was actually got wrong.
SCREEN_BLUR = 6.88

# What the model itself measures for the v0.19.0 tree (rungs present, the old
# RoadStoneVariation values, no _RimUpMask, 0.10 m lane lines), at the blur above. Recorded
# so a prediction can be expressed as a change against the configuration whose true on-device
# numbers ARE known. Regenerate from a worktree at that commit with --baseline.
MODEL_V19 = dict(range=38.8, edge=0.46, stone_range=38.8, stone_edge=0.45)

# What those device frames actually measured. The two together turn a model ratio into a
# predicted device number.
DEVICE_V19 = dict(range=(25.0, 29.0), edge=(0.53, 0.84),
                  stone_range=(21.0, 24.0), stone_edge=(0.30, 0.61))

# Portrait, and the aspect is what decides how much foreshortened road is in frame at all.
WIDTH, HEIGHT = 540, 1200
FOV_Y = 60.0            # CameraRig.BaseFieldOfView
CAM_HEIGHT = 5.5        # CameraRig.TargetPosition
CAM_SETBACK = 10.0      # CameraRig.SetbackMeters
LOOK_HEIGHT = 1.5       # CameraRig.LookTarget
LOOK_AHEAD = 10.0

LANE_WIDTH = 2.2
ROAD_HALF = LANE_WIDTH * 1.5            # CrowdMath.RoadHalfWidth
NEAR_BAND = (12.0, 35.0)                # metres from the camera: the "near road" that was measured


# =============================================================================
# reading the project
# =============================================================================

def _rgb(text):
    m = re.search(r"new Rgb\(([-0-9.]+)f,\s*([-0-9.]+)f,\s*([-0-9.]+)f\)", text)
    if not m:
        raise ValueError("not an Rgb: " + text[:60])
    return np.array([float(m.group(1)), float(m.group(2)), float(m.group(3))])


def read_worlds():
    """Parse the eight authored WorldThemes. Regex, deliberately: a C# parser for eight
    object initialisers would be more code than the model it feeds."""
    src = open(THEMES).read()
    blocks = re.split(r"new WorldTheme\s*\n?\s*\{", src)[1:]
    worlds = []
    for block in blocks:
        # Cut at the closing brace of the initialiser (the first "}," at the block's indent).
        end = block.find("\n            },")
        body = block[:end] if end > 0 else block

        def col(name):
            m = re.search(name + r"\s*=\s*(new Rgb\([^)]*\))", body)
            return _rgb(m.group(1)) if m else None

        def num(name, default=None):
            m = re.search(name + r"\s*=\s*([-0-9.]+)f", body)
            if m:
                return float(m.group(1))
            if default is None:
                raise ValueError("missing " + name)
            return default

        name = re.search(r'DisplayName\s*=\s*"([^"]+)"', body).group(1)
        surface = re.search(r"Surface\s*=\s*RoadSurfaces\.(\w+)", body)
        worlds.append(dict(
            name=name,
            stone=col("RoadStone"), mortar=col("RoadMortar"), damp=col("RoadDamp"),
            accent=col("Accent"),
            sky_zenith=col("SkyZenith"), sky_horizon=col("SkyHorizon"), sky_glow=col("SkyGlow"),
            fog=col("Fog"),
            tiling=num("RoadTiling"), wetness=num("RoadWetness"), gloss=num("RoadGloss"),
            variation=num("RoadStoneVariation"),
            grime=num("RoadGrimeContrast", 0.42),
            mortar_width=num("RoadMortarWidth", 0.075),
            light_intensity=num("LightIntensity", 1.1),
            light_pitch=num("LightPitch", 32.0), light_yaw=num("LightYaw", 250.0),
            light_color=col("LightColor"),
            fog_start=num("FogStart", 70.0), fog_end=num("FogEnd", 170.0),
            surface=(surface.group(1).lower() if surface else "cobble"),
        ))
    if len(worlds) != 8:
        raise SystemExit(f"expected 8 worlds, parsed {len(worlds)}")
    return worlds


def surface_features(kind):
    """FeaturesPerTile from RoadSurface.cs — the metres-to-UV conversion the shader relies on."""
    src = open(SURFACE_CS).read()
    for m in re.finditer(r'new RoadSurface\("([^"]+)",\s*([0-9.]+)f', src):
        if m.group(1).lower() == kind:
            return float(m.group(2))
    return 6.0


def marking_emission(stone, accent):
    """WorldTheme.MarkingEmission = Lerp(RoadStone * 1.5, Accent, 0.25)."""
    return (stone * 1.5) * 0.75 + accent * 0.25


def marking_base(stone):
    return stone * 0.55


def read_lane_line_width():
    """Read the authored lane-line width straight out of TrackController rather than
    duplicating it here, so narrowing the lines updates the prediction automatically."""
    src = open(TRACK_CS).read()
    m = re.search(r"new Vector3\(([0-9.]+)f, 0\.02f, length\), _markingMaterial\)", src)
    return float(m.group(1)) if m else 0.10


def rungs_present():
    return 'SpawnStatic("Rung"' in open(TRACK_CS).read()


# =============================================================================
# geometry — where on the road each screen pixel lands
# =============================================================================

def ground_positions():
    """Cast one ray per pixel and intersect y = 0.

    This is the step the texture-file measurement skipped entirely, and it is where most of
    the detail dies: at 30 m a 4 cm cobble covers about a third of a pixel, so the whole
    per-stone term has already averaged to its mean before any of the grade runs.
    """
    cam = np.array([0.0, CAM_HEIGHT, -CAM_SETBACK])
    target = np.array([0.0, LOOK_HEIGHT, LOOK_AHEAD])
    fwd = target - cam
    fwd /= np.linalg.norm(fwd)
    right = np.cross(np.array([0.0, 1.0, 0.0]), fwd)
    right /= np.linalg.norm(right)
    up = np.cross(fwd, right)

    ty = math.tan(math.radians(FOV_Y) * 0.5)
    tx = ty * (WIDTH / HEIGHT)
    # Pixel centres, +y up the screen.
    sx = (np.arange(WIDTH) + 0.5) / WIDTH * 2.0 - 1.0
    sy = 1.0 - (np.arange(HEIGHT) + 0.5) / HEIGHT * 2.0
    gx, gy = np.meshgrid(sx, sy)

    d = (fwd[None, None, :]
         + right[None, None, :] * (gx * tx)[:, :, None]
         + up[None, None, :] * (gy * ty)[:, :, None])
    d /= np.linalg.norm(d, axis=2, keepdims=True)

    with np.errstate(divide="ignore", invalid="ignore"):
        t = -cam[1] / d[:, :, 1]
    hit = (d[:, :, 1] < 0) & (t > 0) & np.isfinite(t)
    t = np.where(hit, t, 0.0)

    world = cam[None, None, :] + d * t[:, :, None]
    return world, d, t, hit


# =============================================================================
# the shader, transcribed from Assets/Resources/Road.shader
# =============================================================================

def load_surface(kind):
    mask = np.asarray(Image.open(os.path.join(SURFACES, f"{kind}_mask.png")).convert("RGBA"),
                      dtype=np.float32) / 255.0
    nrm = np.asarray(Image.open(os.path.join(SURFACES, f"{kind}_n.png")).convert("RGB"),
                     dtype=np.float32) / 255.0
    return mask, nrm


def sample(tex, u, v):
    """Bilinear, wrapping. The textures are seamless, so wrapping is the correct edge rule."""
    h, w = tex.shape[:2]
    x = (u * w) % w
    y = (v * h) % h
    x0 = np.floor(x).astype(np.int32)
    y0 = np.floor(y).astype(np.int32)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    x1 = (x0 + 1) % w
    y1 = (y0 + 1) % h
    return ((tex[y0, x0] * (1 - fx) + tex[y0, x1] * fx) * (1 - fy)
            + (tex[y1, x0] * (1 - fx) + tex[y1, x1] * fx) * fy)


def value_noise(x, z):
    """The shader's ValueNoise, close enough for a macro term: smooth bilinear hash noise."""
    xi, zi = np.floor(x), np.floor(z)
    fx, fz = x - xi, z - zi
    fx = fx * fx * (3 - 2 * fx)
    fz = fz * fz * (3 - 2 * fz)

    def h(a, b):
        n = np.sin(a * 12.9898 + b * 78.233) * 43758.5453
        return n - np.floor(n)

    return ((h(xi, zi) * (1 - fx) + h(xi + 1, zi) * fx) * (1 - fz)
            + (h(xi, zi + 1) * (1 - fx) + h(xi + 1, zi + 1) * fx) * fz)


def shade_road(world, view_dir, dist, w, footprint):
    """Road.shader's fragment, in linear space, before fog."""
    mask, nrm = load_surface(w["surface"])
    tiling = w["tiling"] / max(1e-3, surface_features(w["surface"]))
    tiling = min(max(tiling, 0.01), 4.0)            # RoadSurface.TileRepeatsPerMetre clamp

    u = world[:, :, 0] * tiling
    v = world[:, :, 2] * tiling

    # MINIFICATION. The GPU picks a mip from the UV derivative; a point sample here would
    # predict per-stone detail at 60 m that no device has ever drawn. Blending the sample
    # toward the texture's own mean by how many texels fall inside one pixel is the cheapest
    # honest model of that, and without it every number this file prints is too optimistic.
    texels = footprint * tiling * mask.shape[0]
    blend = np.clip(1.0 / np.maximum(texels, 1.0), 0.0, 1.0)[..., None]

    surf = sample(mask, u, v)
    surf = surf * blend + mask.reshape(-1, 4).mean(axis=0)[None, None, :] * (1 - blend)
    nT = sample(nrm, u, v) * 2.0 - 1.0
    nT = nT * blend + np.array([0.0, 0.0, 1.0])[None, None, :] * (1 - blend)

    face = np.clip((surf[:, :, 1] - w["mortar_width"]) / 0.28, 0, 1)
    face = face * face * (3 - 2 * face)

    tone = (surf[:, :, 0] - 0.5) * w["variation"] * 2.0
    stone = np.clip(w["stone"][None, None, :] * (1.0 + tone[..., None]), 0, 1)
    albedo = w["mortar"][None, None, :] * (1 - face[..., None]) + stone * face[..., None]

    grime = (value_noise(world[:, :, 0] * 0.33, world[:, :, 2] * 0.33) * 0.6
             + value_noise(world[:, :, 0] * 0.10, world[:, :, 2] * 0.10) * 0.4)
    g = w["grime"]
    albedo = albedo * ((1.0 - g) + ((1.0 + g * 0.62) - (1.0 - g)) * grime)[..., None]

    cavity = 0.45                                    # Road.mat _Cavity
    albedo = albedo * (1.0 + (np.clip(surf[:, :, 3] * 0.75 + 0.4, 0, 1) - 1.0) * cavity)[..., None]

    # Tangent frame is constant on a horizontal plane: worldN = (n.x, n.z, n.y).
    bumped = np.stack([nT[:, :, 0], nT[:, :, 2], nT[:, :, 1]], axis=2)
    bumped /= np.linalg.norm(bumped, axis=2, keepdims=True)
    geo = np.array([0.0, 1.0, 0.0])
    n = geo[None, None, :] + (bumped - geo[None, None, :]) * 1.0    # _NormalStrength = 1
    n /= np.linalg.norm(n, axis=2, keepdims=True)

    pitch = math.radians(w["light_pitch"])
    yaw = math.radians(w["light_yaw"])
    # Unity's convention: the light DIRECTION handed to the shader points at the source.
    ldir = -np.array([math.cos(pitch) * math.sin(yaw),
                      -math.sin(pitch),
                      math.cos(pitch) * math.cos(yaw)])
    ldir /= np.linalg.norm(ldir)

    lambert = np.clip((n * ldir[None, None, :]).sum(axis=2), 0, 1) * 0.6 + 0.4
    light = w["light_color"] * w["light_intensity"] if w["light_color"] is not None \
        else np.array([0.75, 0.78, 0.95]) * w["light_intensity"]

    # SampleSH under Trilight, approximated by the up-facing road taking mostly the sky term.
    amb_sky = w["sky_zenith"] * 5.5
    amb_eq = (w["sky_horizon"] * 1.6) * 0.65 + w["accent"] * 0.35
    ny = np.clip(n[:, :, 1], 0, 1)[..., None]
    ambient = amb_sky[None, None, :] * ny + amb_eq[None, None, :] * (1 - ny)

    shadow = 1.0            # open road; the rails' shadows are a separate, local term
    color = albedo * (light[None, None, :] * lambert[..., None] * shadow + ambient)

    half = ldir[None, None, :] - view_dir
    half /= np.linalg.norm(half, axis=2, keepdims=True)
    spec = np.clip((n * half).sum(axis=2), 0, 1) ** w["gloss"]
    color = color + (w["damp"][None, None, :]
                     * (spec * w["wetness"] * surf[:, :, 2] * grime)[..., None])
    return color


def marking_up_mask():
    return 'SetFloatSafe("_RimUpMask", 1f)' in open(TRACK_CS).read()


def shade_marking(w, emission, base):
    """A road decal, flat-shaded — its normal points straight up so it has no relief.

    _RimUpMask is the whole point of this function: with the mask at 0 the rim term is
    evaluated at ~80 degrees off the view axis (which is all a 2 cm decal is ever seen at),
    where even the power-4 lobe reads about 0.64. With it at 1 the term is killed on
    up-facing normals and only the view-independent _EmissionFlat survives.
    """
    rim = 0.0 if marking_up_mask() else 0.64 * 0.35       # _RimStrength 0.35
    flat = 0.10                                  # _EmissionFlat
    amb = w["sky_zenith"] * 5.5
    return base * amb + emission * (rim + flat)


# =============================================================================
# the post stack, transcribed from EnvironmentLook.cs
# =============================================================================

def neutral_tonemap(c):
    """URP's Neutral tonemapper (the Tonemapping.mode this project selects)."""
    a, b, cc, d, e, f = 0.20, 0.29, 0.24, 0.272, 0.02, 0.3
    white = 5.3

    def curve(x):
        return ((x * (a * x + cc * b) + d * e) / (x * (a * x + b) + d * f)) - e / f

    return curve(c) / curve(white)


def grade(c, w):
    """Exposure, filter, contrast, saturation and the shadows/midtones/highlights split."""
    c = c * (2.0 ** 0.20)                                       # postExposure

    accent = w["accent"]
    norm = accent / max(accent.max(), 1e-5)
    filt = norm + (1.0 - norm) * 0.82                           # GradeFilter
    c = c * filt[None, None, :]

    lum = (c * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2, keepdims=True)
    chroma = float(accent.max() - accent.min()) / max(float(accent.max()), 1e-5)
    sat = 2.0 + 12.0 * chroma                                   # GradeSaturation
    c = lum + (c - lum) * (1.0 + sat / 100.0)

    c = neutral_tonemap(np.maximum(c, 0.0))

    # Contrast is applied around ACEScc mid-grey in URP; on an already-tonemapped value the
    # 0.4135884 pivot is the right stand-in and the sign and slope are what matter here.
    c = np.clip(0.4135884 + (c - 0.4135884) * (1.0 + 22.0 / 100.0), 0.0, 1.0)

    # ShadowsMidtonesHighlights: shadows take the sky, highlights the ember band.
    zen = w["sky_zenith"]
    zen = zen / max(zen.max(), 1e-5)
    shadows = zen + (1.0 - zen) * 0.72
    shadows = shadows / max(shadows.mean(), 1e-5)
    l = np.clip((c * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2, keepdims=True), 0, 1)
    ws = np.clip(1.0 - l * 2.2, 0, 1)
    c = c * (1.0 + (shadows[None, None, :] - 1.0) * ws)
    return np.clip(c, 0.0, 1.0)


def to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def blur(img, sigma):
    """Separable Gaussian. Small kernel, and it runs on the encoded frame because that is
    where the device's own downscale happens."""
    if sigma <= 0:
        return img
    r = max(1, int(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    out = img
    for axis in (0, 1):
        pad = [(0, 0)] * img.ndim
        pad[axis] = (r, r)
        p = np.pad(out, pad, mode="edge")
        acc = np.zeros_like(out)
        for i, kw in enumerate(k):
            sl = [slice(None)] * img.ndim
            sl[axis] = slice(i, i + out.shape[axis])
            acc = acc + p[tuple(sl)] * kw
        out = acc
    return out


# =============================================================================
# rendering one world
# =============================================================================

def render(w, rungs=None, blur_sigma=None):
    if rungs is None:
        rungs = rungs_present()
    if blur_sigma is None:
        blur_sigma = SCREEN_BLUR

    world, d, t, hit = ground_positions()
    dist = np.where(hit, t, 1e6)

    # One pixel's footprint on the ground, in metres. This is what decides how much of the
    # texture survives, and it is the term the file-based measurement had no way to see.
    px = math.tan(math.radians(FOV_Y) * 0.5) * 2.0 / HEIGHT
    slope = np.clip(-d[:, :, 1], 1e-3, 1.0)
    footprint = np.where(hit, dist * px / slope, 1.0)

    color = shade_road(world, d, dist, w, footprint)

    on_road = hit & (np.abs(world[:, :, 0]) <= ROAD_HALF)

    # The decals, drawn on top exactly as the scene does.
    lane_w = read_lane_line_width()
    emission = marking_emission(w["stone"], w["accent"])
    base = marking_base(w["stone"])
    deco = shade_marking(w, emission, base)

    x = world[:, :, 0]
    z = world[:, :, 2]
    marks = np.zeros(x.shape, dtype=bool)
    for edge in (-1, 1):
        for cx in (edge * LANE_WIDTH * 0.5, edge * ROAD_HALF):
            marks |= np.abs(x - cx) <= np.maximum(lane_w, footprint) * 0.5
    if rungs:
        marks |= ((z % 6.0) <= np.maximum(0.35, footprint)) & (np.abs(x) <= ROAD_HALF)
    marks &= on_road

    color = np.where(marks[..., None], deco[None, None, :], color)

    # Fog, per-pixel — the shader recomputes it rather than interpolating the strip's corners.
    fog_t = np.clip((dist - w["fog_start"]) / max(1e-3, w["fog_end"] - w["fog_start"]), 0, 1)
    color = color * (1 - fog_t[..., None]) + w["fog"][None, None, :] * fog_t[..., None]

    frame = to_srgb(grade(color, w))
    frame = blur(frame, blur_sigma)
    return frame, on_road, marks, dist


def measure(frame, mask):
    """p98 - p2 of luminance, and mean Sobel magnitude, both in 0-255 — the same two numbers
    the device frames were measured with, so the figures are comparable."""
    lum = (frame * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2) * 255.0
    sel = lum[mask]
    if sel.size < 64:
        return 0.0, 0.0
    rng = float(np.percentile(sel, 98) - np.percentile(sel, 2))

    gy, gx = np.gradient(lum)
    mag = np.hypot(gx, gy)
    # Interior only: the road's own silhouette against the verge is a huge edge and has
    # nothing to do with whether the SURFACE has detail. Measuring it was how a flat road
    # once scored well.
    inner = mask.copy()
    inner[:, :-1] &= mask[:, 1:]
    inner[:, 1:] &= mask[:, :-1]
    inner[:-1] &= mask[1:]
    inner[1:] &= mask[:-1]
    return rng, float(mag[inner].mean()) if inner.any() else 0.0


def near_mask(on_road, dist, marks=None, exclude_marks=False):
    m = on_road & (dist >= NEAR_BAND[0]) & (dist <= NEAR_BAND[1])
    if exclude_marks and marks is not None:
        m &= ~marks
    return m


# =============================================================================
# entry points
# =============================================================================

def calibrate(worlds):
    """Fit SCREEN_BLUR so the model reproduces the recorded v0.19.0 device aggregates.

    Run this ONLY against v0.19.0's settings (rungs present, the old variation values) — from
    a worktree at that commit. Run against today's tree it would happily fit the blur to hide
    an improvement, which is the same class of error this whole file exists to stop.

    Edge density falls monotonically with blur, so this bisects rather than sweeps.
    """
    target = sum(DEVICE_V19["stone_edge"]) / 2

    def mean_edge(sigma):
        edges, ranges = [], []
        for w in worlds:
            frame, on_road, marks, dist = render(w, rungs=True, blur_sigma=float(sigma))
            r, e = measure(frame, near_mask(on_road, dist, marks, exclude_marks=True))
            edges.append(e)
            ranges.append(r)
        return float(np.mean(edges)), float(np.mean(ranges))

    lo, hi = 0.4, 12.0
    for _ in range(11):
        mid = (lo + hi) / 2
        e, r = mean_edge(mid)
        print(f"  sigma {mid:5.2f} -> stone edge {e:5.2f}  range {r:5.1f}")
        if e > target:
            lo = mid
        else:
            hi = mid
    sigma = (lo + hi) / 2
    e, r = mean_edge(sigma)
    print(f"\nfitted SCREEN_BLUR = {sigma:.2f}")
    print(f"  model at that blur: stone edge {e:.2f}, range {r:.1f}")
    print(f"  device recorded:    stone edge "
          f"{DEVICE_V19['stone_edge'][0]:.2f}-{DEVICE_V19['stone_edge'][1]:.2f}, range "
          f"{DEVICE_V19['stone_range'][0]:.0f}-{DEVICE_V19['stone_range'][1]:.0f}")
    return sigma


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--world", type=int, default=None, help="0-7, default all")
    ap.add_argument("--rungs", action="store_true", help="force the speed-rung grid on")
    ap.add_argument("--calibrate", action="store_true", help="refit SCREEN_BLUR (v0.19.0 tree only)")
    ap.add_argument("--baseline", action="store_true",
                    help="print MODEL_V19 for the current tree (run from a v0.19.0 worktree)")
    ap.add_argument("--png", default=None, help="write the predicted frame of --world here")
    args = ap.parse_args()

    worlds = read_worlds()
    if args.calibrate:
        calibrate(worlds)
        return 0

    chosen = worlds if args.world is None else [worlds[args.world]]
    rungs = True if args.rungs else rungs_present()

    print(f"grid: {'rungs + 4 lane lines' if rungs else '4 lane lines only'}"
          f"   lane line {read_lane_line_width():.2f} m"
          f"   rim up-mask {'on' if marking_up_mask() else 'OFF'}"
          f"   blur {SCREEN_BLUR}")
    print()
    print(f"{'world':22} {'range':>7} {'edge':>6}   {'stone only':>10} {'edge':>6}")

    rows = []
    for w in chosen:
        frame, on_road, marks, dist = render(w, rungs=rungs)
        r1, e1 = measure(frame, near_mask(on_road, dist))
        r2, e2 = measure(frame, near_mask(on_road, dist, marks, exclude_marks=True))
        rows.append((r1, e1, r2, e2))
        print(f"{w['name'][:22]:22} {r1:7.1f} {e1:6.2f}   {r2:10.1f} {e2:6.2f}")
        if args.png and args.world is not None:
            Image.fromarray((np.clip(frame, 0, 1) * 255).astype(np.uint8)).save(args.png)
            print(f"wrote {args.png}")

    m = np.array(rows).mean(axis=0)
    print(f"{'MEAN':22} {m[0]:7.1f} {m[1]:6.2f}   {m[2]:10.1f} {m[3]:6.2f}")

    if args.baseline:
        print(f"\nMODEL_V19 = dict(range={m[0]:.1f}, edge={m[1]:.2f}, "
              f"stone_range={m[2]:.1f}, stone_edge={m[3]:.2f})")
        return 0

    # THE ONLY NUMBERS WORTH QUOTING. The model has a known systematic error in level (see
    # SCREEN_BLUR), so a prediction is expressed as the model's change against the v0.19.0
    # configuration, applied to what that configuration actually measured on device.
    print("\npredicted on device, as a change against v0.19.0's measured frames")
    print("  (model ratio x the recorded device number; the assumption is that the model's")
    print("   level error is a constant factor, which the next screenshots will test)")
    labels = [("all road", "range", 0, "range"), ("all road", "edge ", 1, "edge"),
              ("stone only", "range", 2, "stone_range"), ("stone only", "edge ", 3, "stone_edge")]
    for zone, stat, i, key in labels:
        ratio = m[i] / max(MODEL_V19[key], 1e-6)
        lo, hi = DEVICE_V19[key]
        fmt = "{:5.2f}" if "edge" in key else "{:5.1f}"
        print(f"  {zone:11} {stat}  x{ratio:5.2f}   "
              + (f"{fmt}-{fmt}".format(lo, hi) + "  ->  "
                 + f"{fmt}-{fmt}".format(lo * ratio, hi * ratio)))

    print("\nfor reference, the shipped pre-surfaces baseline measured range 19-42, edge 0.81-1.04")
    return 0


if __name__ == "__main__":
    sys.exit(main())
