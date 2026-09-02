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
| Content | 5 levels, 2 bosses, 15 gear items, 4 rarities, 12 talents |
| Art | Greybox — procedural meshes, code-built uGUI, no imported assets |
| Tests | 140, green under both `dotnet test` and Unity's Test Runner |
| Android build | Automated: ARM64 / IL2CPP APK published to Releases |
| Monetization | Rewarded-ad and IAP flows wired to **mock** services only |
| Docs | Enforced — `tooling/check_docs.py` gates pushes locally and in CI |
| Not started | Real ad SDK, analytics, battle pass, art pass |

**Confirmed on device:** v0.1.2 plays as a lane game. The crowd stays in its lane at
any size and the army reads as a column reaching up the road.

**Unreleased since v0.2.0:** an art pass on the lighting — HDR, bloom, tonemapping,
colour grading, a procedural night sky, trilight ambient, MSAA and real shadows, so the
army stands on the road instead of hovering over it. Talents can also be handed back for
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
