# 12 — Scenery: the land, the grade, and why eight worlds still looked like one

## The complaint, measured

After eight worlds shipped, the verdict was: *"the difference between round 1 and 3 after
killing the first boss is still too small — minor colors only, I would expect a dramatic
change in the scenery and to see castles, farms that I go by."*

Sampling the device screenshots confirms the palettes really do change. Round 1-1 (The Ashen
Road) has a horizon at `(95, 66, 68)` and a road at `(30, 30, 42)`; round 2-1 (Gallows Mire)
has a horizon at `(66, 88, 50)` and a road at `(3, 8, 4)`. That is a genuine brown-to-green
swing.

And the complaint is still correct, because colour was the *only* thing that changed. Read
out of `WorldThemes.cs` rather than eyeballed:

| | Ashen Road | Gallows Mire |
|---|---|---|
| Prop meshes | Gravestone, DeadTree, BrokenColumn | DeadTree, HangingCage, Stump, **Gravestone** |
| Prop colour | one grey `(0.28, 0.27, 0.26)` | one grey `(0.20, 0.22, 0.17)` |
| Placement bands, scale range, tilt, yaw, density curve, draw path | — | **identical** |

Three structural causes, none of them fixable by authoring more colours.

### 1. There was no ground

`TrackController.SpawnGroundStrip` derived every dimension from the lane pitch:

```
roadHalf          = laneWidth * 1.5              = 3.300
shoulder          = laneWidth * 0.14             = 0.308
railCentre        = 3.300 + 0.308 + 0.15         = 3.758
groundHalf        = 3.758 + 0.15 + 0.25          = 4.158
```

and then built exactly one box, `8.316` m wide. **Nothing else existed laterally.** Every
prop `RoadsideProps` placed between 5.2 m and 26 m was standing on the skybox. There was
nowhere to put a farm, because there was no ground for a farm to be on.

### 2. Nothing was larger than 1.36 units

The tallest mesh in the entire game was `BuildObelisk`, at 1.36 units, and the far band
scales props by at most 3.2 — so the biggest thing beside the road topped out around 4.4 m.
There was no verticality, no skyline, and nothing you could be said to *pass*.

### 3. The grade was a constant, and it was fighting the palettes

`EnvironmentLook.BuildStack` built the post-processing profile once and dropped the component
handles on the floor, so all eight worlds were graded identically:

| Component | Shipped value |
|---|---|
| Bloom tint | `(1.00, 0.86, 0.72)` — warm |
| Colour filter | `(1.00, 0.96, 0.90)` — warm |
| Saturation | **−4** |
| Shadows / highlights | `(0.86, 0.92, 1.18)` / `(1.12, 1.02, 0.86)` |
| Vignette | `(0.02, 0.01, 0.04)` |

Every bright pixel in the green world bloomed orange. Every world was pulled back toward the
same warm grey, and the one control that decides how much colour survives was set to remove
it. Eight authored palettes cannot win against one shared grade sitting on top of them.

`DarkSky.shader` made it worse in a fourth way: `_GlowDirection` was never written from a
theme, so the ember band — the most recognisable single feature of the sky — sat straight
down +Z at the same height in all eight worlds.

---

## The land

`Assets/Resources/Terrain.shader`, and the same discipline as the road: everything derives
from world XZ, so there is **no texture to author, import, stream or strip and no UV channel
on the mesh** — which matters, because no mesh in this project has ever carried one.

- two octaves of the *same* `Hash21` and value noise `Road.shader` uses. Deliberately the
  same functions: two different noise fields meeting at the kerb would draw a seam along the
  full length of the level, which is the one place the eye is guaranteed to be
- the coarse octave decides where a patch of the alternate colour sits, the fine one keeps
  its edge from reading as a blob, and the result is squared so patches stay patches
- a per-metre speckle, because a 65 m flat plane gives the key light nothing to catch —
  exactly the failure the road had before it was given cobbles
- an optional sheen, gated on the patch mask so standing water sits in the low ground

Geometry is **two boxes, one per side**, from the road edge out to 70 m: two draw calls and
48 vertices for the entire world beside the road. Their top sits at y = −0.03, 3 cm below the
road, so the edge reads as a kerb instead of two coplanar surfaces z-fighting along 400 m —
and the rails at ±3.758 stand over the join anyway.

They receive shadows and cast none. Flat geometry casting its own shadow is a no-op, and the
band is the only surface large enough to show the rails' shadows falling across it.

Why 70 m: the scenery's far band reaches 26 m and the camera's far clip is 220 m, so the land
has to outrun everything standing on it while still ending well inside fog. At
`WorldThemes.MaxFogEnd = 185` the far edge is solid fog colour, which is what turns a finite
box into a horizon.

### Eight grounds, checked rather than eyeballed

| World | Ground | Patches |
|---|---|---|
| The Ashen Road | ash over dead grass | pale dust |
| Gallows Mire | dark bog | sedge |
| The Sunken Crypt | drowned flagstone | standing water |
| Ember Fields | scorched earth | live cinder |
| The Bone Wastes | pale sand | bone grit |
| The Frozen Reach | snow | drift |
| The Blood Marsh | red silt | wet silt |
| The Throne of Dust | violet dust | old flags |

A test requires every pair of worlds to be separable by more than 0.18 in summed RGB distance
across the base and patch colours. It found two collisions on the first pass that would
otherwise have shipped — a flooded crypt and a dusty throne room stood on nearly the same
ground (0.090), as did ember fields and a blood marsh (0.096) — and the worst pair now clears
at 0.204. It also caught the pair that matters most: **The Ashen Road and Gallows Mire are
rounds 1–2 and round 3**, which is precisely the transition the complaint was about, and they
started at 0.177.

Two further guards. Every ground has to stay above 0.10 peak, because Linear colour space
charges twice for authored darkness — the road shipped at 0.115 sRGB and arrived at the
shader as 1.25% reflectance, which is why the lower half of the frame was black. And every
patch colour has to be visibly different from its own base, or the second colour does nothing.

---

## The grade, derived instead of fixed

`EnvironmentLook` now keeps handles to `Bloom`, `ColorAdjustments`, `ShadowsMidtonesHighlights`
and `Vignette`, and `ApplyTheme` pushes per-world values into them. The build-once guard on
the profile stays — that part was always right.

Every value is **derived on `WorldTheme` from colours the world already declares**, in the
style of the existing `RailBase` / `AmbientSky` / `FogFromSky` properties, so a new world
cannot forget to grade itself and a second table cannot drift out of step with the first:

| Grade value | Derivation |
|---|---|
| Bloom tint | accent, normalised, 55% toward white |
| Colour filter | accent, normalised, 82% toward white |
| Saturation | `2 + 12 × accent chroma` |
| Vignette | sky zenith, normalised, ×0.06 |
| Shadows | sky zenith, 72% toward white, mean-normalised |
| Highlights | sky glow, 70% toward white, mean-normalised |

**World 0 reproduces the shipped constants.** Its derived bloom tint is `(1.000, 0.841, 0.709)`
against the authored `(1.00, 0.86, 0.72)`, and its filter is `(1.000, 0.936, 0.884)` against
`(1.00, 0.96, 0.90)` — inside 0.03 per channel, which is the tolerance a test now pins. The
Ashen Road keeps the look that was actually judged on a screen; the other seven stop borrowing
it.

Two details worth writing down.

**The multipliers are mean-normalised.** `ShadowsMidtonesHighlights` multiplies, so a tint
whose three channels do not average 1 silently lifts or crushes the frame as a side effect of
changing its hue. On device that reads as *"this world is brighter"* rather than *"this world
is green"* — the same mistake being fixed, pointing the other way. A test asserts the mean is
1 to within 0.001 for every world.

**Saturation is no longer negative.** −4 was actively removing the colour the worlds are made
of. It now runs from about +6 to +13, and the most chromatic world earns the most. This is the
single value most likely to need a device pass.

The hue shift is applied to the grade too. It already moves the sky, the fog, the road and the
props; a grade left un-shifted would drag every round of an act back toward the act's
undrifted colour — the same error at a smaller scale.

## The horizon points somewhere now

`WorldTheme.SkyGlowYaw` feeds `DarkSky`'s `_GlowDirection`, with `ThemeVariant.LightAzimuth`
riding on top at 35% so the band drifts between rounds as well as between worlds. Bounded to
±40°: past that, the band the player is meant to be running toward is beside them instead of
ahead. A test pins both the bound and the spread.

---

# Real scenery: castles, farms, and things you go past

## Where it comes from

`https://raw.githubusercontent.com/shorepine/kenney` mirrors Kenney's entire CC0 library with
one file per path. That matters because it is **the only asset host reachable from the build
container**: kenney.nl, itch.io, quaternius.com, OpenGameArt and Freesound all fail to
connect, GitHub's HTML and API return 403, and the zip endpoint is blocked too — so models
have to be fetched one at a time.

The kit index lists 49 kits and 4,812 glTF-binary models. This game uses 119 of them, drawn
from castle, fantasy-town, graveyard, nature and survival.

**File names had to be probed for.** There is no directory listing and the API is blocked. A
first pass of 290 plausible names found 95; the breakthrough was `graveyard/iron-fence`,
which proves the kits use **hyphens**, and a second pass of 5,014 hyphenated and suffixed
candidates took it to 175. `tooling/fetch_scenery.py --probe` re-checks the list.

## The bake

Unity cannot import `.glb`, and the packages that can — glTFast, UnityGLTF — bring a second
material and rendering path alongside the `Graphics.RenderMeshInstanced` one everything here
already uses, plus a GUID per model in a repo whose `.meta` files are all hand-written. So
the models are baked offline instead: **one committed file, one `.meta`, no importer, no
prefabs**, and meshes that arrive in exactly the shape the instanced renderer wants.

`tooling/fetch_scenery.py` fetches (cached, gitignored), parses, bakes and packs.

**Colour arrives two ways, and the baker has to handle both.** Most kits use a single
`colormap` material pointing at a 512×512 gradient atlas, with every vertex UV'd into a flat
patch of it. The nature kit has no image at all and instead splits a model into one primitive
per material, each carrying a linear `baseColorFactor`. Both bake down to a **vertex colour**,
so the game needs no textures, no UV channel and no per-piece material.

Two things that cost a run to discover:

- **No V flip.** The atlas is authored top-down to match these UVs. Flipping — the reflex when
  reading glTF — samples solid black, because the top-left of the atlas is empty.
- **The atlases are 8-bit indexed PNGs**, not truecolour. The decoder needed a `PLTE` path.
  Pillow is deliberately not a dependency (tooling has to run wherever CI does), so the whole
  decoder is one `zlib` call and five filter cases.

Vertices are then **welded on position, normal *and* colour together**. Welding on position
alone would smooth every hard edge in the pack and turn a castle into a blob — Kenney's meshes
are flat-shaded, so a corner shared by three faces is genuinely three vertices.

Result: 119 pieces, 30,886 vertices, 22,749 triangles, **563 KB**.

## The format, and the two bugs the tests caught

`Assets/Resources/Meshes/scenery.bytes`, read by `Core/Art/MeshPack.cs`:

```
'B','R','S','P' | version u16 | pieceCount u16
pieceCount records of 64 bytes:
    name 24B | vertexStart, vertexCount, indexStart, indexCount (4 x u32) | bounds 6 x f32
vertex block, 14 bytes each: position 3 x u16 | normal 3 x i8 | pad | colour 4 x u8
index block, u16 each
```

Positions quantise **per piece**, across that piece's own bounding box, rather than per pack.
A gravestone and a castle wall share no scale, and one global quantum would spend all of its
precision on the castle. Within a piece, u16 is about 0.02 mm on a 1.3 m wall.

The reader lives in Core because that is what makes it testable: the test assembly references
Core and nothing else, so `MeshPackTests` runs identically under headless `dotnet test` and
under Unity's runner, with no file path and no engine. `MeshPackWriter` exists purely so the
format round-trips in a test.

It earned its keep immediately. **Two format bugs were caught before anything reached a
device**, and neither would have thrown at runtime — a misread buffer does not error, it
scatters triangles across the level and looks like a physics bug:

1. `RecordBytes` was declared 56 when the record is 64 (24 + 16 + 24). Every vertex was read
   eight bytes early.
2. `VertexBytes` was declared 16 when both writers emit 14. The doc comment even claimed the
   padding byte kept the record 4-byte aligned, which 14 is not.

## Three zones, which are a budget rather than a label

| Zone | Distance | Content | Cap | Shadows |
|---|---|---|---|---|
| Verge | 5.2–12 m | gravestones, stumps, rocks, grass, plus the ten original procedural props | 144 | no |
| Field | 12–40 m | trees, pines, fences, carts, crops, iron railings | 96 | no |
| Landmark | 26–52 m | castles, cottages, mills, mausoleums, ruins, outcrops | 64 | **yes** |

Only landmarks cast. The verge is dense and its shadows fall on ground nobody looks at, and
the shadow pass is exactly where a field of scenery starts costing milliseconds — but a castle
that casts nothing sits on the land the way the army used to hover over the road.

Field distance is drawn with a **squared** distribution. A uniform draw across a 28 m band
puts as much in the first metre as the last, which crowds the near edge and leaves the far
one bare.

## A landmark is expanded, not baked

`Core/Art/Landmarks.cs` describes nine structures as **data**: a keep, a cottage, a windmill,
a watermill, a mausoleum, a ruin, an outcrop, a pine stand, a siege camp. Each is a list of
`(piece, local position, yaw, scale)`.

This is the load-bearing choice in the whole increment. Baked flat, the keep is 7,100 vertices
and one draw call — but six keeps on screen are six draws of 7,100. Kept as parts, **six keeps
are still three draw calls**, because every castle wall in the level lands in the same
instancing bucket regardless of which castle it belongs to.

It also makes a landmark walkable by a test, and three tests promptly found things:

- `NoLandmarkPartStandsWhereItsOwnFootprintSaysItDoesNot` caught four structures whose parts
  reached past the radius the placement uses to keep them clear of the road — the cottage
  fence by 0.1 m, the windmill's crops by 1.5 m. Every radius is now the computed reach.
- `LandmarkPartsAreStackedOnEachOtherRatherThanFloating` checks that anything raised is sitting
  on a part that actually reaches that high. Tower courses are stacked by hand from measured
  module heights (base 1.01, mid 1.01, roof 2.01) and a mistyped course would float.
- `ScaleTurnsKenneyModulesIntoBuildings` caught the mausoleum topping out at 5.7 m, which is a
  large building and not a landmark, and — after the fix — an upper bound was added because
  **at the landmark scale first chosen the castle keep stood 38 m and filled the sky.** At 3.4
  it stands 26 m, which is a real castle.

## Bright landmarks, dark verge

Kenney's palette is cheerful and saturated. The road the player actually looks down has to
stay grim or this stops being dark fantasy — but a lit castle on the horizon is worth more
than either extreme alone. So the pack is baked **honestly**, in Kenney's own colour, and each
zone is dragged its own distance toward the world's `PropStone` at runtime:

| | verge | field | landmark |
|---|---:|---:|---:|
| The Ashen Road | 0.32 | 0.20 | 0.12 |
| The Frozen Reach | 0.26 | 0.13 | 0.05 |
| Gallows Mire | 0.34 | 0.22 | 0.14 |

A test pins the ordering — verge grimmer than field grimmer than landmark, in every world —
so this cannot quietly invert. It is a *lerp* in albedo, not a multiply: multiplying toward
grey desaturates but also darkens everything equally, which flattens a verge rather than
making it grim.

Because the tint is a runtime uniform, **changing the mood of the whole game is a number, not
a re-bake**.

### Those numbers were 0.60–0.78, and that was the bug

The direction was right and the amount was wrong by a factor of two and a half. At 0.74,
three quarters of every roadside piece's albedo is replaced by **one colour per world**.
Worlds 1-1 and 2-1 share *zero* verge pieces — different gravestones, different stumps,
different rocks — and still arrived on device as two flat greys, because whatever different
thing was placed there was then lerped to within a quarter of the same paint. The player's
report was *"visuals per level have a tiny change"*, which is a precise description of what
0.74 does.

So the test's **floor became a ceiling**: at most 0.40 of a piece may be painted over, and
the authored values sit at 0.26–0.34. A stump still looks like wood and a gravestone still
looks like stone; the ordering that makes the skyline the colourful thing is untouched.

The other half of the same problem was the **ten procedural props**, which are painted the
world's `VergeStone` outright with nothing varying them per instance — ten silhouettes
arriving as one flat mass, at roughly 37% of the verge population. Their density is halved
and the imported verge raised by the same amount, so the roadside is as full as it was and
more of it is art that carries its own colour. It costs no draw calls: the pieces were
already in the palette, so the instances land in buckets that already existed.

### Nine landmarks for eight worlds

`Ruin` stood in five worlds and `Mausoleum` in five, so two worlds a whole act apart shared
both their low walls and their only house — while **sixteen baked modules had never been
referenced by anything**: the entire roof, corner and tower vocabulary (`fa_roof-gable`,
`fa_roof-point`, `fa_wall-half`, `ca_tower-square`, `ca_wall-corner` and the rest).

Because a landmark is a **part list** and not a mesh, eight more cost zero fetch, zero bake
and **zero additional draw calls** — a chapel's walls land in the same instancing bucket as a
cottage's. Every world now has one nothing else uses:

| world | its own landmark | built from |
|---|---|---|
| The Ashen Road | `chapel` | gabled nave, bell tower, churchyard wall |
| Gallows Mire | `stilthouse` | plank platform, wood walls, flat roof, smoke pipe |
| The Sunken Crypt | `columnhall` | four columns and a slab, stairs, a crypt |
| Ember Fields | `smithy` | three walls open at the front, chimney, barrels |
| The Bone Wastes | `boneshrine` | a head on a plinth among broken rock |
| The Frozen Reach | `watchtower` | four-course flagged tower on a broken curtain |
| The Blood Marsh | `gatehouse` | a gate between two square towers |
| The Throne of Dust | `manor` | two wings, corner roof, fenced yard |

`EveryWorldHasOneLandmarkNoOtherWorldUses` pins it, and nothing may stand in more than four
of the eight. Two existing tests did real work while these were being written:
`LandmarkPartsAreStackedOnEachOtherRatherThanFloating` refused a course placed at a guessed
height rather than a measured one, and `ScaleTurnsKenneyModulesIntoBuildings` caught the
chapel at 5.3 m — a nave alone is a building, not a landmark, which is why it has a tower.

## The shader, and one coupling deleted rather than worked around

`Assets/Resources/Scenery.shader` is `CrowdInstanced`'s lighting — the same wrapped
half-lambert, the same shadow floor, the same per-pixel fog — with colour read from `COLOR`
instead of the material, and with the run-bob and the scale decode **removed**.

That removal is the point. `CrowdInstanced` recovers each soldier's bob phase from *instance
scale* inside a 0.44–0.50 window, and anything outside pins to 1.0 and brightens about 30%.
Scenery is placed at 1.5× to 3.4×. Borrowing the crowd shader would have meant remembering to
zero `_ToneSpread` on every scenery material forever; a separate shader means the trap does
not exist.

The lighting is otherwise identical on purpose: an imported castle and a procedural soldier in
one frame under two different lighting models is the fastest way to make bought-in art look
pasted on.

## Eight worlds, dressed differently

The Ashen Road gets graveyard and dead trees under ruins and a cottage. Gallows Mire gets
stumps, moss and a watermill. The Sunken Crypt gets colonnades and mausoleums. Ember Fields
gets a siege camp. The Bone Wastes gets obelisks and a keep on the skyline. The Frozen Reach
gets pine stands and a frozen keep. The Blood Marsh gets swamp huts and a watermill. The
Throne of Dust gets a full castle.

A test requires that no two worlds share more than three quarters of any band, and that no two
have identical landmark sets.

## Licence

Everything is **CC0**. `Assets/Art/LICENSE-KENNEY-CC0.txt` carries Kenney's verbatim text —
*"You can use this content for personal, educational, and commercial purposes"*, with crediting
explicitly *not* a requirement — and `Assets/Art/ASSETS.md` maps every baked piece to its kit,
its original Kenney file name, its zone, its triangle count and its height.

The `.glb` sources are **not committed**; `tooling/.cache/` is gitignored and the script
re-fetches on demand. `.gitattributes` gained explicit `binary` entries, because it previously
marked nine text extensions and nothing as binary — a new `.bytes` or `.wav` was relying
entirely on git's content heuristic, and the unconditional `*.asset text eol=lf` would have
LF-mangled any Unity asset written in binary mode.
