# 14 — Surfaces: the first textures this project has ever had

## What was actually wrong

Not the palette. That was measured before anything was built, against the two photoreal
references supplied as the bar:

| | game frames | REF town | REF village |
|---|---|---|---|
| distinct colours | 87–108 | 82 | 81 |
| mean saturation | 0.40–0.74 | 0.56 | 0.34 |
| **edge density** | **0.81–1.04** | **1.21** | **8.25** |

**Colour and saturation already matched. The gap was high-frequency detail per pixel.** The
near road measured a p2–p98 luminance range of **19–42 out of 255**; the reference ground is
54–139. `Road.shader` was computing a brick bond, mortar, per-stone tone and two octaves of
grime *per pixel* and still landing at 2–5% contrast — which is why the road read as a flat
slab, which is what it was called.

A fragment shader can afford a handful of instructions per pixel. A texture is a lookup table
for arbitrarily expensive maths, and a voronoi cell diagram with per-cell tone and multi-octave
grime is exactly that kind of maths.

## Why textures were possible without touching a single mesh

`Road.shader` already computed `uv = positionWS.xz * _Tiling`. **That is a real UV.** Sampling
a texture with it needs no mesh UV channel, no importer, no change to any mesh in the project
— and no mesh here has ever had UVs. That is the whole unlock.

Normal mapping usually needs a mesh tangent frame, and no mesh here has one of those either.
But the road and the terrain band are horizontal, axis-aligned planes, so their tangent frame
is known at compile time — `T = (1,0,0)`, `B = (0,0,1)`, `N = (0,1,0)` — and `worldN =
(n.x, n.z, n.y)` falls straight out of it. The trick works only for a horizontal plane, which
is precisely what these two surfaces are; the blend is faded by the geometric normal's Y so the
stretched box's vertical sides are not shaded with a floor's normal map.

## What is in them — structure, not colour

Eight worlds author eight tuned road palettes, and baking colour would throw all of it away.
So `tooling/gen_surfaces.py` bakes a mask whose four channels are all **structure**:

| channel | is |
|---|---|
| R | surface tone — per-stone lightness with the grime folded in |
| G | face mask — 1 on a stone face, 0 in a joint or crack |
| B | wetness — where standing water and sheen collect |
| A | height — drives the normal map, and the cavity shading |

The shader tints R with the world's stone colour and the gaps with its mortar, so all eight
palettes survive and gain detail instead of being replaced by it. The macro grime stays
*computed*, for a second reason: a 256px texture repeating every few metres down a 400 m road
would otherwise show its tile, and noise an order of magnitude larger than the tile is what
breaks that up.

## No shader keywords, for a specific reason

The audit found the project uses **zero** shader keywords anywhere, and `Road.shader` is
already 128 forward variants from its fog and shadow `multi_compile`s alone. A six-way surface
keyword would take it to **768**, every one of which has to compile on a mid-tier Android
device. So eight worlds bind eight different **textures** into one variant, which costs
nothing. `WorldTheme.Surface` and `WorldTheme.GroundSurface` name them; `TrackController`
loads them once and caches them.

The road's surface is never the land's. The kerb is the one edge in the frame the eye is
guaranteed to find, and the same texture either side of it erases the road; a test pins it.

## Seams, and a measurement that was lying

Everything tiles: a road runs for a kilometre and the texture repeats every few metres, so a
seam is not a subtle artefact, it is a stripe across the whole level, once per tile, forever.
The value lattice tiles 2×2 before resampling so the interpolation kernel sees wrapped
neighbours; the worley sweeps a 3×3 neighbourhood with modular seeds; the normal map rolls
rather than clamps.

The first seam metric **reported a seam on every voronoi texture, and there were none.** It
compared the gradient across the tile join against the *mean* interior gradient, and on a
cellular texture the mean is dominated by flat cell interiors, so the join looks enormous
against it. Measured against the 99th percentile instead, seven of the eight were already
clean and exactly one — `rows`, the plank generator — genuinely seamed: 11 bands do not divide
256 evenly and a 0.02 skew breaks the x-wrap. Join error was **0.3422 against an interior p99
of 0.0017**. Eight bands and no skew fixed it.

## Two regressions the numbers caught

Neither would have thrown, crashed, or logged anything.

**The normal map's green channel was inverted.** Row 0 of a PNG is the *top* row and Unity's
texture origin is the *bottom* left, so `v` decreases as the array's `y` increases. Tangent
space wants `-dh/dv`, which here is `+dh/dy`. The textbook `-dy` would have handed the shader
the wrong sign, every bump would have lit as a dent, and the surface would have read
inside-out under the key light — the one thing normal maps were being added to fix.

**Throne of Dust measured worse than the road it replaced.** The mosaic generator multiplied
its face mask by a "half lost" field, so a third of the road came out as *joint* — and Throne
of Dust deliberately puts gold in its seams. The result was a road that was one third gold,
at a luminance range of **15** and an edge density of **0.66**, against a shipped baseline of
19–42 and 0.81–1.04. A worn tessera is still a tessera: it is duller and lower, not a seam.
Moving the loss into the tone and height channels and leaving the grout a grout fixed it.

Then the fix exposed the next one. Lifting that world's stone off the darkness floor put it
within **2%** of its own gold's luminance, so the gilding became invisible — a joint the same
value as the stone has a pattern only in hue, and the luminance structure the eye actually
reads is flat. The gold is now bright enough to be gold. `SurfaceTests` pins the invariant,
and deliberately does *not* pin "the joint is darker": a bright seam is a real look, an
equal-value seam is not.

## What it measures now

End to end, with each world's real palette, its real mortar width and its real tiling:

| world | surface | joint | range | edge |
|---|---|---|---|---|
| The Ashen Road | cobble | 8% | 63 | 3.62 |
| Gallows Mire | planks | 3% | 52 | 1.73 |
| The Sunken Crypt | flagstone | 7% | 49 | 1.33 |
| Ember Fields | dirt | 5% | 29 | 2.39 |
| The Bone Wastes | sand | 0% | 60 | 3.46 |
| The Frozen Reach | snow | 0% | 48 | 2.51 |
| The Blood Marsh | gravel | 13% | 36 | 3.99 |
| The Throne of Dust | mosaic | 8% | 39 | 2.29 |
| **shipped, for comparison** | procedural | — | **19–42** | **0.81–1.04** |

Every world is past the reference town's ground edge density of 1.21, at **1.5×–4.9×** what
shipped. Sand and snow measure 0% joint because they *have* no joints — a face mask that is
1.0 everywhere is the correct description of a snowfield, and the mortar colour then never
appears no matter what width the world authors.

## Numbers that had to be re-authored, and one that never was

`WorldTheme.RoadTiling` means surface features per metre, and all eight values had been
authored against a single procedural cobble grid where a feature was always a cobble. Sand
ripples and shale chips are not cobbles: **1.2 wind ripples per metre makes each ripple most
of a metre across.** Four worlds were re-tuned so that every road's tile repeat lands between
2 and 8 metres — close enough not to read as wallpaper, far enough not to alias on a surface
seen almost edge-on. A test pins the band.

`WorldTheme.RoadMortarWidth` was **declared and never once overridden.** All eight worlds sat
on the same `0.075`, so the joint pattern was byte-identical everywhere — a measurable part of
why the pavement "doesn't change" between rounds. Eight distinct values now, and a test that
fails if they collapse back.

## The couplings that had to be pinned

Core names eight textures and says how many features are in each; the Python decides what is
actually in them. Three things can drift and every one fails without an error message: a
**name** drifts and `Resources.Load` returns null, so the road silently reverts to the flat
slab; a **feature count** drifts and every stone in that world quietly changes size; the
**PNG** is not committed, which looks exactly like the first case. `tooling/lint_unity_yaml.py`
checks all three, in the same place it already pins the crowd's scale constants and the audio
clip table. Verified by breaking each one deliberately and watching it fail.

## Import settings, which this project had never needed before

There was not a single `.png` under `Assets/` before these, so there was no convention to
inherit and no `TextureImporter` meta anywhere to copy. `gen_surfaces.py` writes a `.meta`
template with a GUID derived from the file name, so a fresh clone and a working copy agree
about what the asset is. But a `.meta` is a serialized blob whose schema moves between Unity
versions, and a field got subtly wrong there fails silently — the texture still imports, still
renders, and is simply wrong. So `SurfaceImportSettings.cs` re-asserts all of it as an
`AssetPostprocessor`:

- **`sRGBTexture = false`** — the one that would actually break things. Unity's default for a
  `.png` is *on*, because the common case is a photograph. These are data: a tone, a mask, a
  wetness and a height, in a project that renders in **linear** colour space. A gamma decode
  on upload bends all four into numbers the shader never asked for, and on the normal map,
  where the channels are vector components, it decodes to normals that do not point where the
  height field says they should.
- **Repeat wrap**, or clamping stretches one row of pixels down the whole road.
- **Trilinear and aniso 8** — the road is seen almost edge-on out to the fog wall, which is
  exactly where bilinear mip transitions band and where, without anisotropy, the surface
  dissolves into flat grey about ten metres out. That is this entire piece of work undone by
  an import setting.
- **Default, not NormalMap**, even for the `_n` files: a NormalMap import swizzles into
  DXT5nm or its ASTC equivalent depending on the build target, so the shader would have to
  call `UnpackNormal` and hope the platform define matched. Decoding `tex.xyz * 2 - 1` by hand
  is one instruction and the same instruction on every device this ships to.
- **ASTC 6×6 on Android**, because the alpha channel is a height field and a format that drops
  or halves alpha would flatten the cavity shading. Sixteen 256² textures land near 1 MB.

## What this does not fix

The references are PC-GPU environments with hundreds of PBR textures, dynamic GI and
volumetrics. This is the ground, and only the ground. The composition problem — props
scattered evenly, everything past ~30 m collapsing into one flat fog silhouette, the verge
crushed to black — is a separate piece of work and is not addressed here.
