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

## Deliberately not done yet

Gates as real portals, camera juice and VFX. That is stage 3.

## Verifying a look change from a container

Nothing here can be seen from CI. What CI *does* prove is that the shaders compile for
Android and the C# builds — which is most of the risk, because a shader typo is a magenta
build and a wrong URP property is a compile error. Everything past that needs a screenshot
from a real device.
