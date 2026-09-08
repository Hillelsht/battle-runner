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
