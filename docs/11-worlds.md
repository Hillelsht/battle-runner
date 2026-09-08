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

## The boss stops being a formality

`RunnerLoopState.OnFinishReached` used to transition unconditionally into `BossState`. Every
round ended in a fight, which meant the sixth Bone Colossus of the evening carried exactly as
much weight as the first.

A fight is now the **last round of an act**. On every other round the act's boss comes to the
finish line anyway: `BossThreatState` shows it, ramps its telegraph through the same
`BossView.SetTelegraph` the fight uses, roars — two shockwave rings, camera trauma, an FOV
punch — and lets the player past. About two and a half seconds.

**It closes in.** The standoff is `40 − 7 × ThreatStep` metres, floored at 17, and the roar's
trauma, ring size and mote count all scale with `ThreatStep / (ActLength − 1)`. So an act of
three and an act of five both build to the same peak, and the last threat before the fight is
close enough to read every horn.

Two details that would otherwise have been bugs:

- **`GameConfig.BossFor` now resolves through `RoundPlan.BossSlot(plan.ActIndex, …)`**, not the
  raw round index. The creature that threatens on round two has to be the one that swings on
  round four; picking per round would have shown a different monster each time and made the
  whole build-up meaningless.
- **The threat state is deliberately not wired to `TutorialCoach`.** The coach arms its shield
  lesson on the first telegraph it sees, and a threat telegraphs without ever landing a blow —
  it would have taught "flick down to block" against an attack that cannot arrive, and asked a
  finished run to hold while it did.

## Every round pays, and the fight is the payday

Making bosses rare without touching rewards would have cut talent income roughly **fourfold**,
silently, because `BossEncounterState` was the only thing that ever granted stat points and the
tree was sized against per-round income. A player would have reached round thirty with a quarter
of what the tree expects and concluded the tree was broken.

`Core/Progression/RoundRewards.cs` holds the curve, and `LegacyIncome` deliberately preserves
the old one — `perBossKill + roundIndex / 2`, every round — so a test compares against the real
former behaviour rather than a remembered number. Across sixty acts, no act pays less than the
same rounds used to.

The *shape* changes even though the total does not: a normal round pays a little, a boss round
pays more than three normal ones plus a bonus for how long the act made you wait. Three rounds
of small change and then a windfall reads as a reward; four equal payments read as a salary.
Loot rolls at reduced luck on a threat round under a different header ("SPOILS OF THE ROAD"),
so the fight stays the thing worth reaching.

The award moved out of `BossEncounterState` entirely — `LootPhaseState` now runs after both
endings and is the single place points are granted, because two award sites would have paid a
boss round twice.

## Knowing where you are

The HUD had four text elements — force, spell cooldown, shield cooldown, boss name — and the
level name appeared only on the main menu, which is the one place it does not matter. There is
now a marker at the top left reading `3-2  THE BONE WASTES`, or on the last round of an act
`3-4  HOLLOW LEECH AWAITS`. The build-up only works if the player can see how many rounds are
left before the fight.

The main menu also shows the **world's** name now rather than the level asset's: an act wears
one world for three to five rounds while the level list cycles on its own period, so the level
name there would have announced somewhere the player was not going.

## Eight things a road can ask

The old generator had **three outcomes**. `BuildChunksForLevel` placed an add gate at 12 m,
another at 28 m, and on every third chunk a `×2` opposite a `−N` at 40 m, with one enemy pack
always in lane 0. Every chunk in the game was one of those three, cycling forever. That is the
literal reason the report was "every round the doors are the same" — it was not an impression,
it was a formula with three branches.

`Core/Run/ChunkLayouts.cs` now has eight shapes, each asking something different rather than
posing the same question in a new order:

| Shape | What it asks |
|---|---|
| **Ladder** | Add gates one lane apart — steer continuously, don't pick once and hold |
| **Fork** | A `×2` with a heavy `−N` in both other lanes. Commit before you can read it |
| **Gauntlet** | Three packs, no gates. Steering with nothing to gain |
| **Minefield** | Subtract in two lanes, the reward in the third, and it isn't announced |
| **Toll** | Every lane costs. The only decision left is which loss is cheapest |
| **Vault** | The prize and the price share a lane — taking it means paying for it |
| **Breather** | One gate, wide open. It is what makes the rest read |
| **Crossfire** | Packs in alternating outer lanes with a gate between them |

A round is a sequence chosen from its index under three rules, each of which exists because
breaking it makes a round read badly: **no shape twice in a row**, **the opening chunk is always
readable** (Ladder or Breather — starting on a Toll is a round that begins by taking something
away), and **at least one Breather in the back half** so a long round has somewhere to exhale.

Spacing is the one hard constraint. At 10 m/s a 45 m chunk is 4.5 seconds, and the comment that
survives from the original generator is that decisions 0.2 s apart are unreadable. Nothing is
placed closer than 12 m to the next separate decision — gates sharing a Z are *one* decision,
"pick a lane" — and a test walks every shape at every difficulty to prove it.

The road is now built from these per round rather than from six ScriptableObject levels cycled
forever, which is why round eight used to replay round two's gates. `ContentFactory` still
materialises chunks for the editor, but it delegates to the same generator: content a designer
opens has to match what the game actually plays.

With variety in place the round-length clamp is gone. Rounds run the designed 12–22 chunks,
540–990 m, about 54 to 99 seconds. Pools are prewarmed from `RoundPlan.MaxChunkCount` times the
per-shape maxima rather than a hand-picked number that would go stale the next time a shape is
added.

### Par force had to be rebuilt, and this one was a real bug

`ParForceAtFinish` sizes the revive a player is handed after watching an ad. The old estimate
walked the optimistic line — every add and multiply hit — and multiplied them all together. That
was stable when the generator produced exactly one `×2` every three chunks. Against varied
layouts it swung by two orders of magnitude on nothing but how many multiplies a round happened
to roll:

```
round  2:  mult 2   par    297
round  5:  mult 6   par 12,533
round 20:  mult 8   par 60,000   (pinned at the soft cap)
round 30:  mult 7   par 60,000   (pinned)
round 45:  mult 3   par 16,455
```

A player reviving on round 2 would have come back with 99 units and on round 5 with 4,177.

The adds are the stable backbone — their count and value track depth smoothly — so they are
banked in full, and the multiplies then lift the result **logarithmically in their count**
rather than multiplicatively in their values. That is also closer to the truth: three lanes
cannot all be taken, so the tenth multiply in a round is worth much less than the first. It is
the same log-shaped damping `GateMath.OverflowToBonusMultiplier` already uses.

```
round  0: par   337      round 20: par 2,672
round  5: par   797      round 45: par 5,733
round 10: par   889      round 60: par 8,258
```

Par is also computed per **round** now and lives on `GameContext.CurrentPar`, not on the level
asset — a level asset cannot know which round is being played.
