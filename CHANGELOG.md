# Changelog

Current status of the project and what shipped in each build. Newest first.

Releases: <https://github.com/Hillelsht/battle-runner/releases>

---

## Current status

**Stage:** playable greybox MVP. The full loop runs on device: menu → 3-lane run
through `+` `×` `−` gates → telegraphed boss fight → loot with Auto-Equip → stat
points → save → next level.

| Area | State |
|---|---|
| Game loop | Complete end to end |
| Content | 8 worlds, 6 levels, 6 bosses (6 archetypes) x 5 champion affixes = 30 fights, 15 gear items, 4 rarities, ~60 talents + endless paragon |
| Art | Procedural meshes and code-built uGUI, 119 CC0 Kenney models in one 563 KB pack, and 8 generated ground surfaces with normal maps |
| Tests | 411, green under both `dotnet test` and Unity's Test Runner |
| Android build | Automated: ARM64 / IL2CPP APK published to Releases |
| Monetization | Rewarded-ad and IAP flows wired to **mock** services only |
| Docs | Enforced — `tooling/check_docs.py` gates pushes locally and in CI |
| Audio | 15 cues in 21 files, 2 music beds played on real instruments and bent per world; mute toggle |
| Not started | Real ad SDK, analytics, battle pass |

**Confirmed on device:** v0.1.2 plays as a lane game. The crowd stays in its lane at
any size and the army reads as a column reaching up the road.

**Unreleased since v0.2.0:** an art pass on the lighting — HDR, bloom, tonemapping,
colour grading, a procedural night sky, trilight ambient, MSAA and real shadows, so the
army stands on the road instead of hovering over it — and a procedural cobbled road
with brick bonding, grime and a wet sheen, in place of the flat slab. The UI is
rebuilt on code-generated sprites too: rounded bevelled panels, a bronze frame with
corner notches, a gradient backdrop and readable disabled states, across every screen.
**The army is continuous now, and that is the largest single change the game has had.** The
report was *"every round the crowd shrinks to minimum... once you achieved a certain level or
crowd size you never lose it"*, and carrying it over turned out to be one line that could not
be shipped alone. Simulated against the real generator with the old absolute gates, an army
carried between rounds is pinned at the 100,000 soft cap by **round five** and every round
after it is a flat **1.00×** — every gate in the game doing nothing measurable. Below the cap
it is no better: a round entered with 2,461 men is worth **1.9×** against the 45× the same
round pays at five.

So a gate is a SHARE now — a recruit is ×1.026, an ambush ×0.875 at the top of a round and
×0.409 at the bottom of a deep one — the soft cap is gone, and force is a `double` because a
`long` overflows around round 52 at the measured growth of a competent player, which is inside
the sixteen authored acts. The `×2` arch did not survive and that was measured too: with
literal doubling, every lane-choice quality from 0.70 to 1.00 lands within one order of
magnitude of the same colossal number, because rally gates dominate everything else the player
does. It is ×1.30.

`StandingArmy` holds both halves of the promise: **Banked** carries unbroken between rounds,
**BestEver** is a high-water mark that only rises, and the floor is 55% of it. A disastrous
round costs up to 45% of a career; no sequence of them can put the player back at the
beginning. Measured across all 62 rounds of the campaign from the shipped code, break-even
sits at lane quality **0.72** — below it the army does not grow and the floor is what stops
that being a spiral; at 0.85 it reaches **574M by round 30 and 20.1T by round 62**.

**The boss is re-priced against the army that actually walks in**, through the same
`CrowdFactor` its damage is multiplied by, so the two cancel exactly: the fight lasts about as
long whether the player arrives with four hundred men or four hundred trillion. `BossSim.ExpectedForceAtAct`
is deleted — with a continuous army no modelled ladder can be fair to a player at 0.80 and one
at 0.90, who end eight orders of magnitude apart. Bases ×0.60 and pressures eased to
0.036–0.051; measured, the campaign runs 17s → 51s across sixteen acts with the affixes folded in.

**Two bugs behind "the spell doesn't destroy enemy packs and shield doesn't block against
them".** The spell swept 15 m from the crowd's CENTRE, and the crowd's front line already
stands up to 7 m ahead of that — so it reached about eight metres past the men, under a second
of road, hitting only what was already unavoidable. And it iterated enemy packs only, while
most of the red on the road is a subtract GATE. Both now act on ambushes of either kind, from
the army's front, at 34 m; a raised shield nullifies either and says so with a ring instead of
silently costing nothing.

**Both abilities are magazines.** `Core/Run/Magazine` refills whenever it is short rather than
only when empty, which is what makes a second charge worth a talent point. New `SpellCharges`
and `ShieldCharges` stats, bought by Full Quiver / Arsenal and Doubleguard / Triguard, with the
keystone payoff folded into the existing keystones rather than added as a fourth — three
mutually exclusive keystones per branch is a design rule with a test on it.

**And the affixes broke the window the moment they were tested.** The campaign window test was
written against the plain boss, which made it nearly worthless: `Colossal` multiplies health by
1.40 and the rotation lands one on act 15. Folding `BossAffixes.For(act)` in failed it at
**67.9 s** — a curve that passed comfortably and would have shipped a sixty-eight-second boss
fight. Pressures ease from 0.042–0.070 to 0.042–0.062 and the Hollow Leech's base from 1360 to
1320; because the pressure compounds on the act index this touches **only the late slope**, and
act 0 keeps every second of its new difficulty. The curve that ships, measured:
17s, 21, 24, 23, 26, 30, 22, 27, **45 (Colossal)**, 30, 36, 44, 28, 36, 44, **57 (Colossal)**.

**One plan item was dropped because measurement did not support it.** The plan claimed multiply
gates were mispriced — prize proportional to force, price linear in round. Modelled against the
generator with the multiplies compounding, the Fork toll is a near-constant **48.7% → 38.3%** of
force from round 0 to round 30, because `AddValue` and the banked force both scale linearly in
the round index and move together. No change made. That is the third measurement in this
increment to contradict the plan it was implementing.

**The music was the wrong genre, not badly played.** The second bed is eight bars of D natural
minor on a nylon guitar over a cello, correctly voiced, in a real room, with its chords verified
by FFT — and the report was still *"music is bad"*, the same words as after the first. It is a
**twenty-four-second ambient loop at roughly forty beats a minute, playing under a forty-second
sprint in which the player makes a lane decision every two seconds**. A score that slow does not
merely fail to support that; it works against it, because the tempo the ear is given is the tempo
the hands expect.

The run bed is now **eighty-seven seconds of D aeolian at 132 BPM** — a frame-drum and low-string
ostinato under a modal melody, in four sections: drums and bass alone, the tune, a breakdown on
solo guitar, and the return an octave up. Four sections rather than one loop twelve times,
because a four-bar figure repeated for ninety seconds is the thing the ear times and then stops
hearing. The bed loops and is never restarted per round, so the whole arrangement is heard across
successive rounds. The progression is i–VII–VI–VII: the VII pulls back to the tonic without the
leading-note pull of a harmonic-minor V, which the boss bed keeps.

**The drums are synthesised because the SoundFont reader resolves exactly one kit key.** Probed
across thirty-five keys of GeneralUser-GS's GM kit, only the crash sounds; every other key falls
outside the zone the reader picks. A membrane is four lines of arithmetic, and tuning it to the
key is something a sampled kit cannot do.

Measuring the render caught three things, two of them real bugs that read as correct in source:

- **The chords were never played.** `DRIVE_CHORDS` holds three voices a bar and the code used
  `chord[0]` and nothing else. An FFT of bar 0 heard a bare D–A fifth — neither major nor minor —
  and bar 3 had no E in it. The progression existed only in the comments.
- **The pulse was at half speed.** With the low drum on beats 1 and 3 only, the strongest
  periodicity was the half bar: the measured tempo came back at **66 BPM in a track written at
  132**, which is the original complaint reproduced exactly.
- **Then the groove went flat.** A stroke on every eighth at similar gains made the onset
  envelope uniform — beat, eighth and half bar all within 12% of each other. A hand drum is loud
  on one and quiet everywhere else, and that difference is the pattern.

And one measurement mistake of my own, recorded beside them: the first tempo check took the
**argmax** of the onset autocorrelation. In 4/4 the half bar always correlates strongly, so
argmax says nothing about whether the beat is there; the check now asks whether the one-beat lag
is a local peak.

This is the third attempt. If driving battle music is also wrong, the honest next step is a
reference track rather than a fourth guess.

**The game was too easy AND unfinishable, and both had one cause.** `BossSim.BossHp` compounded
its growth on the **round** index while a boss is fought once per **act** — and acts are three to
five rounds. At the authored 0.25–0.29 that is about **3.0× more health between consecutive
fights**, against a player who grows 1.2–1.7× and, once force hits the soft cap, **1.06×**.
Modelled with the shipped numbers: the first boss dies in **9 seconds**, and the act-10 boss
needs **two and a half hours**. No value of that growth number fixes both ends.

The player's curve changes shape, which is why no fixed exponent could ever track it: gear does
not scale with level (fifteen fixed items), talents are a finite tree, paragon is linear, and the
force reaching a boss doubles per act until `SoftCap` stops it dead. Growth per act runs 1.68,
1.50, 1.40, 1.33, 1.28, 1.25, 1.22, 1.20, then 1.06 forever.

So `BossHp` takes an **act** index and is priced against what the player provably has: the army
it will face, through the **same `CrowdFactor` its damage is multiplied by** at
`ExpectedForceAtAct` — so when the soft cap flattens the army it flattens the boss, automatically
and for the same reason — and the stat points the game has handed out, which the balance settings
already fix. **Gear and talents are deliberately excluded**: they are the player's edge, and
modelling them would price that reward away. `PerLevelGrowth` 0.25–0.29 becomes 0.05–0.07, which
is not the same quantity made smaller but a different quantity — a pressure of a few per cent per
act meaning "a little harder than the last one".

Measured from the shipped code for a player with **nothing but stat points**: 17.1 s at act 0
rising steadily to 48.5 s at act 15, against 9 s rising to 9,272 s.
`TheWholeCampaignStaysInsideAPlayableWindow` walks all sixteen acts and fails outside 5–60 s.

**The army was worth almost nothing.** `1 + log10(1 + force)` moves 2.79 → 6.00 across the entire
game, so the thing the player spends every second on paid 2.15× in total and a hundredfold army
paid 1.7×. The first replacement was a single power law and **the project's own
diminishing-returns test rejected it** — a single power law has a constant ratio between decades
and the `1 +` damps the small end, so the curve *accelerates*. The comment written beside it
claimed the opposite, plausibly and wrongly, and only the test knew. Two segments with the
exponent dropping at a two-thousand-man knee (0.40 below, 0.18 above) diminish by construction.
The weight is solved so the factor at sixty men is held at 2.80 against the old 2.79 — no early
fight quietly made easier — while the cap goes 6.00 → 15.80 and a hundredfold army pays **3.5×**.

**Blows can kill you now.** A blow took a fixed fraction of what is *left*, so the sequence
approaches zero without reaching it: **24 unblocked blows** to wipe ten thousand men, 30 for a
hundred thousand — a hundred seconds of ignoring every telegraph, in a fight lasting seventeen.
It was backwards in shape too, since a bigger army passively survived more. Blending a tenth of
the army that *walked in* into the basis costs **eight** blows and makes the count nearly
independent of army size: the army buys damage, Health and the shield buy survival. With
`referenceForce == force` the new overload is algebraically the old formula, so all five existing
`BossHit_*` tests are untouched rather than rewritten.

`BossHp_GrowsPerLevel` is **deliberately rewritten** and the old assertion is worth stating: it
pinned `BossHp(500, 0.25, 4) == 500 × 1.25⁴` and it passed. The quantity was right and the index
was wrong. What the test pinned was the bug, faithfully.

**A second gate for a second five-minute mistake.** `dotnet test` compiles `BattleRunner.Core`
and nothing else — the Gameplay, Meta, Data and Editor assemblies reference UnityEngine, so the
first compiler that ever sees them is the headless editor in CI. Two commits went out with a
`Matrix4x4[] into` declared in a method that already had a `float into` eighty lines below it in
the same block: `CS0128`, plus a `CS1503` cascade where `Mathf.Lerp` was handed an array. Both
obvious on sight; neither catchable by any gate that existed.

`tooling/check_local_shadowing.py` tracks brace depth inside method bodies and flags a local
whose name is already live in an enclosing or equal scope. Like the brace checker it is
deliberately not a parser and CI is still the compiler; it just stops the cheapest mistake from
costing a round trip. Verified against the real defect: run against the rejected commit it
names `'into' is already declared`, at the line the compiler named.

Writing it produced a false positive worth recording, being the same trap as the road work. The
first version also accepted a declaration ending in `)`, meaning to catch a parameter on a
continuation line — and caught them so well that every method *parameter* was recorded in the
class scope and never popped, so the next method to reuse a name was reported. A checker that
finds a problem is not the same as a checker that is right.

**The boss fight had no fighting in it.** The army's damage to a boss was `dps * dt`, applied
sixty times a second, writing nothing but the HUD bar. The only fight geometry in the whole
encounter was the boss's own one or two swings per attack cycle: the player could watch a bar
move and a boss lean, and could see nothing at all of the thing actually killing it. *"No visual
fighting with the boss"* and *"I want to see I smack him, he smack me back not only using spells
but every second"* are exact descriptions.

`Core/Boss/BossMelee` is the army's half, as arithmetic — deliberately **not** `Melee`, which is
pre-resolved: `Melee` is constructed knowing both sides' totals and the outcome and interpolates
toward it, and a boss fight has no such outcome at the moment it starts. Reusing it would have
meant either inventing a fake outcome to animate against or letting the animation contradict the
real force count.

- **The grind becomes volleys.** Same dps, same total, delivered on a 0.55 s beat with a flash, a
  sound, a burst and a line of men swinging. `SwingsBy` is stepped rather than a timer that
  resets, so the sum over a fight is exactly `dps × elapsed` to within the beat in flight —
  pinned by a test at four frame rates, because a presentation change that quietly alters the
  balance is a balance change wearing a disguise.
- **And he smacks back, on the same beat.** `ApplyMaul` is a small unblockable force loss with a
  body movement and a victim. Unblockable by design: a shield that answered it too would make
  the shield's real job — the telegraphed blow — unreadable. `BossSim.MaulBite` floors where
  `ApplyBossHit` ceilings, and a test pins that four thousand beats of attrition converge on the
  last man and never take him, because a fight lost to attrition alone is a fight the player was
  given no way to answer.
- **The line is a volley, not a shimmer.** Phases spread by the golden ratio over 35% of the
  cycle, so at any instant roughly 30% are closing, 45% trading and 25% falling back. A full
  spread is the same failure as unison from the other end — statistically busy, visually static —
  and there is a test for each.
- **A blow the boss lands takes somebody off the board.** They fall, and stay down; the line
  re-forms when it is spent.
- **`FlashHit` takes a strength.** Melee asks 0.30, the spell asks 1.0. At full strength forty
  volleys would pin `_EmissionFlat` near maximum for the whole fight and the spell would land on
  a shell already at full glow.
- **The arena closes from +16 m to +11**, with the army's press up from 3.2 to 4.5. At the old
  numbers the two sides never came within ten metres and there was physically nowhere for a melee
  to happen. `TheArmyPressesForwardAndIsDrivenBackByAnUnblockedBlow` is **deliberately rewritten**:
  its `< 3.3` bound guarded against an army walking into a boss at +16, and it now says the same
  thing against the arena that exists.
- **`AudioCue.BossHit` was declared and never played** — there was no event to hang it on. Its
  jitter goes 0.10 → 0.18 and its gap 0.05 → 0.18, or forty identical clicks a fight would read
  as a machine. Real variants are the honest next step.

It costs **zero extra draw calls**: a boss round has no enemy squads, so `SquadRenderer`'s
fighter bucket is idle for the whole encounter and the skirmish fills exactly that array.

One bug found by writing it down rather than by running it: `Tick` returns early once the fight
resolves, so the line would have frozen mid-swing at a collapsing boss for the whole victory beat
and the loot screen behind it. It is cleared on both resolution paths, not only in `Exit`.

**Two bugs made every red crowd look wrong, and both were one line.**

`CrowdInstanced.shader` recovers each body's walk phase from its uniform **scale** — there is no
second per-instance channel, only the matrix, so the 0.44–0.50 window is the entire animation
bus. `SquadRenderer` drew every body at a flat **0.47**, which is the exact centre of that
window, so the shader decoded phase 0.5 for all of them: an enemy squad marched as one
synchronised band with every left leg forward on the same frame. The player's own army has
looked right the whole time because `CrowdRenderer` varies it. `ScaleMin`/`ScaleSpan` are now
`public const` and the squads encode a real per-body phase through them (the CI coupling that
pins those declarations against the shader still matches).

And `enemyMaterial._BobAmount` was **0**, which is right for a squad standing in the road
waiting and wrong for the whole duration of a clash: the player's soldiers animated while the
red side was a rigid statue sliding along the road. A second material with the bob back on, and
a squad routes into it while it is fighting — **one extra instanced draw, only while a clash is
on screen**.

**`−N` gates are crowds now too.** The ask was "some join you, others attack you" and the first
pass shipped only half of it: `DrawAsCrowd = op == GateOp.Add`, so a −26 was still a door with a
number on it. A subtract gate is now men standing in the road who take that many of yours down
with them, in the enemy silhouette and the enemy red, and when the army reaches them they are
driven **backward** and scattered — each man his own distance and his own way, so the line comes
apart instead of sliding off as one piece. It runs on its own clock (`RoutSeconds` 0.85 against
`JoinSeconds` 0.55), because being overrun is the only moment in a run where the army visibly
pays for something and at 0.55 s it was over before the eye found it. The multiply gate stays an
arch: a ×3 is not a number of men.

**Eight worlds were painted three-quarters over with one colour each.** `VergeTint` sat at
0.60–0.78, meaning up to 78% of every roadside piece's albedo was replaced by a single
per-world stone. Worlds 1-1 and 2-1 share **zero** verge pieces and still arrived on device as
two flat greys, because whatever different thing was placed there was then lerped to within a
quarter of the same paint. *"Visuals per level have a tiny change"* is a precise description of
what 0.74 does.

The tint is now 0.26–0.34, and `TheVergeIsAlwaysGrimmerThanTheHorizon` is **deliberately
rewritten**: its `VergeTint > 0.5` floor — the assertion that made this the shipped state —
becomes a ceiling at 0.40. The ordering it also pinned was right and survives untouched, so
"bright landmarks, dark verge" still holds; a stump now just looks like wood while it holds.

The ten procedural props were the other half: painted the world's `VergeStone` outright with
nothing varying them per instance, ten silhouettes arriving as one flat mass at ~37% of the
verge population. Halved, with the imported verge raised by the same amount — as full a
roadside, more of it art that carries its own colour, and no extra draw calls because those
pieces were already in the palette.

**Nine landmarks served eight worlds; sixteen baked modules served none.** `Ruin` stood in five
worlds and `Mausoleum` in five, so worlds a whole act apart shared both their walls and their
only house — while the entire roof, corner and tower vocabulary had never been referenced by
anything. A landmark is a part list rather than a mesh, so eight more cost zero fetch, zero bake
and zero additional draw calls: a chapel's walls land in the same instancing bucket as a
cottage's. Chapel, stilt house, column hall, smithy, bone shrine, watchtower, gatehouse and
manor — one per world that nothing else uses, pinned by a new test, with nothing permitted to
stand in more than four of the eight.

Two existing tests did real work here: the stacking test refused a course placed at a guessed
height instead of a measured one, and the scale test caught the chapel at 5.3 m, which is why
it has a bell tower.

**The road was measured as a file and shipped as a slab, twice.** The generated cobble carries
a luminance range of 178 in the PNG and arrived on screen as 21 — flatter, with the grid
excluded, than the 19–42 the road had before any of the surface work began. Every number
previously reported about it was true of the input and irrelevant to the output.

Two things on top of the stone were doing the damage. **101–176 speed rungs**, a full-width
decal every 6 m on an emissive material measuring 1.6–2.2× the road's own luminance: that grid
*is* what the road kept reading as, and no surface can compete with a brighter thing drawn over
it every 6 m. They are deleted, along with the largest remaining draw-call item in the ground
strip. And **`_RimUpMask` was never set on the marking material** — the mask that exists to kill
the rim on up-facing normals was applied to the crowd only, with a comment claiming road decals
should keep the wide lobe "because their top face is their only lit surface". Backwards: the rim
peaks at *grazing* angles, which is the only angle a 2 cm ground decal is ever seen at. It is now
set on the marking, rail and finish materials. Lane lines stay — three-lane game — narrowed to
0.07 m.

**`tooling/predict_road.py` is the instrument that should have existed first.** An offline model
of `Road.shader` plus the key light, fog, grade and tonemapper, evaluated through the real camera
one ray per pixel onto the ground plane, so perspective minification is modelled rather than
assumed away. Its limits are written at the top of the file rather than buried: one fitted
parameter, a known systematic error in level, and therefore it reports **ratios against the
configuration whose true device numbers are known** instead of raw absolutes.

It contradicted the plan it was written to verify, which is the point of building it:

| lever | effect on the road's on-screen luminance range |
|---|---|
| `RoadStoneVariation` 0.53 → 1.00 (shader maximum) | **+2.5%** |
| `RoadMortarWidth` doubled | −0.2% |
| `RoadGrimeContrast` back to the old fixed clamp | **−34%** |
| `RoadTiling` halved (features twice as big) | **+16% range, +28% edge** |

A feature on the ground is exactly `1 / RoadTiling` metres across and one screen pixel covers
about 15 cm of road at 25 m. Three worlds were authored at 5.0, 7.0 and 9.0 — 20 cm, 14 cm and
11 cm features, at or below one pixel in the middle distance, where they can only average to
flat. `EveryRoadFeatureIsBigEnoughToSurviveTheTripToTheEye` now holds every world to a two-pixel
floor. The grime clamp, which was hard-coded in the shader and identical in all eight worlds,
becomes the per-world `RoadGrimeContrast`. The per-stone tone — the plan's headline lever — is
raised anyway because it reads in the first few metres, but it is recorded in the code as not
fixing anything.

The Blood Marsh needed a palette change none of that could substitute for: at 0.189 luma it was
the darkest road of the eight and every structural term landed inside a few per cent of black.
Its stone is up 45% and its key light 0.90 → 1.05. It is still the darkest road in the game.

Predicted against the v0.19.0 frames: stone-only range **21–24 → 30–34**, edge
**0.30–0.61 → 0.41–0.82**. A prediction is not a measurement and the next device screenshots
decide it — if they disagree, the model is wrong and gets fixed, not the other way round.

**The boss fight was a tableau, and now it is a fight.** The audit is blunt about what was
there: the boss was pinned at a fixed point, its rotation was written once at construction and
**never written again**, the army never moved, and the only thing that ever crossed the eleven
metre gap was a bolt the PLAYER fired. Its own attack produced no geometry at the boss's end at
all — every effect was centred on the crowd. Two objects facing each other, one of them changing
colour. *"it's just standing one close to another without animation"* is an exact description.

`Core/Feel/BossChoreography` is the body language as arithmetic: an idle that never fully
settles, a wind-up that **rears back and gathers** — pulling away is what makes the release read
as coming at you — a strike that is fast in and slow out because an attack that returns as fast
as it arrives has no weight, a recoil when the player's spell lands, and a collapse that falls
forward and sinks on a cubed curve instead of the mesh blinking out.

**And the army advances.** It presses forward as the fight goes on, flinches from a blow and is
driven back hard by one it did not block. Applied as an offset from the mark the encounter
parked it on, and unwound on exit — `AdvanceZ` moves the crowd permanently, so leaving without
putting the army back would carry the offset into the next round, and the one after that.

It is **not** a skeletal rig, and the docstring says so. The boss meshes are built from
contiguous box and prism blocks and could be sliced into limbs, but that is a much larger change
than the fight needed to stop being a tableau — a creature that rears, drives, recoils and falls
reads as fighting, and all of it is the body transform. The one property that is load-bearing on
fairness rather than looks is pinned by a test: the wind-up peaks **before** the blow, because
it is the only warning the player gets and a warning that arrives with the hit is not a warning.

**Enemy packs became squads, and the fight takes a second.** A pack was five frozen bodies with
a `-26` over them, in five separate MeshRenderers, and the frame the crowd's leading plane
touched it the whole thing was subtracted and released to the pool. Two complaints at once: a
number drawn over five men contradicts what it labels, and a fight one frame long cannot be
animated because there is nothing to animate.

A squad now has N soldiers in it, up to a display cap of 40, drawn through the same instanced
path as the player's army. `Core/Run/Melee` spreads the same `force -= cost` across 1.15
seconds, so every tuned difficulty number survives untouched while both sides visibly drain —
a test pins that the outcome is EXACTLY the subtraction it replaced, across every combination
of army and squad size.

The detachment that runs out to meet them is drawn by the same renderer rather than detached
from `CrowdController`. That was the obvious design and it is the wrong one: the crowd's
formation is the most load-bearing maths in the project, its width and tail invariants are
pinned by tests and its scale is CI-enforced against the shader that decodes the bob phase out
of it — and a soldier leaving the front rank of a five-hundred-strong blob leaves no visible
hole, so spending that risk buys nothing the player can see. It is also the largest draw-call
saving available: roughly twenty-five draws a level in five-body packs are now two.

**The doors became arches, and add gates became crowds.** "The doors with +-* look bad" was
fair: four thin bars and an infill plate is a DIAGRAM of a gate, not a thing standing in a road.
The plate alone was 77% of the gate's projected area, which is why a gate read as a solid
coloured rectangle with no visible frame at all. It is an arch now — thick piers, an
overhanging lintel, springers and a keystone — with no infill, because you run THROUGH it and
seeing the road on the far side is most of what makes it read as a gate.

And an ADD gate is not a door at all. A reinforcement is people, so the arch is hidden and the
gate draws `Value` allied soldiers standing in the lane, who break and run into the army when
it arrives, shrinking out on a cubed curve so they hold their ground and then go all at once.
Multiply keeps the arch: multiplication has no crowd metaphor, because there is no number of
men that IS "times three".

**Headcounts over the army and over every squad**, billboarded. The point is comparison — my
number against theirs in one glance, both attached to the thing they describe, which a readout
at the top of the screen cannot do. Every label in the game before this was pinned at a fixed
12 degree pitch on the assumption the camera never leaves -Z, which stopped being true the
moment the rig gained dynamic pitch and shake.

The army's number EASES rather than snapping, and that is a consequence of the melee: a clash
subtracts the force on the frame of contact, but the squad drains over the next second, so left
snapping the player would watch their own number fall instantly and the enemy's fall slowly —
which reads as the fight being decorative. It flatters every other change too; a x3 gate now
counts up instead of teleporting.

**The music is played rather than synthesised.** Reported from device, flatly: *"music is bad.
choose much better."* Correct, and the honest answer was not a better oscillator —
Karplus-Strong is a plucked string and a summed sine stack is a pad, but neither of them is a
cello. The beds are now PLAYED, on a General MIDI bank, by a SoundFont renderer written for this
project (`tooling/sf2.py`): preset and instrument zone resolution, key ranges, sample loops,
tuning and the volume envelope. Verified by autocorrelation — nylon guitar, cello, strings,
choir, harp, double bass and oboe all land within 7 cents of equal temperament.

GeneralUser GS's licence covers exactly this: "without restriction ... private or commercial",
and the samples explicitly allow "musical recordings created with GeneralUser GS". It is a
BUILD-TIME dependency only — 32 MB into the already-git-ignored tooling/.cache, never committed.
Playing it at runtime would mean a native .so per ABI, 30 MB resident and +32 MB of install size
on a title where install size is the most conversion-sensitive number there is. Rendering
offline costs none of that and the device still sees two WAV files.

The arrangement follows the reference rather than the orchestra: a nylon guitar carries the
arpeggio over a bowed cello, slow strings underneath, a choir barely there, timpani on the bar.
The boss bed swaps guitar for a string section and bass for double bass.

THE BALANCE WAS MEASURED. The first render put 43% of the bed's energy under 160 Hz and 8% in
the guitar's own register — the cello and timpani were burying the only line that moves. It now
reads 17/67/14 across bass, low-mid and mid. The boss bed had it in reverse: normalising to a
peak set by timpani transients left it at 0.087 rms against the ambient bed's 0.131, a fight
layer QUIETER than the calm one. It sits at 0.114 now, 59% of its weight in the bass.

TWO MORE CHORDS WERE WRONG, caught the same way as the first three: loot_reveal's docstring said
D major and it played E major, gate_multiply said D minor and played E minor — both off by a
whole tone, both invisible in review, both found by measuring the rendered file against the note
names its own comment claimed.

**The music plays notes now.** The bed was a stack of detuned sine drones, filtered noise for
wind and a slow amplitude swell — correctly described as *"just a noize, not a music, like an
ocean sound"*, and that description was accurate, because it contained none. It was ambience.

It is now twenty-four seconds of **D natural minor**: eight bars of i-i-VI-VI-iv-iv-v-v,
four arpeggiated Karplus-Strong plucked voices per bar over a bowed root, with a frame drum on
the bar and the half-bar, in a Schroeder hall. The boss bed is the same length, key and room so
the two cross-fade without a key change, and tightens to i-VI-iv-**V** — that raised third is
the one interval in the key that genuinely wants to resolve, and it is why the ambient bed's
`v` deliberately stays minor.

**The loop wraps its tail instead of cross-fading.** An equal-power cross-fade is right for
ambience and wrong for music: it overlaps the last bar with the first, so two different chords
sound at once for a second and a half, once per loop, forever. Each bed is rendered longer than
its musical length and the overhang is ADDED BACK at the head, which is where that sound belongs
when the loop comes round. Anything that never decays — the choir pad and its tremolo — is
snapped to whole cycles over the loop body, since those are the only things that can put a step
at the wrap.

**Three chords were wrong and an FFT is what caught it.** A wrong interval does not throw and
does not sound obviously broken; it changes the MODE. `Gm` was written `(-7, -3, 0, 5)` — a B
natural, so G MAJOR — and both A chords carried a C sharp, which is A major and therefore
exactly the harmonic-minor leading note the ambient bed is documented as avoiding. The G minor
bar's strongest partial sat at 370.5 Hz, and 370.5 is F sharp. Every bar's strongest partials
are chord tones now.

**The effects got a middle and a place.** Every cue was one waveform times one envelope, which
is why "too simple" was fair: a real impact is a noise transient of a few milliseconds, a body
that carries the pitch, and a tail that carries the size, and one envelope over all three makes
them decay together — which is what makes a sound read as synthetic. A gate subtract is now
grit gone in 30 ms, a swept body in 110, and a rumble carrying on for a third of a second.

`room()` sends a little of each cue through the SAME reverb the music uses. That is the largest
single change to how the effects sound and it is not an effect, it is a place — fifteen dry
one-shots over a bed with a hall on it is a soundboard triggered next to a score. Not applied
to the cues that fire several times a second: a tail on a sound heard six times in a second is
mud, not depth.

**And the four cues heard most now have three recordings each.** A gate add fires several
hundred times in a run and no amount of pitch jitter stops one waveform heard that often from
flattening into a beep. They are separate SYNTHESES, not one sample retuned — the three gate
adds measure 874.8, 880.2 and 885.6 Hz with an RMS difference larger than the signal's own RMS.

**The ground has a surface, and the scenery has somewhere to be.** Measured against the two
photoreal references supplied as the bar, colour and saturation already matched (87-108
distinct colours against 82; saturation 0.40-0.74 against 0.56). The gap was **detail
density**: 0.81-1.04 edge density against 1.21, and a near road whose luminance ran 19-42 out
of 255 against the reference's 54-139. `Road.shader` was computing a brick bond, mortar,
per-stone tone and two octaves of grime *per pixel* and still landing at 2-5% contrast, which
is why it read as a flat slab.

It already computed `uv = positionWS.xz * _Tiling` — a real UV — so a texture could be sampled
with no mesh UV channel and no change to any mesh, and because the road and the terrain band
are horizontal axis-aligned planes their tangent frame is a compile-time constant, so normal
mapping works without mesh tangents either. `tooling/gen_surfaces.py` bakes eight seamless
surfaces carrying structure rather than colour (R tone, G face mask, B wetness, A height), so
eight tuned palettes survive and gain detail. **No shader keywords**: Road is already 128
forward variants and a six-way surface keyword would make it 768, all of which compile on the
device — eight worlds bind eight different *textures* into one variant. Measured end to end
with each world's real palette: **range 29-63 and edge 1.33-3.99, against 19-42 and 0.81-1.04
shipped.** Every world is past the reference town's ground density.

Two regressions the numbers caught, neither of which would have thrown. The normal map's green
channel was **inverted** — PNG row 0 is the top row and Unity's texture origin is the bottom
left, so every bump would have lit as a dent. And Throne of Dust measured *worse* than the road
it replaced (range 15, edge 0.66): its mosaic multiplied the face mask by a "half lost" field
and that world deliberately puts gold in its seams, so a third of the road came out gold. A
worn tessera is duller and lower, not a seam.

Also re-authored: `RoadTiling` had been tuned against one procedural cobble grid, so 1.2 wind
ripples per metre made each ripple most of a metre across; and `RoadMortarWidth` was declared
and **never once overridden**, so the joint pattern was byte-identical in all eight worlds.

**The scenery clusters into settlements.** It was placed by walking Z and dropping a piece
every so many metres — a uniform field, which is what texture looks like rather than what a
place looks like. Each side now walks its own sequence of hamlets and a third are paired
across the road. Three things were wrong first and were fixed by simulation: alternating sides
down one sequence halved the count per verge (four hamlets, **79% open country**); `GapRadii`
1.35 then swung it to **31% open**, a continuous village with gaps in it; and rejection
sampling delivered 30-40% *fewer* pieces than the scatter it replaced, which is a thinner world
rather than a rearranged one. At 2.6 and an oversample of 2.2 it is 40-47% open with the total
within 1% of before — and **the busiest quarter of the road holds 42-54% of the field pieces
against the 25% an even scatter puts there by definition.**

Landmarks now stand *inside* those settlements, and their clamp moved from the centre to the
**edge**: a keep's footprint is 11 m, so clamping its centre outside 26 m meant it could never
stand closer than 37 m, and in a world that fogs out at 105 m the largest structures in the
game were silhouettes. The same keep now stands at 19 m.

**The roadside was a hole in the frame.** 45-70% of a frame sat below 12% luminance against
36-43% for the references; the procedural props are painted `PropStone` outright and three
worlds authored one below 0.17 luma. The road has carried this invariant at 0.12 since the
lighting pass and the roadside never did. The lift *scales* rather than adding a constant, so
a world's hue survives. Two knobs that existed and nothing ever wrote are now themed too:
`Terrain._Gloss` (ice and dry ash caught the key light identically in all eight worlds) and
`WhiteBalance`, which was not in the post stack at all and costs nothing per pixel because it
folds into the grading LUT.

**The army stopped being a field of crosses.** `BuildUnit` was a torso box with pauldrons
jutting to ±0.24 at head height and NO LEGS — one box from the ground to the shoulders,
which is a plus sign with a head on it. Now: separated legs, pauldrons in to ±0.19 and down
to the shoulder line, and four archetypes (spear, shield, axe, banner) assigned by a hash of
the slot index so a soldier keeps his identity as the army grows. Banners are one in eight,
because a banner over every fourth man is a parade. Four instanced draws instead of one.

**And it marches.** Legs rotate about a hip pivot, the two sides in antiphase via `sign(x)`,
in the same macro the shadow pass uses so shadows stay welded to the feet. Gated on
`_BobAmount` (already 0 everywhere but the crowd) and on an x-band — without the band a
spear butt hangs below the hip and the shaft would visibly bend as the right leg swung.

**The shield is an actual barrier now.** The player reported no shield effect; the
screenshots show `ShieldWard` firing exactly as built, army lit cyan and `SHIELDED` in the
HUD. Both are true — recolouring the army reads as the army changing colour, not as a
shield. `ShieldDome` draws the shell: an additive hemisphere with a fresnel rim, so the
surface facing you contributes almost nothing and the army stays readable through it while
the turning edge describes the sphere. Sized from the crowd's envelope every frame, pops in
oversized and settles, and takes a hard flash when it eats a blow. The ward stays as the
secondary cue.

**The spell has a middle.** Casting was a cause with no middle: you flicked and packs
stopped existing. A bolt now leaves the army, travels, and detonates at the spell's actual
clear range — and on the boss during an encounter. The wall and embers fire from inside the
bolt's own update on the frame it arrives, so the detonation cannot drift from where the
bolt landed.

**The shockwaves never drew, and the screenshots proved it by measurement.** In v0.9.0 the
debris motes sampled at (83, 122, 217) against a road of ~(30, 30, 45) — clearly present —
while a scan across the road at four depths found no ring crest in any frame. Same
material, same shader, same draw path, so the fault was specific to the ring, and only two
things were unique to it: it was the ONLY mesh in the project carrying UVs (and the shader
shaped its falloff from `uv.x`, so a channel that never arrived means `sin(0)` and nothing
drawn), and it was flat, giving it zero-extent bounds and an almost edge-on view from a
camera 5.5 m up. The shape is now an open cylinder wall that expands and flattens, which
retires both: no UVs anywhere (the falloff comes from object-space height), real bounds,
and it faces the camera. Shock lives also went up ~35% — at a third of a second they were
hard to read and nearly impossible to catch in a screenshot, which is the only oracle here.

**Additive VFX — the game finally reacts to itself.** Until now nothing HAPPENED when you
passed a gate: the number changed, the camera nudged, and that was the whole event, in a
game whose appeal is the moment the crowd doubles. Six effects now, all from one shader:
a shockwave at every gate tinted and sized by the operation, debris off the army when a
pack bites, a ring that sprints out to the spell's actual clear range, embers off the boss
on a hit, a bright ring when the shield eats a blow, and a three-wave death beat with
forty motes when the boss falls.

The tree's scroll column carries a fully transparent `Image` on its viewport purely as a
raycast target. Without a Graphic the `ScrollRect` is not hit-testable, so only drags that
began on a child button reached it — and on a column of sixty nodes the gaps between cells
are most of the screen, which made the list read as stuck rather than as fussy.

**The game had no sound. Not placeholder audio — none.** No AudioSource, AudioClip or
PlayOneShot anywhere in the codebase, no audio assets, no AudioManager.asset, and no
AudioListener: Main.unity has one GameObject and no camera, and the runtime camera never added
one, so anything the game might have played would have played to nobody.

Fifteen cues and two music beds, all SYNTHESISED. No free audio host is reachable from the
build container — Freesound, OpenGameArt and kenney.nl all fail to connect, and the Kenney
mirror carries no audio — and synthesis is what this project already does everywhere else: the
meshes, the sky, the road and every UI sprite are generated rather than imported.
`tooling/synth_audio.py` needs numpy and the standard library only; ffmpeg, sox and scipy are
none of them present. 2.07 MB of 22 kHz mono WAV in the repo, about 200 KB of Vorbis on device.

Three details that are not cosmetic. Sweeps INTEGRATE the frequency, because
`sin(2*pi*f(t)*t)` produces an audible artefact and `sin(2*pi*integral f)` does not. Every
one-shot now ends at silence — measured, `shield_raise` ended at 0.46 amplitude, which is a
step discontinuity and therefore a click on EVERY block. And the beds cross-fade head-to-tail
with EQUAL power, because a linear cross-fade of two uncorrelated signals dips about 3 dB in
the middle and the loop point becomes an audible dropout instead of an audible click.

Sixteen pooled voices, doc 04's own number, prewarmed and never allocated during a run. They
live under a DontDestroyOnLoad root and NOT under ArenaRoot, which CreateArena deactivates —
an AudioSource on an inactive GameObject does not play, so that would have made the menus
silent and delayed the first round's music by a whole round. Eviction is by PRIORITY, not age:
a boss blow lost because the army was passing gates is a fairness problem, and a test pins
that no gate, bite or UI cue can evict one.

Sound fires from EVENTS, not inputs. ShieldSystem.Raised rather than the flick handler,
because TryRaise silently refuses on cooldown and a sound on a shield that did not go up
teaches the player the flick worked. The boss telegraph fires on the one frame the wind-up
window opens rather than in the per-frame telegraph update, which would retrigger it sixty
times a second and turn the only warning the player gets into a drone. The UI tap goes inside
UiFactory.ActionButton, the single place every button in the game is built.

**Eight worlds share one bed.** A twenty-second bed is most of a megabyte and eight would
outweigh the rest of the project, so `MusicMood` bends one — the same "one asset, themed" move
the sky and the road make. It derives from two things the player can already SEE: how far they
can see (fog drives a low-pass, so the Sunken Crypt sounds muffled and the Bone Wastes open)
and how cold the sky is (the zenith's blue-to-red balance drives pitch, so the Frozen Reach
sits above Ember Fields). Tests pin both pairs, require all eight to differ, and prove the
derivation never leans on its own clamps.

The mute preference is in PlayerPrefs, deliberately: PlayerProfile is per save SLOT and
SaveProfile refuses to write while no slot is active, so a preference there would be
unwritable on exactly the screens that offer the button. UiFactory has no Toggle and no
Slider, so the main menu gets a label-swapping ActionButton — the arm/disarm pattern the slot
picker and the skill tree already use.

Core names every clip; the Python decides which files exist; nothing at compile time connects
them, and a drift on either side produces silence and a warning nobody reads. The YAML lint
now pins the two tables against each other and against the files on disk, beside the crowd
scale coupling it already pins. Verified against a deliberate one-character drift.

Also fixed: `check_asmdef_refs.py` matched any MEMBER called `Volume` — `mix.Volume` on a
plain struct — and demanded a URP assembly reference the file does not need. It now ignores
member access while still catching `Rendering.Volume`, verified by removing the real reference
and watching it fail.

**Castles, farms, mills and crypts, from 119 CC0 Kenney models.** The verge was ten
procedural meshes, none taller than 1.36 units, in one grey per world, in two fixed bands.
Two worlds could differ only in WHICH THREE of the ten they drew — which is what "minor
colors only" actually was.

The models come from `raw.githubusercontent.com/shorepine/kenney`, a complete CC0 mirror and
**the only asset host reachable from the build container** (kenney.nl, itch.io,
quaternius.com, OpenGameArt and Freesound all fail to connect; GitHub HTML, the API and the
zip endpoint return 403). File names had to be probed for: 290 plausible guesses found 95,
and then `graveyard/iron-fence` gave away that the kits use HYPHENS, and 5,014 hyphenated
candidates took it to 175.

**Baked offline, not imported.** Unity cannot read .glb, and glTFast or UnityGLTF would bring
a second material and rendering path alongside the Graphics.RenderMeshInstanced one everything
here already uses, plus a GUID per model in a repo whose .meta files are all hand-written.
`tooling/fetch_scenery.py` fetches, parses, bakes vertex colours, welds and packs 119 pieces
into one 563 KB TextAsset: one committed file, one .meta, no importer, no prefabs.

Colour arrives two ways and both are handled — most kits sample a shared 512x512 atlas, the
nature kit has no image at all and splits a model into one primitive per material with a
linear baseColorFactor. Two things cost a run each to find: the atlas needs **no V flip**
(flipping samples solid black), and the atlases are **8-bit indexed PNGs**, so the decoder
needed a palette path. Pillow is deliberately not a dependency, so the whole PNG reader is one
zlib call and five filter cases. Welding is on position, normal AND colour together: welding
on position alone would smooth every hard edge and turn a castle into a blob.

**The format is read in Core, and it caught two bugs before anything reached a device.**
Neither would have thrown — a misread buffer does not error, it scatters triangles across the
level and looks like a physics bug. `RecordBytes` was declared 56 when the record is 64, so
every vertex was read eight bytes early; and `VertexBytes` was declared 16 when both writers
emit 14, with a doc comment claiming the pad kept it 4-byte aligned, which 14 is not. The
reader lives in Core specifically so the test assembly — which references Core and nothing
else — can round-trip the format with no file path and no engine.

**A landmark is expanded, not baked, and that is the load-bearing choice.** Baked flat the
castle keep is 7,100 vertices and one draw call, so six keeps are six draws of 7,100. Kept as
data — `Core/Art/Landmarks.cs`, nine structures as lists of (piece, position, yaw, scale) —
six keeps are still THREE draw calls, because every castle wall in the level lands in the same
instancing bucket. It also makes a landmark walkable by a test, and three tests found things:
four structures had parts reaching past the radius used to keep them clear of the road; the
mausoleum topped out at 5.7 m, which is a large building and not a landmark; and after fixing
that an upper bound was added, because at the landmark scale first chosen **the keep stood
38 m and filled the sky**. At 3.4 it stands 26 m.

**Bright landmarks, dark verge**, as a checked invariant. The pack is baked in Kenney's own
cheerful palette and each of the three zones is dragged its own distance toward the world's
stone at runtime — 0.74 / 0.30 / 0.12 on the Ashen Road — so the road you look down stays grim
while the castle on the skyline carries the colour. A test pins the ordering in every world.
Because the tint is a runtime uniform, changing the mood of the whole game is a number rather
than a re-bake.

`Scenery.shader` is CrowdInstanced's lighting with colour read from the vertex and the run-bob
and scale decode DELETED. That deletion is the point: CrowdInstanced recovers a soldier's bob
phase from instance scale in a 0.44-0.50 window and anything outside pins to 1.0 and brightens
30%, and scenery is placed at 1.5x to 3.4x. A separate shader means the trap does not exist
rather than has to be remembered.

Only landmarks cast shadows. The verge is dense and its shadows fall on ground nobody looks
at, and the shadow pass is where a field of scenery starts costing milliseconds — but a castle
that casts nothing sits on the land the way the army used to hover over the road.

Licence: all CC0, `Assets/Art/LICENSE-KENNEY-CC0.txt` verbatim, `Assets/Art/ASSETS.md` mapping
every piece to its kit, original name, zone, triangle count and height. The .glb sources are
not committed. `.gitattributes` gained explicit binary entries, because it previously marked
nine text extensions and nothing as binary.

**The world beside the road did not exist.** Not "was sparse" — did not exist.
`SpawnGroundStrip` derived every dimension from the lane pitch and built exactly one box,
8.316 m wide, and nothing else existed laterally. Every prop `RoadsideProps` placed between
5.2 m and 26 m was standing on the skybox. That is the real reason eight authored worlds read
as one place with a colour filter on it: there was nowhere for a world to be.

`Terrain.shader` and two boxes per level fix it — the land now runs out to 70 m each side.
Same discipline as the road, and deliberately the same `Hash21` and value noise: two different
noise fields meeting at the kerb would draw a seam down the full length of the level, which is
the one place the eye is guaranteed to be. Two draw calls and 48 vertices for the entire world
beside the road. Its top sits 3 cm under the road so the edge reads as a kerb rather than as
two coplanar surfaces z-fighting over 400 m. It receives shadows and casts none — it is flat,
so its own shadow is a no-op, and it is the only surface large enough to show the rails'.

Eight grounds: ash, bog, drowned flagstone, scorched earth with live cinder, bone sand, snow,
red silt, violet dust. **A test requires every pair to be separable, and it caught three
collisions that would have shipped** — including the one that matters most, since The Ashen
Road and Gallows Mire are rounds 1-2 and round 3, exactly the transition the complaint was
about, and they started 0.177 apart against a 0.18 floor. The worst pair now clears at 0.204.

**The grade was a constant, and it was fighting the palettes.** `BuildStack` built the volume
profile once and dropped the component handles, so all eight worlds bloomed at
`(1.00, 0.86, 0.72)` warm, filtered at `(1.00, 0.96, 0.90)` warm, and ran saturation at
**-4** — the one control that decides how much colour survives, set to remove it. Every bright
pixel in the green world bloomed orange.

The grade is now derived per world from colours the theme already declares, in the style of
the existing `RailBase` / `AmbientSky` / `FogFromSky` properties, so a new world cannot forget
to grade itself and a second table cannot drift out of step with the first. World 0 reproduces
the shipped constants to within 0.03 per channel and a test pins that: the Ashen Road keeps
the look that was actually judged on a screen, and the other seven stop borrowing it.
Saturation now runs +6 to +13 by accent chroma.

Two things worth writing down. The `ShadowsMidtonesHighlights` multipliers are
**mean-normalised**, because that component multiplies and a tint whose channels do not average
1 lifts or crushes the whole frame as a side effect of changing its hue — which reads on device
as "this world is brighter" rather than "this world is green", the same mistake pointing the
other way. And the hue shift is applied to the grade as well; leaving it out would drag every
round of an act back toward the act's undrifted colour.

**The horizon glow finally points somewhere.** `DarkSky`'s `_GlowDirection` was never written
from a theme, so the most recognisable feature of the sky sat straight down +Z at the same
height in all eight worlds. It now comes from `SkyGlowYaw` with the round's azimuth riding on
top, bounded to +/-40 degrees — past that the band the player is meant to run toward is beside
them instead of ahead.

Also fixed while auditing: `Assets/Scripts/Core/World.meta` was missing entirely, a committed
gap since the world table was added.

**Six bosses became thirty fights.** The roster is six creatures and it was six fights: the
same Bone Colossus with the same interval, the same blows and the same ward every time it came
round. `Core/Boss/BossAffix.cs` adds five champion modifiers — Frenzied, Armoured, Vampiric,
Haunted, Colossal — chosen from the ACT index, so the affix is a property of the act rather than
of the encounter and the threat rounds can show it three rounds before it matters.

Each one modulates a seam `BossSim` already had rather than adding a new one: `NextInterval` for
Frenzied (x0.72), `WardPool` for Armoured (+16% of max HP as ward, on a boss that may have had
none), `AddsPerCycle` for Haunted, HP x1.40 and blows x1.25 for Colossal, and a heal on
`ApplyBossHit` for Vampiric (4.5% of max on an unblocked landing, nothing on a blocked one). The
clamps live with the affix, not at the call sites: blow fractions stay in [0,1] and intervals
have a 0.35 s floor, because both are `BossSim` preconditions and a composed multiplier is
exactly where they get violated.

**The cycle is seven long against a roster of six, and `None` sits in it twice.** That second
fact is the one worth writing down: the period I asserted was wrong twice before I enumerated it.
`None` appearing twice makes the act-to-affix map non-injective — the first pair to recur is
`(boss 1, None)` at acts 5 and 23, 18 apart, not 42. What does not repeat is the champion
encounters: across the 42 acts from the first affix act the named affixes produce **30 distinct
champions**, six bosses times five affixes, every one and none twice. The test pins that count
rather than a period reasoned my way to. `None` is also forced for the first two acts, so the six
base fights are taught before anything modifies them.

**Haunted summons on top of its swing, it does not summon instead of one.** Wiring the extra add
into the existing `Summoner` path would have made every Haunted boss spend its attack cycle
calling and never swinging — a strictly easier fight than the same boss without the affix.
`LandBossAttack` now bites with the adds, adds the summons, and *then* swings unless the
archetype is genuinely a summoner.

Surfaced three ways, all of them visible on the threat rounds as well as the fight: a name prefix
("Frenzied Bone Colossus"), an aura the body breathes and the ward shell takes 45% of, and the
HUD bar pulled a third of the way toward the affix colour. The aura fades out under a telegraph
rather than adding to it — the wind-up is the one signal the player has to read to survive, and a
second colour competing with it would cost them blows. Affix tints are authored as HDR emission
colours, so the HUD divides by the brightest channel first: a UI `Image` clamps at 1, which would
have rendered every warm affix as the same red.

**A gate for the failure that keeps costing five minutes.** Unity is the only compiler for
everything outside `BattleRunner.Core`, and a headless round trip is about five minutes. A
dropped brace is the cheapest possible way to spend that — not a design mistake, a text edit that
lost a character — and it cost a CI failure during this very work (`ContentFactory.cs`,
`error CS1513`). `tooling/check_csharp_braces.py` strips comments, strings and chars, then counts
the three bracket kinds across every tracked `.cs` file. Deliberately not a parser; anything
subtler is the compiler's job. It runs in the CI lint job and in the pre-push gate ahead of the
docs check, because a file that cannot parse makes every other check meaningless. Verified
against the real defect, not a synthetic one.

**Eight things a road can ask, where there used to be three.** `BuildChunksForLevel` placed an
add at 12 m, another at 28 m, and on every third chunk a `x2` opposite a `-N` at 40 m, with one
pack always in lane 0 — every chunk in the game was one of those three, cycling forever. That is
the literal reason the doors always looked the same; it was a formula with three branches, not
an impression. There are now eight shapes, each asking something different: Ladder, Fork,
Gauntlet, Minefield, Toll, Vault, Breather, Crossfire.

A round is a sequence chosen from its index under three rules, each there because breaking it
makes a round read badly — no shape twice in a row, the opening chunk is always readable
(starting on a Toll is a round that begins by taking something away), and at least one Breather
in the back half. Spacing is the hard constraint: nothing sits closer than 12 m to the next
separate decision, and a test walks every shape at every difficulty to prove it. The road is
built from these per round rather than from six baked levels cycled forever, which is why round
eight used to replay round two's gates. With variety in place the length clamp is gone — rounds
run 12 to 22 chunks, 540 to 990 m, about 54 to 99 seconds.

**Par force had to be rebuilt, and that one was a real bug.** It sizes the revive a player is
handed after watching an ad. The old estimate walked the optimistic line and multiplied every
gate together, which was stable when the generator produced exactly one `x2` every three chunks
and swung by two orders of magnitude once layouts varied: measured at 297 on round 2, 12,533 on
round 5, and pinned at the 60,000 soft cap on rounds 20 and 30. A player reviving on round 2
would have come back with 99 units and on round 5 with 4,177. The adds are the stable backbone
so they are banked in full, and the multiplies now lift the result LOGARITHMICALLY in their
count rather than multiplicatively in their values — which is also closer to the truth, since
three lanes cannot all be taken and the tenth multiply is worth far less than the first. Par now
runs 337 at round 0 to 8,258 at round 60, smoothly, and is computed per ROUND on
`GameContext.CurrentPar` rather than read off a level asset that cannot know which round is
being played.

**The boss stops being a formality, and starts stalking you.** `RunnerLoopState.OnFinishReached`
transitioned unconditionally into `BossState`, so every round ended in a fight and the sixth Bone
Colossus of the evening carried exactly as much weight as the first. A fight is now the last
round of an act; on every other round the act's boss comes to the finish line anyway, ramps its
telegraph, roars — two shockwave rings, camera trauma, an FOV punch — and lets the player past.
It stands `40 - 7 * ThreatStep` metres out, floored at 17, and everything about the roar scales
with how far into the act it is, so an act of three and an act of five build to the same peak.

Two things that would otherwise have been silent bugs. `GameConfig.BossFor` now resolves through
`RoundPlan.BossSlot(plan.ActIndex, ...)` rather than the raw round index — the creature that
threatens on round two has to be the one that swings on round four, or the build-up means
nothing. And `BossThreatState` is deliberately NOT wired to `TutorialCoach`: the coach arms its
shield lesson on the first telegraph it sees, and a threat telegraphs without ever landing a
blow, so it would have taught "flick down to block" against an attack that cannot arrive and
asked a finished run to hold while it did.

**Every round pays now, and the fight is the payday.** Making bosses rare without touching
rewards would have cut talent income roughly FOURFOLD in silence — `BossEncounterState` was the
only thing that ever granted stat points, and the tree was sized against per-round income.
`RoundRewards` holds the curve and keeps `LegacyIncome` — the old `perBossKill + roundIndex / 2`,
every round — so the test compares against real former behaviour rather than a remembered
number. Across sixty acts no act pays less than the same rounds used to. The shape changes
though the total does not: a normal round pays a little, a boss round pays more than three of
them plus a bonus for how long the act made you wait. Threat rounds roll loot at reduced luck
under their own header. The award moved out of `BossEncounterState` entirely, because two award
sites would have paid a boss round twice.

**And the player can finally see where they are.** The HUD had four text elements and the level
name appeared only on the main menu, which is the one place it does not matter. There is now a
marker reading `3-2  THE BONE WASTES`, or `3-4  HOLLOW LEECH AWAITS` on the last round of an
act. The menu shows the world's name rather than the level asset's, since an act wears one world
while the level list cycles on its own period.

**And there is now something standing beside the road.** The measurement that mattered most
was the third one: either side of the road sampled `(10,8,12)` — black — in every frame. There
was no background to be tired of, because there was none. Ten procedural props (gravestone,
dead tree, broken column, brazier, obelisk, hanging cage, bone arch, rock spire, ruined wall,
stump) now dress both verges in two bands: a near band from 5.2 to 9 m carrying detail the
player reads as they pass, and a sparser far band from 10.5 to 26 m at larger scales carrying
mass. Both sit outside the rails, so nothing there can be mistaken for something to steer at.

Drawn exactly as the crowd is — one `Graphics.RenderMeshInstanced` per prop kind, about four
draw calls for a verge of three hundred, against doc 04's budget of under sixty on the lowest
tier. Placement is hashed from the round index, so a round is dressed identically every time it
is played, and the visible slice is re-gathered only after the player has moved eight metres
rather than every frame. Nothing casts a shadow: the rails are the only static casters today
and the shadow pass is exactly where a prop field would cost real milliseconds.

Two settings on the prop material are pinned rather than left to a default, and both would have
been silent bugs: `_BobAmount` would have made a graveyard march in step with the army, and
`_ToneSpread` decodes its per-unit phase from INSTANCE SCALE inside a 0.44-0.50 window — props
are scaled 0.8 to 3.2, so every one of them would have pinned to the top of that curve and come
out about 30% brighter than intended.

**Eight worlds, and rounds that belong to acts.** The report was that every round has the same
pavement, the same background and the same doors. Sampling nine device frames settles it: the
sky above the horizon is `(11,10,15) ± 2` in every one, the road in front of the camera is
`(25-29, 25-31, 37-46)` in all eight gameplay shots, and beside the road every frame reads
`(10,8,12)` — pure black. There was no background to be tired of, only an unlit plane and then
void. The causes were structural: `EnvironmentLook.Apply()` is a parameterless static that runs
once at boot BEFORE a level exists, and `TrackController` builds its four materials from
constants in `Initialize` and never touches them again.

An **act** is now 3-5 rounds sharing one of eight authored worlds — Ashen Road, Gallows Mire,
Sunken Crypt, Ember Fields, Bone Wastes, Frozen Reach, Blood Marsh, Throne of Dust. Slot 0
reproduces the shipped palette exactly, because it is the one look that has been seen on a real
screen and judged; the other seven are pushed away from it rather than invented beside it. A
world carries only values that were already shader properties — `Road.shader` exposes eight and
`DarkSky.shader` ten — so none of this needed new shader work.

**Worlds walk forward, bosses walk backward.** `LevelFor(i)` and `BossFor(i)` both wrapped
modulo 6 against arrays of length 6, so the pairing never changed; the v0.12.0 note calling them
"two coprime-ish cycles" was wrong, they were the same cycle. Acts now take `theme = act % 8`
and `boss = -act mod n`. Stepping by `n-1` is stepping by `-1`, and `n-1` is coprime to `n` for
every `n`, so it visits every boss in a different order than the themes without a stride picked
by hand per roster size. **24 distinct pairings before the first repeat, against exactly 1.**

**The first act is two rounds and is a one-off, not part of the cycle.** The tutorial teaches the
shield on a boss telegraph, so a long opening act strands a new player with a verb they have
never been shown — and folding a 2 into the repeat would bring two-round boss gaps back around
forever. A test caught exactly that and now pins the gap between fights at only ever 3, 4 or 5.

**Rounds inside an act differ too, and the drift ramps.** `ThemeVariant` hashes the round index
into a hue shift, fog depth, star strength, light azimuth, prop density and road wetness — about
a third of full amplitude on an act's first round, full on its last. A world is introduced before
it is bent, which reads as going deeper into somewhere rather than as noise. The hash is unsigned
integer arithmetic with exact expected values pinned, because float maths that agrees under .NET
and disagrees under Mono has already cost this project a CI failure.

**The fog rule in doc 10 was half wrong and is now corrected.** It records fog as
`horizon + ~0.76 * glow`; solving the shipped values per channel gives r = 0.710, g = 0.697,
**b = 0.457** — red and green fit one factor to within 0.005, blue does not, because the shipped
fog is deliberately warmer. Deriving fog from sky would have shifted every world's horizon cooler
than the one on screen today, so fog stays authored and a test refuses any world whose red or
green drifts more than 0.02 from the sky-derived value.

`Resources/Road.mat` and `Resources/DarkSky.mat` are now **instanced rather than used by
reference** — a per-round retint of the loaded asset would have edited a committed file on disk
every time the editor played a round. The key light is kept in a field, where before it was
created and its reference dropped. The ground strip runs 200 m past the finish instead of 180,
because that 180 was chosen to clear a fog end fixed at 170 and worlds now pick their own
weather. The fog MODE is deliberately not themed: Unity's Automatic stripping keeps only the
modes a scene declares, and a world switching to exponential would have no fog at all on device.

Rounds grow from `5 + min(3, levelIndex)` chunks — capped at 8 forever, 24-38 seconds — to a
flat 12, about 54 seconds. `RoundPlan` designs 12 to 20, but content clamps to the floor until
the chunk archetypes land: tripling a round's length while every chunk is still one of three
layouts makes the repetition worse, not better. Pools moved with it, 14/20 to 40/24.

**Six bosses that are actually six bosses.** The game shipped with two, and they used the
SAME MESH: `BossDefinition` differed in name, tint and stats and in nothing else, both
rendered `ProceduralMeshes.Boss` at 6x, and both ran one pattern — telegraph, then a single
hit. `GameConfig.LevelFor` then clamped past the last authored level, so from round six
onward it was that same fight forever with only the HP curve moving.

Now there are six archetypes, and an archetype is a silhouette AND a power, deliberately
coupled: a player has about a second of telegraph to decide what to do, and they will only
ever learn "the hunched one drains you" if the hunched one is always the one that drains.
*Bone Colossus* slams (the fight that already existed, unchanged). *Ember Lich* throws a
three-blow volley worth more in total than a slam and less per blow, so one well-timed
shield turns the worst attack in the game into the best one to defend. *Grave Warden*
carries a ward that soaks damage until it breaks, and a spell strips it three times faster
than the crowd's grind does — so breaking it is an action rather than something that happens
while you wait. *Hollow Leech* bleeds force every frame and only a raised shield stops it.
*Pale Shepherd* calls adds that bite on the next cycle unless a spell clears them; the
shield deliberately does NOT answer them, or one flick would handle every archetype and
there would be no reason to have six. *Gore Hound* compresses its own attack cycle as its
health falls, down to 45% of the printed interval at the moment it dies.

**What separates them on screen is the SILHOUETTE, because that is all that survives the
distance.** The boss stands about 16 m out, backlit against fog; a different shoulder width
is invisible there and a different number of legs is unmistakable. So the six differ in what
an outline can carry: the lich has no legs at all and a staff above its head, the warden
presents a tower shield to the camera before it presents a body, the leech is bent almost
horizontal with its head thrust forward BELOW its shoulders, the shepherd stands under a
closed halo ring in empty sky, and the hound is a four-legged horizontal mass about 10 m
long. Measured rather than eyeballed: their object-space heights run 0.66 to 1.34 units, so
`BossView` derives each one's scale from a target on-screen height instead of a shared 6x —
otherwise the roster's variety would land in the sizes, where it just looks like a bug,
instead of in the shapes, where it reads.

`AddPrism` now swaps its two ends when the caller's "top" is lower. `AddHull` takes corners
0-3 as the bottom face and winds every quad from that assumption, so a descending segment —
a head thrust down and forward, a trailing tail — came out inside-out: normals inward, every
face backface-culled, the limb rendering as a hole.

`LevelFor` cycles instead of clamping, and the boss is chosen by ROUND rather than by level,
so the two cycles run independently and the first six rounds are six different bosses.

**The talent tree ran dry after three boss kills; now it does not run dry at all.** The old
shape was 3 branches of (1 + 2-exclusive + 1) = 12 nodes with only NINE takeable, at one
point each, against an income of 3 points per boss. Three bosses emptied it, and from the
fourth on every point earned had nowhere to go. The new tree is four branches (Warlord,
Warden, Zealot and a Crossroads of hybrids), six tiers deep, ranked: ~60 authored nodes
carrying over 200 point-spends, three mutually exclusive keystones at the bottom of each
path, and hybrids gated on real investment in TWO branches so they reward committing
rather than dabbling. Tiers unlock on points spent in the branch, not on per-node
prerequisites, which keeps the rule explainable in one sentence and means adding a node
later can never orphan a save.

**A tier gate counts only what is ABOVE it, and that is load-bearing.** Counting the whole
branch made a gate self-satisfying, and worse, it made the tree impossible to unwind: two
tier-3 nodes sitting on exactly eight branch points block each other's refund forever and
the only escape is FORGET ALL. Measuring points strictly shallower than the node means the
deepest thing a player holds is always refundable, so any tree can be walked back one rank
at a time — proven by a test that buys everything buyable and then hands every rank back.

**And past the tree, paragon.** Six endless tracks, unlocked by taking any keystone —
reaching a keystone is the moment a build has an identity, which is a far better place to
hand someone an infinite sink than the moment they have taken literally everything. Cost
escalates (`1 + held/12`) so an endless track cannot outrun the authored tree in an
evening; value is a diminishing SUM (`PerRank / (1 + rank/25)`) so the total grows without
bound and a point at round fifty still means something. Boss income scales too —
`3 + levelIndex/2` — putting the tree at roughly thirty kills rather than eighty.

**Nine new mechanics, so the tree hooks into verbs instead of into numbers.** Gate crits
(double the GAIN, not the printed value, which is the only definition that reads the same
for `+` and `x`), multiply chains that escalate and break on a subtract, lane magnetism
that only ever pulls toward a BENEFICIAL gate, pack shatter, banked overflow, second wind,
execute, spell echo and shield reflect. All of it is engine-free in `Core/Run/Talents.cs`
with the random roll passed IN rather than taken — a function that calls `Random` itself
can only be tested statistically, and a mechanic whose edge cases are merely sampled is a
mechanic whose edge cases ship. Every one is pinned inert at zero, so a player without the
talent gets byte-identical behaviour to the game before it existed.

**The tree screen was a fixed 3x4 grid and could not survive this.** Twelve nodes fit on a
phone; sixty do not. `SkillTreeScreen` is now five tabs over a hand-built `ScrollRect`
(`RectMask2D`, not `Mask` — a stencil material per graphic would break batching for sixty
buttons), tier headings as signposts, rank pips on every cell, and a paragon tab. Taking
and giving back became separate controls, because with ranks a node can be both rankable
and refundable in the same moment and one tap can no longer mean both.

Save schema v4 -> v5: taken ids become rank-1 entries, and ids that no longer exist are
refunded as unspent points rather than silently dropped.

**The magenta risk is retired, not hoped away.** A fourth shader in `Resources` is exactly
the path that shipped v0.1.0 solid magenta. `Vfx.shader` sits beside a hand-written
`Vfx.mat` that references it by GUID (the arrangement `Crowd.mat` has used since v0.1.1),
carries no `multi_compile` keywords at all so there is exactly one variant, declares
`Fallback Off`, and `VfxSystem` validates the material against the ACTIVE pipeline and
turns itself off entirely if anything is wrong. Deliberately no fallback material:
substituting an opaque shader for an additive one would flash untinted rectangles across
the road. Worst case is a build with no effects, never magenta ones.

`GateApplied` and `EnemyContact` now carry the world position of what resolved — at 10 m/s
the gate and the crowd are metres apart by the time the handler runs, and an effect that
misses its cause reads as an unrelated flash. Impulse sizes reuse `CameraFeel`'s octave
ratio, so a x2 lands the same at 10 units and at 1000.

**Fog was being interpolated across a 400 m quad.** Turning fog on and stretching the
ground past the fog wall were both right, and together they produced a new defect: on
device the near road brightened ~2x and warmed toward the fog colour between the start of
a level and its boss, with neither the material nor the distance changing. Both shaders
computed the fog factor at the VERTEX — fine for ordinary geometry, wrong for a ground,
lane lines and rails that are each one stretched box spanning the whole level from eight
corners. The factor then depends on where the camera sits along the box. Fog is now
computed per pixel from the interpolated world position.

**Two overcorrections from the previous pass.** Lane lines and rungs were neutralised in
hue (right) but cut to ~0.9x the road's luminance (wrong) — darker than the stone they are
painted on, and effectively invisible on the dark early stretch of a level, in a game whose
whole read is three lanes. Back to ~1.5x. And the finish line still had the unrestrained
default rim the gates were just fixed for: a full-width up-facing slab over an above-white
colour, filling the bottom of the frame with gold for the entire boss fight, which happens
past it.

**Confirmed on device: the shield ward reads.** Army lights cyan-white, hero near-white,
HUD reads SHIELDED. Also confirmed: the boss renders as a lit figure, the road runs into
haze instead of ending in mid-air, the rails and road are neutral, enemy packs match the
crowd's scale, talent text wraps inside its cell, and a pure-Focus relic now scores Item
Power 4 instead of ~0.

**The boss was rendering as a black cutout, and three things caused it.** Device shots of
v0.7.0 showed the Bone Colossus at a uniform #3a3a3a across every face — darker than the
road under it. `BossView` uploaded `tint * 0.5f`, but these are sRGB values in a linear
project, so halving in sRGB is a 0.234x cut in linear and the boss landed on a 5%
reflectance. `saturate(dot(N,L))` then collapsed every camera-facing normal to the same
0.45, because the boss is backlit. And `_EmissionFlat` at 0.15 against that dead albedo
was two thirds of every pixel — a term that depends on neither normal nor view. Now: a
real bone albedo, a wrapped half-lambert (floored at 0.30 so nothing that was visible goes
black), and the resting flat term cut to 0.03 — with the telegraph driving it back to 0.33,
because that term was the entire wind-up warning. The widened rim lobe is reverted; it was
~0.004 on the faces the camera sees whatever its width.

**The lavender was the rails and the lane markings, not the road.** The premise in the
previous entry was wrong: ambient is `Trilight`, which does not sample the skybox at all,
and the sky's glow is warm. The rails ran `_EmissionColor` at a blue/red ratio of 4.0 on a
face whose normal is perpendicular to the view axis, so the "tight" rim still read 0.52 at
30 m — a self-lit periwinkle bar the length of the frame. The lane lines and rungs were 86%
pure emission at a ratio of 3.5, in a dense grid over the whole road. Both neutralised.

**Fog was stripped from the Android build entirely.** `Main.unity` had `m_Fog: 0`, and
Unity's default Automatic stripping keeps a `FOG_*` variant only if a scene enables that
mode — so `MixFog` was a no-op on device and every fog value in the project was dead code.
That, plus a ground strip that stopped 40 m past the finish, is why the road visibly ended
in mid-air 62 m ahead. Scene fog on, ground out to 180 m, and linear fog 70-170 m in a warm
colour that matches the sky the road actually meets rather than the bare horizon band.

**Gate frames were 1.7-3.8x the bloom threshold.** The gate colours are over white and are
gamma-EXPANDED on upload, so `_EmissionFlat 1.15` was multiplying 1.23-1.78, and gates
never overrode the shader's wide default rim lobe. Flat term to 0.70 — the point where the
dimmest gate still clears the threshold face-on — and the same tight lobe the rails have.

**Enemies were 2.1x the size of the soldiers running at them.** A stale constant, not a
choice: packs are drawn at scale 1, which matched a crowd drawn at 0.94-1.06, and were
never brought down when the crowd went to 0.44-0.50.

**Item Power ignored every fraction-valued stat.** The same "kind alone decides the units"
bug that produced "Focus -0 %", in a second place: a +1% Focus affix scored 0.01 points
instead of 1, so the Ember Talisman on the loot card read Item Power 2 with its second
affix contributing nothing, and Auto-Equip ranked every fraction-stat relic as junk. Four
tests now pin ItemPower's units to `StatFormat.IsFraction`.

**UI**: talent descriptions wrap instead of overflowing their cells and being drawn over by
the neighbouring column; cells narrowed to 280 so the three columns have a real gutter on
phones taller than 16:9; CONTINUE centres when FORGET ALL is hidden instead of sitting
172 px right of centre; the slot screen's PLAY and ERASE no longer overlap by 53 units, and
PLAY centres on an empty slot; the slot list is rebalanced from a 2.6:1 vertical imbalance;
the two loot buttons get the same footprint; the loot header stops saying "TWICE!" for the
rest of the session; the main-menu stat readout is parchment, not hyperlink blue.

**The boss is no longer the soldier mesh at 6x.** `ProceduralMeshes.Boss` is its own
132-triangle shape: horns (two segments sweeping out on the left, a snapped stub on the
right), a torso that leans forward onto the player, mismatched pauldrons and a cleaver
held out past the road edge. It is built from sheared hulls rather than axis-aligned
boxes, so the rim term has varied normals to shade for the first time. `BossView`'s
180-degree yaw is gone with it — harmless while the mesh was symmetric, it would now
show the player the boss's back.

**The boss was outside the shadow map.** It stands 26.1 m from the camera and the
shadow distance was 24 m, so the largest silhouette in the game cast nothing and
received nothing — while `BossView` had been asking for `ShadowCastingMode.On` the whole
time. Now 28 m, at ~20 mm per texel, still five times finer than the half-depth that
decides whether a caster turns inside out. The distance is also part of the URP asset's
dirty test now: it was assigned unconditionally but never marked the asset changed, so a
machine that had already generated the asset would have kept the old value.

**The road was lavender twice over.** `_BaseColor` was violet (blue highest, green
lowest) *and* the ambient term samples a blue-violet sky, so the largest surface in the
frame got tinted by both. The stone albedo is now warm-neutral and the cold ambient does
the tinting alone; the damp sheen stays cool but is pulled back from a second full
coverage blue wash.

**Every talent capstone was off-screen.** Found in v0.6.0 device screenshots: the tree
showed only three rows. `SkillTreeScreen` stepped rows 0, 2, 4, 6 — both arms of an
if/else did `row++` and the body incremented again — so `RowY(6)` resolved to −0.115,
below the bottom of the screen. Annihilation, Undying and Multiplication have been
unreachable since the tree shipped, and the three visible rows sat at double spacing. The
row is now simply the list index, and a test pins the branch shape the layout leans on.

**Stage 3 — feel.** Every event looked identical before this: a x2 gate, a pack eating
half the army and a boss blow all produced the same nothing. The camera now has a
trauma-squared shake, a critically damped fov punch on a gain, a lane lean and a telegraph
lean-in — all scaled by the RATIO of what changed, so a doubling feels the same at 10
units as at 1000 (engine-free in `Core/Feel`, unit-tested). The shield finally has
feedback: it was a timing mechanic played completely blind, and the block window now wards
the army's own emission with a near-white spike on a blow it actually eats. The boss flash
drives the shader's view-independent flat term, without which it arrived at 15% strength
and was lost. Rails were ~83% pure emission and read as lit plastic.

The v0.4.0 screenshots confirmed the art pass landed — sky, stars, shadows, road, army,
gates and UI frames all correct on device — and surfaced two bugs that were never about
art: `Focus -0 %` on the menu and `+0.01 Focus` on the loot card. Both were units chosen
from the ModifierKind rather than from the stat, plus a hard-coded minus sign in front of
a zero. `StatFormat` in Core is now the single source of truth, pinned by eight new cases (the suite went 140 -> 162; it is 411 tests today).

A 30-agent diagnosis against the first device screenshots produced 24 findings, of which
11 survived adversarial refutation. The headline three: the key light pointed the same way
the camera looks, so every shadow was cast behind its own caster and fully self-occluded;
`.mat` colours are sRGB and gamma-converted on upload, so the road's authored 0.115 was a
1.25% reflectance and no grade could rescue it; and the rim term is a per-face constant on
hard-normal boxes, leaving the crowd 79-97% pure emission. Also: the army was drawn nearly
four body-widths into itself, gates fell below URP's bloom knee so they never bloomed at
all, and no content panel ever got a bronze frame because `AddFrame` had one caller.

The art fixes took a third CI round trip on a stale local left behind by the gate
material split (`CS0103`) — carelessness rather than an environment limit, since only
`BattleRunner.Core` is mirrored into the local `dotnet` project and the Gameplay
assembly's first compile is in CI.

First device screenshots found two sky defects, both arithmetic: the ember glow's
exponent of 6 gave a 27-degree half-angle against an 18-degree half-FOV, so it washed
the whole sky red instead of sitting on the horizon (now 110), and the stars were
18-pixel grey quads because `floor()` gives every pixel in a cell the same value (now
hashed points with a distance falloff).

The art pass took two CI round trips to compile: a missing URP assembly reference (now
caught locally by `tooling/check_asmdef_refs.py`) and a bloom parameter removed in URP
2023.1. Talents can also be handed back for
their point, and
[docs/09-monetization-setup.md](docs/09-monetization-setup.md) spells out the accounts
only the project owner can create — with the 14-day Google Play closed-test clock called
out as the one thing that has to start on day one, not when the game is finished.

**Unreleased since v0.1.2:** a first-time user experience, equal lane widths, gates that
scroll past instead of vanishing, a New Game option, and a talent tree replacing flat
stat points.

**Progression is now a tree.** The problem with three flat stats was not the count — it
was that Damage, Health and Cooldown *all only mattered during the boss fight*, so
nothing a player bought changed the forty seconds of running that is most of the game.
Twelve talents across three branches, with the tier-2 pair mutually exclusive so a build
means something, and a Zealot branch whose every node pays off on the road: gate yield,
run speed, enemy resist, loot fortune. Talents emit the same `StatModifier`s gear does,
so they compose through one pipe. Schema v4 refunds previously-spent points as a free
respec.

Talents are also **reversible**: a learned node stays tappable, the first tap arms the
undo and names it, the second refunds the point, and FORGET ALL empties the tree. Removal
is leaf-first — a node under a capstone refuses and says which talent has to come off
first — and a property test proves no reachable build can get stuck.
See [docs/08-progression.md](docs/08-progression.md).

**Three save slots.** The first screen now picks which game to play; each slot keeps its
own level, talents, gear and tutorial progress. A pre-slots `profile.sav` is *moved* into
slot 1 the first time it is opened, so an existing game is where its player expects it.
Erasing is per-slot and takes two taps, and nothing is loaded until a slot is chosen —
the bootstrap holds an empty placeholder and `SaveProfile` refuses to write before then.

The FTUE closes a measurable cliff, not a polish gap: a player who never learns to steer
is reduced to zero force by the enemy pack at **16.7 s** of their first run. Four
coaching beats now introduce steering, gate vocabulary, the spell flick and the shield
flick, each the moment it first matters. Both flick prompts say *"lift your thumb"* —
`GestureClassifier` is one gesture per contact, so a player steering with their thumb
down physically cannot cast, and an un-worded prompt reads as broken input. A held
prompt zeroes the run's forward speed rather than touching `Time.timeScale`, so
cooldowns, input and the ad service keep running. Every beat times out after six
seconds and is then marked taught, which the tests prove by ticking ten seconds of
frames with no input and asserting the run is released. See
[docs/07-ftue.md](docs/07-ftue.md).

Three lanes now render equal — the road was drawn `4.8 × laneWidth` wide with lane lines
only at `±0.5 × laneWidth`, so nothing marked the outer lanes' outer edge and the eye took
the rails as the boundary: the centre lane measured 2.20 m and the outer two 3.96 m each.

**Gates no longer pop out of existence.** Reported from device: *"on another lane doesn't
influence me, but visually it disappears, and it shouldn't."* Exactly right — resolution
and despawn were the same event, so a gate was recycled to its pool the instant the crowd
drew level with it. They are now separate planes: a gate scores (or does not) at the
crowd's leading edge and keeps drawing until it is 4 m behind the camera, so it lingers
~15 m — about 1.5 s of visible scroll-past. `TrackVisibility` holds both planes in
engine-free Core, and a test asserts the band between them never closes.

What lingers depends on what happened, which an adversarial review of the fix forced out:
a gate you **took** opens its aperture (the infill plate is opaque — `Queue=Geometry`, no
blend — so a 1.6 × 2.4 m slab would otherwise sweep backwards through the whole army), a
gate you **missed** slides past whole, an enemy you **fought** dies immediately, and one
you **dodged** slides past. The same review found that `FrontZ` is not monotonic: it is
derived from the crowd envelope, which shrinks when force drops, so a big subtract gate
pulls the leading plane backwards up to 1.8 m in one frame against an anchor advancing
0.167 m. Resolution is therefore a latch, or a spent gate slides back in front of the
plane and its label pops on again.

**New Game.** The menu offered only *SET FORTH*, so a returning player could not start
over — and could not see the FTUE at all, since their save marks it already taught.
*NEW GAME* takes two taps (the first arms it and says what is about to happen) and wipes
the profile to a fresh one at the current schema, which re-arms the tutorial.

That last part did not work on first release, and the cause was two fixes in the same
commit colliding. `SaveProfile` had just been changed to persist the coach onto the
profile before writing — so `OnNewRunPressed` created a fresh profile with mask 0, saved
it (stamping the *old* coach's "all taught" mask onto it), then read that mask straight
back into the reset. A new game wiped gear and levels but kept the tutorial suppressed.
`ResetProgress()` now takes no mask at all — a new game is untaught by definition, so
there is no mask worth passing — and runs before the save. Reported from device:
*"the central lane is smaller than 2 others."* It was — the road was drawn
`4.8 × laneWidth` wide with lane lines only at `±0.5 × laneWidth`, so nothing marked
the outer lanes' outer edge and the eye took the rails at `2.3 × laneWidth` as the
boundary. The centre lane measured 2.20 m and the outer two 3.96 m each, **1.8× wider**.
Every road dimension now derives from `CrowdMath.RoadHalfWidth`, all four lane edges are
drawn, and a test sweeps the road asserting each lane claims exactly a third — so what
the player sees is the same partition `LaneIndex` scores against.

---

## v0.1.2

Makes it a lane game again. Reported from device: *"when a team becomes big, it
occupies all 3 lanes, so the physics of moving to another lane doesn't work."*

**The formation covered the whole road.** `CurrentSpacing()` compressed spacing by
`sqrt(40/n)` while the phyllotaxis radius grew by `sqrt(n)`. Those cancel exactly, so
the disc pinned at `0.55 * sqrt(40) = 3.48 m` — a **6.96 m blob, wider than all three
2.2 m lanes combined (6.60 m)** — from 40 bodies upward and never changed again. At
the camera's true horizontal FOV (35.98°, not the 60° vertical) the visible frame at
crowd depth is 7.07 m, so the crowd filled **98% of the screen**. Steering moved it
within its own silhouette and nothing appeared to happen.

Width is now a property of the **road**, not the count: it saturates at 0.355 of a
lane (1.54 m, 22% of frame) and never grows again. Growth goes into **depth, forward**.

The two depth directions cost very different amounts of screen, which is the whole
trick. The rig is pitched 11.31° down with a 30° half-FOV, so the bottom-of-frame ray
meets the ground just **3.742 m behind** the anchor — a longer tail is simply invisible
— while the top-of-frame ray points 18.69° *above* horizontal and never meets the
ground at all. Reaching up the road is therefore nearly free, and that is where the
army grows.

| bodies | width | reach ahead | tail | footprint |
|---|---|---|---|---|
| 40 | 1.46 m | +2.42 m | −1.82 m | 6.2 m² |
| 100 | 1.48 m | +3.61 m | −2.18 m | 8.6 m² |
| 300 | 1.52 m | +5.08 m | −2.47 m | 11.5 m² |
| 512 (sim cap) | 1.54 m | +5.71 m | −2.55 m | 12.7 m² |

An earlier cut of this fix bounded depth symmetrically and was right about the lane but
wrong about the reward: the footprint grew only 30% while the army grew 650%, so a ×2
gate changed the count and nothing else. Spending the free forward direction takes that
to +68%, and the army visibly reaches further up the road as it grows.

**Lane collision was ambiguous.** Gates were claimed by `|crowdX - gateX| <= laneWidth
* 0.75` = 1.65 m against lane centres 2.2 m apart, so the three acceptance windows
overlapped by 1.1 m each: a crowd half a lane off centre satisfied **two lanes at
once** and could collect a `+` gate and a `−` gate on the same frame. Lanes are now
assigned by index (`CrowdMath.LaneIndex`), which partitions the road with no overlap
and no gap, and steering snaps to lane centres rather than a continuum.

**Also fixed, all found by auditing the screenshots against the code:**

- gates were **2.34 m wide on a 2.20 m lane pitch**, so adjacent frames overlapped by
  0.14 m and a three-lane row spanned 6.74 m of a 7.07 m frame — a solid wall, not
  three choices. Now 1.92 m with a 1.60 m aperture the crowd visibly passes through
- every gate label in the level drew at once through all geometry (built-in font
  material, `ZTest Always`), stacking into the unreadable pile on the horizon; labels
  beyond 34 m are now hidden
- the crowd's **run-bob was per-frame noise**, not a walk cycle: the shader hashed each
  unit's phase from its *world* position, which advances 0.167 m per frame at 10 m/s,
  moving the hash argument 13 rad and re-randomising every phase every frame. Phase now
  rides in the per-instance scale, which is stable
- growing the crowd never seeded the newly visible slots, so on the first `+` gate they
  drew from the run's start Z and **streaked the length of the level** to catch up
- world-Z smoothing left a permanent `v·dt·b/(1-b)` = **0.92 m lag**, so every body
  rendered a metre behind where the game scored it. Smoothing the local offset instead
  puts the 10 m/s ramp entirely in the exact anchor term and removes it
- gates and enemies resolved at a fixed offset from the centroid while the crowd's
  leading edge was 3.48 m further on; the hero, gates, enemies and the render bounds now
  all read one number, `CrowdController.FrontZ`
- the hero stood 0.6 m ahead of the centroid — 2.9 m *inside* a disc that reached
  3.48 m, with ~120 of 200 bodies drawn in front of it. It now stands on the leading
  plane at 1.35×
- render bounds were a second, independent copy of the formation model; they are now
  derived from the real asymmetric extent
- the build allowed all four screen orientations (defaulting to 1280×720 landscape)
  while every layout constant is tuned for portrait. Locked to portrait

8 regression tests added (67 → 75), including one that pins the original defect: the
old disc really was the same size at 40 and 300 bodies, and that size really was wider
than the whole road.

---

## v0.1.1

Fixes the all-magenta player and makes the playfield readable.

**The magenta.** The repo never committed a render pipeline — no
`GraphicsSettings.asset`, no `UniversalRenderPipelineAsset` in git. `UrpBootstrap`
created them locally on first editor open, but it ran through
`EditorApplication.delayCall`, which never fires under `-batchmode`, so CI never
ran it. The player therefore built on the **Built-in** pipeline, URP's scriptable
stripper removed 100% of our `UniversalForward` variants, and with `Fallback Off`
no SubShader was eligible — Unity substituted the error shader. The build log read
`After scriptable stripping: 0` and `gles3 (total internal programs: 0, unique: 0)`,
the only one of 49 shaders with zero programs, while reporting `Errors: 0`.

- `PipelineGuard` (`IPreprocessBuildWithReport`, `callbackOrder -10000`) assigns the
  pipeline before stripping and fails the build rather than shipping a broken player
- `UrpBootstrap` runs synchronously under `Application.isBatchMode`
- `ShippedShaderCheck` fails any build where the crowd shader packs under 2 KB
- shader gained `#pragma target 3.0` and a real `Fallback`
- `ShaderSafety` resolves against the **active pipeline** — `Shader.isSupported`
  reports compilation, not SubShader eligibility — and all tinting goes through
  `HasProperty`-guarded setters

Verified: Shaders payload `15.0 kb` → **`1.5 mb`**; `CrowdInstanced.shader` packed
`0.7 kb` → **`11.5 kb`**; URP shaders compiled `0` → `18+`.

**Readability**, from device screenshots:

- gate and enemy labels were mirrored — a `TextMesh` reads from its local −Z face,
  which is already where the camera sits; the 180° spin showed its back, drawn
  reversed by the font material's `Cull Off`
- camera 8.5 m / 25° → 5.5 m / 11°, FOV 65 → 60: ground fell from ~86% of a
  portrait frame to ~67%, with sky behind the crowd
- gates gained thicker posts and a filled infill plate (0.16 m bars were ~11 px at
  22 m); labels 0.045 → 0.16 `characterSize`
- crowd Z is kinematic — a critically damped spring tracking a 10 m/s ramp sat a
  constant 2 m behind, so bodies rendered short of gates that had already fired
- per-instance yaw and scale so units read as a crowd, not a lattice
- formation spacing compresses past 40 bodies (200 units spanned 15.5 m on a 7.9 m
  road); lane span no longer overshoots; ground widened; lane lines, side rails and
  speed rungs added for motion cues
- crowd falls back to individual draws when instancing is unavailable instead of
  vanishing; hero 1.6× → 1.2×
- chunks 30 m → 45 m: decisions had been arriving 0.2–0.8 s apart, under reaction time

**Also:** fixed a cross-runtime rounding bug real Unity caught and `dotnet` could
not. `float 0.4f` is `0.40000000596`; .NET collapses `1000 * 0.4f` to exactly
`400f` but Mono keeps the excess, so `Math.Ceiling` removed one unit too many —
and the error scaled with force. `ApplyBossHit` now computes in `double` with a
relative epsilon. 3 regression tests added (64 → 67).

## v0.1.0

First installable build. **Rendered entirely magenta** — superseded by v0.1.1.

- Full greybox loop, engine-free core with 64 tests, ScriptableObject content,
  single-scene state machine, GPU-instanced crowd, mock ad/IAP services
- Android player switched to **IL2CPP + ARM64**: it had been building ARMv7-only,
  which modern 64-bit devices refuse to install ("App not installed")
- `release-apk.yml` publishes a sideloadable APK to GitHub Releases on demand
- Unity CI activation fixed: GameCI needs `UNITY_EMAIL` + `UNITY_PASSWORD` for
  **every** licence type — a `.ulf` alone never reaches the build container
- Project repinned to Unity 6.3 LTS `6000.3.22f1`; the original pin was affected by
  [CVE-2025-59489](https://unity.com/security/sept-2025-01)
