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
| Content | 5 levels, 2 bosses, 15 gear items, 4 rarities, ~60 talents + endless paragon |
| Art | Greybox — procedural meshes, code-built uGUI, no imported assets |
| Tests | 194, green under both `dotnet test` and Unity's Test Runner |
| Android build | Automated: ARM64 / IL2CPP APK published to Releases |
| Monetization | Rewarded-ad and IAP flows wired to **mock** services only |
| Docs | Enforced — `tooling/check_docs.py` gates pushes locally and in CI |
| Not started | Real ad SDK, analytics, battle pass, art pass |

**Confirmed on device:** v0.1.2 plays as a lane game. The crowd stays in its lane at
any size and the army reads as a column reaching up the road.

**Unreleased since v0.2.0:** an art pass on the lighting — HDR, bloom, tonemapping,
colour grading, a procedural night sky, trilight ambient, MSAA and real shadows, so the
army stands on the road instead of hovering over it — and a procedural cobbled road
with brick bonding, grime and a wet sheen, in place of the flat slab. The UI is
rebuilt on code-generated sprites too: rounded bevelled panels, a bronze frame with
corner notches, a gradient backdrop and readable disabled states, across every screen.
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
a zero. `StatFormat` in Core is now the single source of truth, pinned by eight new cases (the suite went 140 -> 162; it is 194 tests today).

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
