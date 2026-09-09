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

---

# Composition: settlements, and the roadside that was a hole

The surfaces above are the ground. This is the arrangement of everything standing on it, and
it was the other half of the same complaint.

## Even scatter is what texture looks like, not what a place looks like

Scenery was placed by walking Z and dropping a piece every so many metres, jittered, on both
sides, for the length of the level. That is a uniform field. **A village is buildings touching
each other with empty ground between clusters**, and the negative space is what makes the
cluster read as a settlement rather than as density. Held against the two references,
everything past about thirty metres collapsed into one flat silhouette, and this is why.

`Core/World/Settlement.cs` is the layout arithmetic — engine-free, so whether a layout actually
leaves empty ground is a thing a test can answer. Each side of the road walks its own sequence
of hamlets, and about a third of them are **paired across the road** so a street happens on
purpose rather than as the only thing that can happen.

Three things were got wrong first and fixed by simulation rather than by looking:

- **Alternating sides down one sequence** halved the count per verge. A 400 m level came out
  with four hamlets and **79% open country** — not clustered, just empty.
- **`GapRadii = 1.35`** then swung it the other way: 9–14 hamlets and only **31% open**, which
  is a continuous village with gaps in it. At **2.6** it is 9–14 hamlets and **40–47% open** —
  about half the road is empty ground, which is what makes the other half read as a place.
- **Rejection sampling delivers `step × mean density`**, and the mean over a half-empty level
  is about a half, so the clustered field came out with 30–40% *fewer* pieces than the even
  scatter it replaced. That is a thinner world, not a differently arranged one. Oversampling
  by 2.2 lands the total within 1% of the old count.

Measured over all eight worlds, **the busiest quarter of the road now holds 42–54% of the
field pieces**, against the 25% an even scatter puts there by definition — so the instance
budget is unchanged and roughly twice as concentrated.

Buildings inside a hamlet share its orientation, jittered. Individually random yaws are the
other half of why a cluster still reads as scatter.

### The verge does not cluster

A hedgerow, a fence line and the stones on the shoulder run the length of a road whether or
not there is a village there. Clustering the one zone the player passes at arm's length would
leave long stretches of bare kerb, which is the opposite of the problem being fixed.

### The gap ratio is the invariant; the world chooses the scale

The obvious way to guarantee empty ground is to floor the spacing at `Spacing(someFixedRadius)`.
That **pinned seven of the eight worlds to the same 110 m** and silently discarded the spacing
each of them authors. Deriving the *radius* from the authored spacing instead honours the
number exactly and still guarantees the gap, because the gap is expressed as a multiple of the
radius rather than as metres. Worlds now run 82–120 m apart with hamlets 17.8–26.1 m across.

## Landmarks stood at the fog wall because of the wrong clamp

They were placed on a spacing of their own while the field pieces walked theirs, so a castle
and the carts and fences around it agreed about nothing. They now stand **inside** the
settlements — one anchor structure, sometimes a second outbuilding, never three.

And they were clamped so their **centre** stayed outside a 26 m line. A castle keep's footprint
is 11 m at landmark scale, so it could never stand closer than 37 m, and in a world that fogs
out at 105 m the largest structures in the game were silhouettes. The constraint that actually
matters is that a building must not overhang the road, and that is a constraint on its **edge**.
Clamping the edge to clear 8.5 m puts the same keep at **19 m**, where it reads as a building
the player is running past.

## The roadside was a hole in the frame

45–70% of a game frame sat below 12% luminance, against 36–43% for the references. The verge is
a large share of that: the ten procedural props are painted `PropStone` outright and every
imported verge piece is dragged 60–78% of the way toward it — and **three worlds authored a
`PropStone` below 0.17 luma**. The road has carried exactly this invariant at 0.12 since the
lighting pass; the roadside never did.

`WorldTheme.MinVergeLuma` is that floor at 0.19, applied through `VergeStone`. It is
deliberately **not** a cap on `VergeTint`: "grim verge, bright landmarks" is the right
direction and desaturating the roadside toward the world's own stone is how it is expressed.
What was wrong is that the stone being aimed at was nearly black, so the same move also crushed
it. The lift **scales** rather than adding a constant, so the hue survives — adding to all
three channels raises luminance and desaturates at once, which turns a world's signature colour
grey. Blood Marsh's prop stone is red before and after. Five worlds are untouched; the three
that needed it move by ×1.14 to ×1.32.

## Two knobs that existed and were never written

- **`Terrain._Gloss`** was in the material and no code ever set it, so ice and dry ash caught
  the key light identically in all eight worlds. Now themed: 4 for dry bone sand, 28 for the
  Frozen Reach.
- **`WhiteBalance` was not in the post stack at all.** It is a different operator from the
  colour filter already there: the filter *multiplies* the frame by a colour, so everything
  tints toward that colour, while white balance rotates the white *point*, so the road and the
  fog and the props move together and the frame reads as being lit differently rather than as
  having a gel over it. It folds into the grading LUT, so a per-world temperature is the
  cheapest difference between two rounds that exists — **zero cost per pixel**.

The rest of what the stack is missing is declined on cost rather than by oversight:
`DepthOfField` is a second full-screen pass on a fill-rate-bound mobile renderer, `FilmGrain` is
a texture read per pixel, and `SplitToning` would duplicate what `ShadowsMidtonesHighlights`
already does.
