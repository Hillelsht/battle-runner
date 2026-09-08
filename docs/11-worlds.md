# 11 — Worlds: acts, themes, and why every round looked the same

## The complaint, measured

"Every round the doors are the same, the pavement is the same and the background is the same."

Sampling nine device frames from v0.12.0 settles it without argument:

| Sample | Value | Frames |
|---|---|---|
| Sky above the horizon | `(11, 10, 15) ± 2` | all nine |
| Road in front of the camera | `(25–29, 25–31, 37–46)` | all eight gameplay frames |
| Either side of the road | `(10, 8, 12)` | all nine |

Two different levels and two different bosses produced one sky, one road, and — beside the
road — nothing at all. That third row is the important one: there was no background to be
tired of, only an unlit plane and then void.

The causes were all structural, not artistic:

- `EnvironmentLook.Apply()` is a `static` with no parameters, called once from
  `GameBootstrap.Awake` **before a level exists**. Nothing downstream could vary it.
- `TrackController.Initialize` builds the road, rail, marking and finish materials once from
  constants. `BuildLevel` never touches them.
- `SpawnGroundStrip` emits a ground box, four lane lines, two rails and rung decals. Nothing
  is placed beyond `x = ±4.16 m`.
- `ContentFactory.BuildChunksForLevel` is a formula with **three** outcomes: an add gate at
  12 m, another at 28 m, and on every third chunk a `×2` opposite a `−N` at 40 m. Every chunk
  in the game is one of those three.
- `LevelFor(i)` and `BossFor(i)` both wrap modulo 6 against arrays of length 6, so the
  level↔boss pairing never changed. (A note in v0.12.0 called these "two coprime-ish cycles".
  They were the same cycle.)

## The shape: acts

An **act** is 3–5 rounds that share a world. `Core/Progression/RoundPlan.cs` derives everything
about a round from its index, in closed form, with no saved state:

```
r 1  act 0 (1/2)  world 0  boss 0  threat   12 chunks
r 2  act 0 (2/2)  world 0  boss 0  FIGHT    14 chunks
r 3  act 1 (1/3)  world 1  boss 5  threat   13 chunks
…
r 9  act 2 (4/4)  world 2  boss 4  FIGHT    16 chunks
```

The opening act is deliberately two rounds and is a **one-off**, not part of the repeating
cycle. The tutorial teaches the shield on a boss telegraph, so a long first act would leave a
new player minutes holding a verb they have never been shown — and folding a `2` into the
repeat would bring two-round boss gaps back around forever. From act one the cycle is
`{3,4,5,4}`, and a test asserts the gap between boss fights is only ever 3, 4 or 5.

**Worlds walk forward, bosses walk backward.** `ThemeSlot = act % 8`; `BossSlot = -act mod n`.
Stepping by `n−1` is stepping by `−1`, and `n−1` is coprime to `n` for every `n`, so this
visits every boss in a different order than the themes without hand-picking a stride per
roster size. The pairing repeats after `lcm(8, 6) = 24` acts, against exactly **1** before.

## The eight worlds

`Core/World/WorldThemes.cs`. Slot 0 reproduces the shipped palette exactly and is the anchor —
it is the one look that has been seen on a real screen and judged, so the other seven are
pushed away from it rather than invented beside it.

Ashen Road · Gallows Mire · Sunken Crypt · Ember Fields · Bone Wastes · Frozen Reach ·
Blood Marsh · Throne of Dust.

A world carries only values that were **already shader properties** — `Road.shader` exposes
eight, `DarkSky.shader` exposes ten — plus fog, ambient and the key light. No new shader work
was needed for any of it.

**What a world deliberately does not touch:** the gate colours and the enemy tint. Blue means
gain, red means loss, and the player reads that in the quarter-second before a gate arrives.
Repainting semantics per world would trade a gameplay signal for decoration.

### The fog coupling, corrected

`docs/10-look.md` records the fog colour as `_HorizonColor + ~0.76 × _GlowColor`. Solving the
shipped values per channel gives **r = 0.710, g = 0.697, b = 0.457** — red and green fit a
single factor of 0.70 to within 0.005; blue does not, because the shipped fog is deliberately
warmer than the rule. So fog is *authored* per world and `WorldTheme.FogDrift` is the guard:
a test refuses any world whose red or green wanders more than 0.02 from the sky-derived value,
which is what would draw a visible seam where the road meets the sky. Blue stays free.

## Rounds inside an act

Eight worlds over an endless game is still a loop if every round in an act is identical.
`Core/Progression/ThemeVariant.cs` shifts each round's palette hue, fog depth, star strength,
key-light azimuth, roadside density and road wetness, hashed from the round index so a round
looks the same every time it is played.

The amplitude **ramps across the act** — about a third at the first round, full at the last.
That is not decoration: a world is introduced before it is bent, so the player sees what the
place actually looks like, and the drift then reads as going deeper into it.

The hash is unsigned integer arithmetic and a test pins exact expected outputs, because float
maths that agrees under .NET and disagrees under Mono has already cost this project one CI
failure (`Talents.Executes`, where `100f * 0.08f` landed a hair below 8).

## Applying a world

`RunLoadingState.Enter` is the only per-round moment that holds the round index while nothing
is on screen, and the index is stable from there until `StatUpgradeState` advances it. The
world is dressed *before* the road is built, so the first frame of a round is already the right
place rather than the last one repainted.

Three things had to change to make that possible:

1. **`Resources/DarkSky.mat` and `Resources/Road.mat` are now instanced, not used by
   reference.** Retinting the loaded asset would have written into the committed `.mat` file on
   disk every time the editor played a round.
2. **The key light is kept in a field.** `ApplyKeyLight` created the directional and dropped
   the reference, so there was no way to reach it again for the life of the app.
3. **The ground strip runs 200 m past the finish**, up from 180. That 180 was chosen to clear a
   fog end fixed at 170; worlds now choose their own weather up to `WorldThemes.MaxFogEnd`, so
   the road has to outrun that instead. 200 m past the finish is ~210 m from the camera, still
   inside the 220 m far clip, so the seam is buried in fog rather than sliced by the clip plane.

**The fog MODE is not themed, on purpose.** Unity's Automatic variant stripping keeps a `FOG_*`
variant only if a scene enables that mode in its lighting settings, and `Main.unity` declares
Linear. A world that switched to exponential would compile clean, look right in the editor, and
have no fog at all on device — which is precisely how fog shipped broken once already.

## Round length

Rounds were `5 + min(3, levelIndex)` chunks of 45 m — 245 m rising to 380 m and then capped at
8 chunks **forever**, 24–38 seconds. `RoundPlan` designs 12–20 chunks, but `ContentFactory`
clamps to 12 for now: tripling a round's length while every chunk is still one of three layouts
makes the repetition worse, not better. The cap is deleted, not raised, when the chunk
archetypes land.

Object pools moved with it — 40 gates and 24 packs, up from 14 and 20. Doc 04 forbids mid-run
instantiation and `ObjectPool.Get` silently creates on an empty pool, so prewarm has to track
round length every time it changes.
