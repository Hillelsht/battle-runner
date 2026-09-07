# 10 — The look: render stack and art direction

## The problem this solves

The greybox was not ugly because it lacked models. It was ugly because it was **unlit,
unresolved and ungrounded**, and each of those is a setting, not an asset.

| Symptom | Actual cause |
|---|---|
| Emissive accents looked like flat bright paint | `supportsHDR = false`. An emission of 1.4 clamps to 1.0 in LDR, and bloom has nothing above white left to bloom. |
| Everything looked washed out and video-gamey | No tonemapping, no colour grading, no vignette — raw linear→sRGB. |
| Hard crawling edges on every box | `msaaSampleCount = 1`, on a game made entirely of hard-edged boxes against darkness — the worst case for aliasing. |
| The army hovered above the road | No shadows anywhere. Nothing cast, nothing received. |
| The world ended in flat charcoal | `CameraClearFlags.SolidColor`. No sky, no horizon, no depth cue. |
| Untextured boxes read as cardboard | `AmbientMode.Flat` lit a sky-facing and a ground-facing surface identically. |

None of those need an artist. All of them are the same size of fix.

## The stack

Set in `Assets/Scripts/Editor/UrpBootstrap.cs` (pipeline asset) and
`Assets/Scripts/Gameplay/EnvironmentLook.cs` (runtime).

**Pipeline** — HDR on, MSAA 4x, one shadow-casting directional light with a single
cascade at 45 m and a 1024 map. Depth and opaque textures stay off; nothing samples them
and each costs a full extra pass on mobile.

**Post-processing**, built as a `VolumeProfile` at runtime rather than as a serialized
asset — the look changes far more often than the code does, and a profile made in code
cannot drift from the scene or be half-migrated by a Unity upgrade:

| Override | Setting | Why |
|---|---|---|
| Tonemapping | Neutral | ACES crushes the low end, and this game is almost entirely low end — the blacks it would eat are the road and the sky. |
| Bloom | threshold 0.85, intensity 1.15, scatter 0.72, warm tint | The single biggest change. Threshold just under white means only the emissive accents bloom and the dark 90% of the frame stays crisp. |
| Color Adjustments | +0.20 exposure, +22 contrast, −4 saturation | Recovers the contrast the tonemapper flattens. |
| Shadows/Midtones/Highlights | cool-violet shadows, warm-ember highlights | One component, and most of what reads as *dark fantasy* rather than merely *dark*. |
| Vignette | 0.34 / 0.45 | Focuses a tall portrait frame and hides where the fog meets the screen edge. |

**Sky** — `Assets/Resources/DarkSky.shader`, procedural: three bands by view-direction
height, one ember glow low on the horizon ahead so the frame has a direction to run
toward, and hash-based stars faded out near the horizon. No cubemap to import or strip.

**Ambient** — Trilight, not Flat, with a cool sky and a warm equator. Free shading on
geometry that has no texture to carry it.

**Shadows** — `CrowdInstanced.shader` gained a `ShadowCaster` pass and the forward pass
now samples the shadow map. Vertex displacement in the two passes goes through one shared
`APPLY_RUN_BOB` macro rather than two copied blocks: if they ever disagreed, every unit's
shadow would detach from its feet and slide. Shadow attenuates only the **direct** term —
ambient and rim survive it, so a shadowed face darkens instead of becoming a black hole,
which in a game this dark would read as a missing polygon rather than as shade.

What casts: the crowd, the hero, the boss, enemy packs, the rails. What does not: lane
lines and speed rungs, which are 2 cm tall road decals whose shadows would be noise on
the surface they are painted on.

## The one reversed decision

Doc 04 said *no realtime shadows on mobile*. That was correct for a 300-unit crowd of
individual renderers. The crowd is a single `RenderMeshInstanced` draw, so casting costs
one more instanced draw into the shadow map — and the alternative is an army that visibly
floats. If the shadow pass turns out to cost real frames on a low tier, the lever is
`shadowDistance`, not the feature.

## Surfaces

**The road** — `Assets/Resources/Road.shader`. It is the largest single area of the frame
and was an untextured slab: no amount of lighting makes a featureless plane interesting,
because there is nothing on it for the light to catch.

Everything is derived from world XZ, so it tiles forever down a road of any length with
no texture to author, import, stream or strip:

- **Brick-bonded cobbles** — every other row shifts half a stone, so the mortar never
  lines up into long straight seams running away down the road.
- **Per-stone tone** from a hash of the cell, so no two neighbours match.
- **Two octaves of value noise** for grime at three metres and damp at ten. Enough to
  break up the regularity without looking like noise for its own sake.
- **A wet sheen** on the stone tops only, strongest where the grime says the stone is
  damp, and killed inside a shadow. This is what makes the road read as a *surface*
  rather than as a colour.

It receives shadows like everything else. It does not cast — it is the floor.

## The UI

Every screen builds through `UiFactory`, so one file changed all of them at once.
Sprites are generated in code by `UiTextures` — no imported images:

- **Rounded, bevelled panels and buttons.** The shape lives in the sprite's ALPHA and the
  RGB carries only a lit-from-above gradient. That matters: screens tint these images to
  say what a widget *means* — a taken talent is gold, a locked one is dark — and a sprite
  with colour baked into its RGB would multiply against that tint and turn every state
  muddy.
- **A bronze frame** with corner notches, as a separate child drawn over the fill, because
  the frame is always bronze whatever the fill beneath it is saying and one tinted image
  cannot be two colours. The notches sit inside the 9-slice corner region so they never
  stretch with the widget.
- **A gradient backdrop** instead of a flat wash. A single unbroken colour behind
  everything is most of what reads as "unfinished app".
- **Explicit button states.** uGUI's default `ColorBlock` fades a disabled button to 50%
  alpha, which on a dark background is indistinguishable from an enabled one.

Two opt-outs exist and both are load-bearing. `Panel(..., rounded: false)` for thin
progress fills — a 9-sliced rounded sprite on a bar a few pixels wide spends its whole
width on corner radius and stops reading as a quantity, which for a boss health bar is
the one thing it has to do. And `FullscreenPanel(..., gradient: false)` for the resurrect
scrim, where the caller's colour and alpha *are* the design: replacing them with a warm
opaque gradient would hide the very thing the player is being asked to decide about.

## What the first device screenshots showed

Two defects in the sky, both mine, both arithmetic rather than taste.

**The ember glow filled the whole sky.** It is `pow(saturate(dot(dir, glowDir)), _GlowPower)`
and `_GlowPower` was **6**, which falls to half brightness at

```
acos(0.5^(1/6)) = acos(0.891) = 27 degrees
```

The camera's horizontal half-FOV is 18 degrees and its vertical half-FOV is 30, so every
pixel of visible sky sat at or above half glow: an ember lamp rendered as a red dome
across the top half of the frame, then pushed further by `postExposure +0.20` and the
warm-tinted highlights. `_GlowPower` is now **110**, a half-angle of 6.4 degrees.

A second contributor: the rig pitches about 11 degrees down, so visible sky only reaches
~19 degrees elevation, where `pow(sin(19deg), 0.55) = 0.54`. Even the top of the frame was
barely half-way to the zenith colour, so the *horizon* band coloured everything on screen.
The horizon is now darker and blue-violet rather than mauve, and `_ZenithFalloff` drops to
0.4 so the sky reaches its dark zenith sooner.

**The stars were 18-pixel grey quads.** `floor()` gives every pixel in a cell the same
value, so a "star" was the entire cell:

```
cell width in dir.x = 0.65 / 60      = 0.0108
dir.x per pixel     = 0.65 / 1080    = 0.000602
                                     = 18 px per cell
```

and peak brightness was `(1 - 0.985) * 66 * 0.55 * 0.6 = 0.33`, mid-grey. Together that is
confetti, not a night sky. Stars now hash a *position inside* each cell, measure the
pixel's distance to it and fall off over 0.055 of a cell, at a brightness above 1.0 so
bloom treats them as light.

The lesson generalises: a falloff exponent is only meaningful against the field of view it
is seen through, and `floor()` alone never makes a point.

## What a 30-agent diagnosis found that I could not

Six subsystems were investigated in parallel against the real code, each finding then
handed to an adversarial verifier told to refute it. **24 findings, 11 survived, 13 were
killed** — including several that sounded right (bloom clipping the sky, vignette eating
the near road, contrast crushing the road to black) and did not hold up under arithmetic.

Three of the survivors were causes I had no path to from the screenshots alone.

**The key light pointed the same way the camera looks.** `Euler(55, -35, 0)` has forward
`(-0.329, -0.819, +0.470)`. The horizontal part is **+Z** — the camera's own view
direction — so every shadow was cast directly away from the viewer and landed behind its
own caster. A caster of height `h` at distance `d` from a camera at height `H` hides
`d·h/(H−h)` of ground behind itself; at `H = 5.5` that is 2.1–2.9 m for the army, and the
shadow only reached 0.55 m. **The shadows were rendering correctly and were 100%
self-occluded.** The light now comes from ahead and to the right at 32° rather than 55°,
so shadows rake across the road toward the camera at 1.6× the caster's height.

**Everything in a `.mat` is sRGB.** The project renders in Linear space, so material
colour properties are gamma→linear converted on upload. `Road._BaseColor = 0.115` reached
the shader as **0.0125 linear — a 1.25% reflectance**, seven times darker than dark
asphalt and darker than charcoal. No grade could rescue that; the lower half of the frame
was black because the albedo was physically impossible. Darkness in a night scene has to
come from the light level, not from an albedo no material can have.

**The rim term is a per-face constant, not an edge.** `pow(1 − dot(V,N), 2.5)` assumes
smooth normals. `ProceduralMeshes.AddBox` duplicates vertices per face for *hard* normals,
so it is constant across each face and exceeds half strength for any face more than 76°
off the view axis. Of the three faces the camera sees on a unit, two are flooded and the
`+0.15` constant floods the third: the crowd was **79–97% pure emission** — self-lit
blocks, not lit figures.

Two more shape defects fell out of the same pass. The formation is pinned to one lane
(1.56 m across) while a body is 0.60 m over the pauldrons, so at n=90 the lateral pitch is
0.157 m and units were drawn nearly **four body-widths into one another** — geometrically
a solid slab before any shader ran; the drawn scale drops from ~1.0 to ~0.47. And a gate's
bars face the camera dead-on, making rim ≈ 0 on every visible surface, so a gate's entire
glow came from the flat term and landed *below URP's bloom knee*: gates contributed
exactly nothing to the bloom pass. Frame and infill plate are now separate materials, the
frame pushed over the knee and the plate deliberately left under it.

## Two CI round trips, and what each cost

Neither could have been caught from a container without a Unity editor, and both were
one line:

1. **`CS0234`** — `BattleRunner.Gameplay` never declared the URP assemblies. See below;
   this one is now caught locally.
2. **`CS0619`** — `Bloom.skipIterations` was removed in URP 2023.1 and is obsolete-as-an-
   **error**, not a warning. Every other post-processing parameter compiled first time, so
   the fix was `maxIterations` and nothing else. There is no local check for this one: it
   needs the real URP assemblies to know what is deprecated, which is exactly what CI has
   and this container does not.

## The check that came out of it

The first attempt at this stage failed in CI with `CS0234: the namespace 'Universal' does
not exist in 'UnityEngine.Rendering'`. **Unity assembly references are not transitive** —
`BattleRunner.Gameplay` referenced `BattleRunner.Meta`, but using a URP type meant it had
to name `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime`
itself.

That is a missing line of JSON diagnosed by a headless editor sixteen minutes later, so
`tooling/check_asmdef_refs.py` now finds package namespaces and distinctive type names in
the `.cs` files under each asmdef and asserts the asmdef declares the assembly providing
them. It runs in the pre-push hook and in CI, takes about a second, and was verified by
reverting the fix and confirming it reproduces both original errors.

## Feel: the camera and the block window

Every event in the game used to look identical. A x2 gate, a pack eating half the army
and a boss blow all produced the same nothing.

**Impulse size is a RATIO, not a difference.** `Core/Feel/CameraFeel.cs` is engine-free
and unit-tested for exactly that reason: force is unbounded, so a `+5` gate is enormous at
10 units and meaningless at 1000. Magnitude is measured in octaves — one doubling is half
strength, two is full — so `10 -> 20` lands exactly as hard as `1000 -> 2000`, and a test
pins it. Nothing may saturate the shake pool on its own, or every event would feel the
same again from the other direction.

**The rig keeps its own base pose** and writes the juiced pose to the transform. The old
code lerped `transform.position` toward the target *from `transform.position` itself*, so
any offset written in became the next frame's input — the shake would fold into the
smoothing and the camera would chase its own noise. Shake amplitude is trauma **squared**,
which is what separates a shake from a jitter.

Shake moves world **x and y only, never z**: the despawn plane is derived from
`SetbackMeters` as a constant, so inventing world-z motion would let a gate pop out of
existence in front of the player. Most of the shake therefore lives in *rotation*, which
moves the position by nothing at all. The follow is now frame-rate independent
(`1 - exp(-rate·dt)`), where the old `Lerp(a, b, dt * 5f)` travelled further per second on
a slow device than on a fast one.

**The shield had no feedback whatsoever.** You flicked down and nothing on screen changed,
so there was no way to learn the flick had registered, and no way to see the window close —
a pure timing mechanic played blind. `ShieldWard` recolours the crowd's *own* emission
rather than drawing a dome: this project ships exactly three shaders and all are opaque, so
a translucent shell would need a fourth in `Resources` — the shader-stripping path that
shipped v0.1.0 as solid magenta. It costs no new mesh, material, shader or draw call. It
lights the **silhouette** rather than filling the bodies, because the army has to stay
readable while you are still steering through it. A blow the shield actually eats spikes
it near-white.

**The boss flash needed the flat term, not the colour.** `CrowdInstanced` adds
`_EmissionColor * (rim * _RimStrength + _EmissionFlat)`, and the boss's camera-facing slab
has `rim ≈ 0` — so raising the emission colour alone arrived at 15% strength and vanished.
Driving the view-independent flat term is what makes the hit land. The scale dip went from
0.96 to 0.90 because 0.96 was a 0.23-unit flinch on a 5.7-unit figure, recovered in about
two frames: invisible.

The rails were **~83% pure emission** — the flat term applies at every angle, and the wide
default rim lobe flooded their grazing faces on top of it, which bloom then turned into
light. Cut, but deliberately not to zero: they are the peripheral cue for where the road
ends. A tight rim keeps a bright edge on the silhouette while the faces go dark.

## The boss stopped being a soldier at 6x

Through v0.6.1 the boss was literally `ProceduralMeshes.Unit` with `localScale = 6`. Same
four axis-aligned boxes as every one of the two hundred units running at it, just bigger.
Nothing about it said *boss* except size, and size alone does not read at 26 m through a
36-degree horizontal field of view.

`ProceduralMeshes.Boss` is its own mesh — 132 triangles, 6.66 m tall at the same 6x. What
it adds is all silhouette, because silhouette is the only channel that survives that
distance:

- **Horns.** Two segments on the left that sweep out and forward, a snapped stub on the
  right. They are the highest point on the mesh and nothing competes with them.
- **A forward lean.** The torso is a prism, narrow at the waist and wide at the shoulders,
  with the top edge pushed 6 cm toward the player. A vertical trunk reads as a pillar
  however wide you make the top; a leaning one reads as a body.
- **Asymmetry.** Mismatched pauldrons, and a cleaver held out on the right whose outer
  corner clears the road edge by 21 cm. Bilateral symmetry is most of why the old shape
  read as scenery.

Two things fell out of it that are not cosmetic. The mesh is built from *sheared* hulls
(`AddPrism`, `AddOrientedBox`) rather than axis-aligned boxes, so its faces finally present
the camera varied normals — which is what the rim term needs, and why the boss could widen
its lobe to `_RimPower 1.8 / _RimStrength 1.15` and gain shape instead of a flat wash. And
because the mesh is asymmetric, `BossView`'s inherited 180-degree yaw stopped being a no-op:
it was harmless only while the boss *was* the unit mesh, which is symmetric about both axes.
Left in, it would have shown the player the boss's back. Every mesh here faces −Z; the boss
now does too, and the yaw is gone.

Widths are budgeted against the road, not eyeballed: lanes are 2.2 m and the road spans
±3.3 m, so at 6x nothing may exceed x = 0.55. The body stops at 0.410 and only the cleaver
passes it.

Giving it a silhouette exposed that it had never cast a shadow. The boss stands at
`CenterZ + 16` and the camera at `CenterZ - 10`, which is 26.1 m apart including the
height difference — and `shadowDistance` was 24 m. It was outside the map entirely,
casting nothing and receiving nothing, while `BossView` set `ShadowCastingMode.On`.
28 m reaches it at ~20 mm per texel, still five times finer than the 110 mm half-depth
that decides whether a caster inverts. The distance is now part of the asset's dirty
test as well: it was being assigned on every run but never counted as a change, so any
machine that had already generated the URP asset would have written the new value into
memory and thrown it away unsaved.

## The road was lavender, and the albedo was only half of why

`_BaseColor` was `(0.30, 0.28, 0.35)` — blue highest, green lowest, which is violet by
definition. But the compounding mattered more than the value. The road's ambient term is
`SampleSH(normalWS)`, fed straight off a sky dome that is deep blue-violet, so a violet
albedo was being multiplied by a violet light and the largest surface in the frame came
back tinted twice.

The albedo is now warm-neutral, `(0.31, 0.295, 0.285)`, and the cold ambient does the
tinting on its own — which is both the fix and the dark-fantasy reference. `_DampColor`
stays cool, because it is a reflection of that same sky, but is pulled back from
`(0.32, 0.34, 0.48)` so the sheen is not a second full-coverage blue wash on top.

## What the v0.7.0 screenshots cost me: two of my own claims

The boss silhouette worked — horns, mismatched shoulders and a raised cleaver all read at
26 m. But the device shot showed it rendering at a uniform **#3a3a3a across every face**,
head and torso and blade within 2/255 of each other, *darker than the road it stands on*.
A ten-agent diagnosis with two skeptics per finding settled why, and refuted two things
this document previously asserted.

**The boss was 93% constant.** Three causes stacked:

- `BossView.Show` uploaded `tint * 0.5f`. These are sRGB values in a linear project, so
  halving in sRGB is a **0.234× cut in linear** — the gamma curve charges for it twice.
  A (0.55, 0.50, 0.45) tint arrived as a 5% reflectance albedo, roughly coal.
- `saturate(dot(N, L))` collapses *every* back-facing normal onto one value, and the boss
  is backlit — light direction (0.797, 0.530, 0.290), boss facing −Z. Torso, head, both
  pauldrons and the horns all clamped to the same 0.45. One number for the whole figure.
- `_EmissionFlat` at 0.15 against that dead albedo was **two thirds of every pixel**, and
  it depends on neither normal nor view. Literally paint sprayed over the silhouette.

The fix is a real albedo, a **wrapped** half-lambert (`dot * 0.5 + 0.5`, remapped to a
[0.30, 1.00] floor so nothing that used to be visible goes black), and the resting flat
term cut to the crowd's own 0.03 — with the *telegraph* now driving it back up to 0.33,
because that term was the entire wind-up warning and cutting it silently would have
removed the only cue the player gets before a blow lands.

Widening the rim lobe to 1.8 was also just wrong, and the diagnosis refuted my reasoning
for it: `pow(1 - dot(V,N), k)` is ~0.004 on the faces the camera sees most of, whatever k
is. Widening only lit the profile faces you can barely see. Shape comes from the diffuse
term; the rim is back to a tight 3.5 doing what it is good at.

**The lavender was never the road.** The v0.7.0 commit changed the road albedo on the
premise that "ambient here is `SampleSH` off a deep blue-violet sky dome". That premise
is **false**: ambient is `AmbientMode.Trilight`, which does not sample the skybox at all,
and the sky's horizon glow is warm anyway. The albedo change was harmless but it was
aimed at the wrong thing. The actual lavender is two other surfaces:

- **The rails.** `_EmissionColor` (0.30, 0.36, 0.58) at a blue/red ratio of 4.0, and their
  large visible face is the *inner side*, whose normal is perpendicular to the view axis —
  so even at `_RimPower 5` the rim term reads 0.52 at 30 m and 0.79 at 80 m. Not an edge:
  a self-lit periwinkle bar running the length of the frame.
- **The lane lines and speed rungs.** 2 cm decals whose only visible face points straight
  up, at ~80° off the view axis where the shader's *default* wide lobe reads 0.64. 86%
  pure emission at a blue/red ratio of 3.5, in a dense grid over the whole road.

Both are now neutral in hue with tight lobes. Same treatment the rails got once already —
the mistake was cutting their flat term and leaving the rim strength at 0.7.

## The end of the world, and why fog could not hide it

The road visibly terminated in mid-air about 62 m in front of the camera by the end of a
level. Three independent causes, and no two of them are sufficient alone:

1. **Fog was stripped from the Android build entirely.** `EnvironmentLook` sets
   `RenderSettings.fog = true` at runtime, but `Main.unity` had `m_Fog: 0`. Unity's
   default fog stripping is *Automatic*: it keeps a `FOG_*` variant only if some scene in
   the build enables that mode in its lighting settings. No scene did, so
   `#pragma multi_compile_fog` only ever compiled the no-fog branch and `MixFog` was a
   no-op on device. Every fog value in this project was dead code. The scene now enables
   fog in the *same mode* the runtime selects — if one changes, the other must.
2. **The ground stopped 40 m past the finish.** One `SpawnGroundStrip(-6, _finishZ + 40)`
   builds the ground, four lane lines and both rails as single stretched boxes, so
   extending it to +180 m costs nothing — no extra draw calls at any length. Only the
   speed rungs are per-metre, and they get their own bound.
3. **Fog was the wrong mode and the wrong colour.** Exponential at 0.014 needs ~280 m to
   reach 98% — past the 220 m far clip — and the density that would reach it by 170 m
   washes half the contrast out of the road at 30 m where the game is played. And the fog
   colour matched `DarkSky`'s `_HorizonColor` but omitted the `_GlowColor` the sky *adds*
   on top, which is at full strength exactly where the road meets the horizon: fog three
   stops darker than the sky behind it cannot dissolve an edge, it draws one. Linear fog
   70→170 m with a warm (0.44, 0.30, 0.23) puts a wall exactly where it is wanted and
   buries the far clip 50 m inside it.

## The gates were over the bloom knee, but not because they were near

The near gate filling the bottom of the frame is not a proximity problem — a verifier
measured the shipped pixels and found it marginally *dimmer* than the far one. What is
real is that gate frames were **1.7–3.8× the bloom threshold** everywhere. The three gate
colours are already over white and are `Color` properties in a linear project, so they are
gamma-*expanded* on upload to 1.49 / 1.78 / 1.23 before `_EmissionFlat 1.15` multiplies
them. And gates never overrode the shader's wide default rim lobe — the treatment the
rails got and the gates missed — which added +0.62 on exactly the uprights' inner faces.
`_EmissionFlat` is now 0.70, the point at which the *dimmest* gate colour still clears the
threshold face-on, so every gate keeps blooming and the peak stops being four times over.

## Enemies were twice the size of the soldiers running at them

Not a design choice — a stale constant. Enemy pack bodies are drawn at `localScale = 1`,
which was correct when the crowd was drawn at 0.94–1.06. When the formation was pinned
inside one lane the crowd dropped to 0.44–0.50 and the packs were never brought with it,
so for several releases five enemies visually outweighed a 116-strong army. That breaks
the one comparison the whole game is about. Now `BodyScale = 0.47`, with the cluster
offsets and the force label scaled to match.

## Fog on a 400-metre quad: the fix that broke the thing it fixed

v0.8.0 turned fog on for the first time and stretched the ground to 180 m past the
finish. Both were right, and together they produced a new defect: on device the near
road **brightened about 2x and warmed toward the fog colour between the start of a level
and its boss**, with nothing about the material or the viewing distance having changed.
Measured medians across the road width, near row, same build:

| shot | crowd | near road |
|---|---:|---|
| early run | 15 | (27, 25, 33) |
| early run | 20 | (25, 22, 28) |
| mid run | 68 | (41, 31, 38) |
| boss | 93 | (51, 36, 40) |

Both shaders computed `ComputeFogFactor(positionCS.z)` **at the vertex**. That is normal
and correct for ordinary geometry — and completely wrong for this project's ground, whose
lane lines and rails are each a single stretched box spanning the entire level from eight
corner vertices. A factor evaluated at those corners and interpolated across 400 m of road
depends on where the camera sits along the box, not on how far away the pixel is. Early in
a level the camera is near the box's near corner and the road reads clear; by the boss it
is halfway along and the whole visible road is dragged toward the fog colour.

Fog is now computed **per pixel**, by recomputing the clip position from the interpolated
world position in the fragment. That is one matrix multiply, and it uses URP's own
`ComputeFogFactor` rather than unpacking `unity_FogParams` by hand so it stays correct
under reversed-Z and any future fog mode. Segmenting the ground into shorter boxes would
also have worked and would have cost eighteen times the draw calls.

## Two overcorrections in the same pass

**Lane lines went from a lavender wash to invisible.** Neutralising their hue was right;
the level was not. At `_EmissionColor 0.34` and a 0.08 flat term they landed at ~0.9x the
road's own luminance — darker than the stone they are painted on — and on the dark early
stretch of a level they all but disappeared. In a three-lane game the lane read is not
decoration. Back to ~1.5x, still neutral.

**The finish line had the same unrestrained rim the gates did.** It is a full-width slab
whose only visible face points straight up, at ~80 degrees off the view axis where the
shader's default lobe reads 0.64, over a colour already above white. The boss fight
happens past it, so it filled the bottom of the frame with saturated gold for the whole
encounter. Same tight lobe the rails and gates now have.

## The shield ward reads

Confirmed on device at last, three builds after it shipped: with the block window open the
army lights cyan-white, the hero goes near-white, and the HUD reads SHIELDED. It was the
change I was least sure about — recolouring the crowd's own emission rather than drawing a
dome, to avoid a fourth shader in `Resources` — and it is unambiguous in a still frame,
never mind in motion.

## Additive VFX, and how the magenta risk was actually retired

Until this pass **nothing happened when you passed a gate**. The number changed, the camera
nudged, and that was the entire event — in a game whose whole appeal is the moment the
crowd doubles. Same for a pack taking a bite, for the spell, for killing the boss.

`Assets/Resources/Vfx.shader` is the fourth shader in the project and the one with the most
history behind it: a shader reachable only through `Shader.Find` gets stripped from an
Android build and renders solid magenta, which is how v0.1.0 shipped. Three things retire
that risk rather than hoping:

- It lives in `Resources` beside a hand-written `Vfx.mat` that references it **by GUID** —
  the same arrangement `CrowdInstanced`/`Crowd.mat` has used successfully since v0.1.1.
- It is **keyword-free**. No `multi_compile` of any kind, so there is exactly one variant
  and nothing for variant stripping to get wrong.
- `VfxSystem.Initialize` validates the material against the **active pipeline** and, if
  anything is off, leaves `Enabled` false — after which every `Shock` and `Burst` is a
  no-op. And unlike `LoadCrowdMaterial`, there is deliberately **no fallback material**:
  substituting an opaque shader for an additive one would flash untinted rectangles across
  the road, which is worse than silence. The crowd has to be drawn one way or another; a
  shockwave does not. Worst case is a build with no effects, never magenta ones.

`Fallback Off` in the shader says the same thing to Unity.

**The first shockwave shape never appeared on device, and the debris did.** v0.9.0
screenshots settled it by measurement, not by eye: motes sampled at (83, 122, 217) against
a road of ~(30, 30, 45) — unmistakably there — while a scan across the road at four
different depths found no ring crest anywhere, in any frame. Same material, same shader,
same draw path. Only two things were unique to the ring:

- It was **the only mesh in the project carrying UVs**, and the shader shaped its falloff
  from `uv.x`. If that channel did not reach the shader, `uv.x` read 0, `sin(0)²` was 0,
  and the ring drew nothing — while the UV-free motes on the same material drew fine.
  That fits the evidence exactly.
- It was **flat**, so `RecalculateBounds` gave it zero extent in Y, and at 5.5 m up and
  10 m back the camera saw it nearly edge-on — a ground ring squashed into a thin ellipse.

Rather than guess between them, the shape changed to an **open cylinder wall** that expands
and flattens, which retires both: it has no UVs (the shader shapes the falloff from
object-space height, which every mesh has), it has real bounds, and it faces the camera.
That is also how shockwaves are drawn in a 3D scene anyway. `_Band` still selects between a
flat surface for the motes and the falloff for the wall — one shader, two shapes, no
keywords, and now no UV channel anywhere in the project.

The falloff is squared, not linear: brightest where the wave meets the ground and dying
away fast up the wall, which gives it a crest instead of reading as a lit cylinder. The
radius eases **out** while brightness falls off faster than linear, so a wave sprints away
from its origin and is gone before it stops moving — easing the radius linearly instead
reads as an inflating balloon.

**No fog on it, on purpose.** Fog *lerps toward* the fog colour, which on additive geometry
ADDS light in the distance instead of removing it. Everything drawn here is within 30 m,
well inside the 70 m fog start, so the correct amount of fog is none.

**Impulses are sized by the same octave ratio the camera shake uses** (`CameraFeel.Trauma`),
so a ×2 at 10 units and a ×2 at 1000 throw the same ring, and a +1 barely ripples. The
enemy-contact debris is scaled to what the pack *actually took* rather than its printed
cost, because Bramble and Undying cut the bite and the effect should show the bite.

Both `GateApplied` and `EnemyContact` now carry the **world position** of the thing that
resolved. At 10 m/s the gate and the crowd are metres apart by the time the handler runs,
and an effect that does not land on its cause reads as an unrelated flash.

The one thing I could not settle from a container: whether Unity gamma-expands a `Color`
set through a `MaterialPropertyBlock` in a linear project. Peak channels are therefore near
1.6 rather than 2.5 — comfortably over the 0.85 bloom threshold if the value is taken raw,
hot but not absurd if it is expanded (1.6^2.2 = 2.9). At 2.5 the expanded case would be 8.5
and the screen would white out. A device screenshot decides which, and then these tune in
one direction with confidence.

## The shield needed an object, not a colour

The player's report was "no shield effect". The screenshots disagree — the Ember Lich
frame shows the army lit brilliant cyan with `SHIELDED` in the HUD, so `ShieldWard` fires
exactly as designed. They were still right about the thing that matters: **recolouring the
army reads as the army changing colour, not as a shield.** A defensive verb needs a
defensive object.

So `ShieldDome` draws the shell and the ward stays as the secondary cue. Two channels for
one event is not redundancy here: the dome says *you are protected*, the ward keeps the
units readable underneath it while the player is still steering.

Additive plus **fresnel** is the whole trick. The surface facing the camera contributes
almost nothing, so the army is never hidden; the turning edge lights up and describes the
sphere. A dome that filled in would be worse than no dome, because it would cover the one
thing the player is looking at.

Two details that are load-bearing:

- **`abs(dot(n, v))`, not `saturate`.** The pass is `Cull Off`, so the far half of the dome
  draws too, and its normals point away from the camera. `saturate` clamps that dot to 0
  and the back hemisphere comes out at *full* fresnel — a bright disc behind the army.
  `abs` mirrors the term so both shells glow at the rim and stay transparent through the
  middle.
- **Normals are set, not recalculated.** On a sphere the outward normal is just the
  normalised position. `RecalculateNormals` would skew the equator ring, because it only
  has geometry on one side — and that ring is exactly what the player sees edge-on, where
  the fresnel term does its most visible work.

The dome is sized from `CrowdMath`'s envelope every frame rather than from a constant: the
army grows from a handful of units to a few hundred, and a fixed shell would swallow the
small crowd and clip through the large one.

## The spell needed a middle

Casting used to be a cause with no middle — you flicked, and packs stopped existing. Now a
**bolt** leaves the army, travels, and detonates at the spell's *actual* clear range, so
the player learns how far the flick reaches by watching it rather than by dying to a pack
one metre outside it. In the boss fight the same bolt detonates on the boss, which is the
difference between a stat change and a hit.

`VfxSystem` fires the wall and the embers **on arrival, from inside the bolt's own update**
rather than from the caller, so the detonation cannot drift away from where the bolt
actually landed. The bolt is stretched along its travel direction — a cube reads as a
floating box, a cube stretched into its velocity reads as something moving fast.

A pool entry is born with `Detonated = true`, which doubles as "inactive". Without that a
never-fired bolt has `Life = 0`, the update divides age by zero, clamps to 1, and "arrives"
at the world origin on the first frame of the game.

## A natural experiment for the missing shockwaves

The shockwave walls have now failed to appear in two rounds of screenshots while the debris
motes on the same material rendered fine (measured at `(87,122,220)` against a `(30,30,45)`
road). Rather than guess a third time, this build is arranged so the next screenshot
discriminates on its own:

| what shows | what it means |
|---|---|
| bolt + dome + wall | everything works |
| bolt only | the two hand-built meshes (`ShockWall`, `Dome`) are the problem, not the material |
| nothing | the additive material is not resolving on device at all |

The bolt uses `ProceduralMeshes.Cube` — the same mesh the working motes use — so it is the
control in the experiment.

## The army was a field of crosses, and the geometry says exactly why

`BuildUnit` was a 0.34-wide torso with pauldrons jutting to ±0.24 **at head height**, and
**no legs at all** — one box from the ground to the shoulders. That is a plus sign with a
head on it, and at 0.47 scale and 26 m, where a unit is about ten pixels tall, the
silhouette is the only thing that survives.

The rebuild fixes both halves. **Separated legs** with a real gap between them — the single
strongest cue that an outline is a person — and pauldrons pulled in to ±0.19 and dropped to
the shoulder line, so the profile tapers from a wide base to a narrow head instead of
spreading into a T.

**Four archetypes** — spear, shield, axe, banner — assigned from a hash of the slot index,
so a soldier keeps his identity as the army grows around him rather than the whole crowd
reshuffling its weapons every time a gate is passed. Banners are deliberately rare, one in
eight: a banner over every fourth man is a parade, a banner here and there over a mass of
spears is an army. This costs four instanced draws instead of one, which is nothing.

The two archetypes that carry something **above the head** do most of the work. At ten
pixels tall, anything at chest height is invisible; only what breaks the skyline reads. A
banner-bearer stands 0.68 m against a spearman's 0.57 and the old unit's 0.45.

## A march, not a bob

The army slid along at 10 m/s with its feet welded together and a vertical wobble for
motion — most of why it read as objects being carried rather than soldiers marching.

Legs now rotate about a hip pivot at y = 0.30, the two sides in antiphase because the swing
is multiplied by `sign(x)`. Anything on the midline has `sign() == 0` and does not move, so
the effect selects limbs by itself: no bone weights, no skinning, no extra vertex channel,
and it happens in the same macro the shadow pass uses so shadows stay welded to the feet.

Two gates make it safe:

- **`_BobAmount` scales it**, and that is already 0 on every non-crowd material — gates,
  rails, road markings, the finish line and the boss are untouched.
- **An x-band as well as a height test.** Legs sit at |x| = 0.105; every weapon starts at
  |x| ≥ 0.23. Without the band a spear butt — which hangs to y = 0.10, well under the hip —
  would swing with the right leg while its head stayed put, and the shaft would visibly
  **bend at the hip line**. The x test is what makes the swing mean "legs" rather than
  "everything low".

`_ToneSpread` breaks up the armour tone per unit off the same instance phase, and is **off
by default**: the phase is decoded from instance scale, and every other object on this
shader sits far outside the crowd's 0.44–0.50 window, so their phase pins to 1.0 and they
would all have silently brightened by 30%.

## Deliberately not done yet

Nothing in the render stack. The remaining gaps are content and production: real art in
place of greybox meshes, audio, and the boss roster beyond two.

## Verifying a look change from a container

Nothing here can be seen from CI. What CI *does* prove is that the shaders compile for
Android and the C# builds — which is most of the risk, because a shader typo is a magenta
build and a wrong URP property is a compile error. Everything past that needs a screenshot
from a real device.
