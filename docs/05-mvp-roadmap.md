# 05 — MVP Development Roadmap

Four sprints, ~2 weeks each, sequenced so the **riskiest and most fun-critical mechanics are proven first**. Every sprint ends in a build that is playable on a device and testable against explicit criteria — no sprint ends with "it'll be testable next sprint".

Art is greybox until Sprint 4. If the game is not fun in grey capsules, better art will not save it.

---

## Sprint 1 — Runner core, greybox

**Goal:** a full run feels good in your hands, with the crowd and gate math already at production scale.

**Build**
- `BattleRunner.Core` / `.Data` / `.Gameplay` / `.Meta` / `.Tests` assembly layout (doc 03 §4).
- `InputRouter` + `GestureClassifier` — **lane drag only** (vertical branch stubbed but wired).
- `LaneTargetChannel` event channel; keyboard simulation path for the editor.
- Treadmill track from pooled `ChunkDefinition` prefabs; generic `ObjectPool<T>`; floating-origin or scrolling world.
- `CrowdController`: instanced rendering with VAT, lane-bounded formation, spring-damper steering, tier caps.
- Gate system: `+`, `×`, `−` evaluated against the crowd centroid; `RunState.forceCount` as `long`; soft cap + overflow rule (R4).
- Data contracts defined and frozen: `RunState`, `RunResult`, `StatSheet` (R9).
- Debug overlay (fps, draw calls, unit count, GC allocs).

**Exit criteria**
- Play a complete greybox run end to end on device.
- Gate math is provably correct: EditMode tests over randomized gate sequences confirm `forceCount` matches the expected arithmetic including soft-cap overflow.
- 60 fps on the mid-tier reference device with 200 units on screen; 30 fps on the low-tier device.
- **0 B/frame GC allocation** during a steady-state run.
- Steering feels responsive to two people who did not write it (subjective gate, recorded in the sprint notes).

---

## Sprint 2 — Combat gestures, boss, and the closed loop

**Goal:** the whole loop runs end to end, and the gesture question from R1 is answered with data.

**Build**
- `GestureClassifier` vertical branch: flick up → spell, flick down → shield; `InputSettings` SO with live-tunable thresholds; `CombatIntentChannel`.
- `SpellSystem` / `ShieldSystem` with cooldowns, UI icons, and cooldown gating.
- Enemy packs and obstacles that subtract force; death condition when force hits zero.
- `BossEncounter`: consumes `RunResult`, HP from `BossDefinition`, simple attack pattern, win/lose resolution.
- `GameStateMachine` with all states wired (`Boot → MainMenu → RunLoading → RunnerLoop → BossEncounter → LootPhase → StatUpgrade → MainMenu`), additive async scene loads; placeholder Loot and StatUpgrade screens.
- Mock `IAdService` / `IIapService` so the resurrect and loot-doubling touchpoints exist as flow, not as SDK.

**Exit criteria**
- Menu → Run → Boss → (win or lose) → Menu completes repeatedly with no state leaks (pools return to their prewarm counts between runs — asserted in a test).
- **Gesture confusion suite passes:** ≥ 98% correct on clean recorded traces, ≥ 90% on sloppy diagonals, **zero** spell casts emitted from any pure-drag trace.
- On-device 20-run session with intent logging: no unintended casts reported, median flick-to-cast latency < 120 ms.
- Boss phase runs standalone from a hand-authored `RunResult` (proves the contract is clean).

---

## Sprint 3 — RPG meta layer

**Goal:** progression that lands in under five seconds of menu time.

**Build**
- ScriptableObject definitions: `StatDefinition`, `GearItemDefinition`, `LootTableDefinition`, `BalanceSettings` (Item Power weights).
- Loot roll from weighted tables with rarity curve + pity counter; `LootPhase` reveal UI.
- **Auto-Equip** by Item Power (R6); gear screen as read-optional depth.
- Stat point spending (Damage / Health / Skill Cooldown) with a "recommended" default; `StatSheet` resolution feeding hero damage/HP, crowd damage, and spell cooldowns for real.
- `PlayerProfile` + `FileSaveService`: versioned JSON, atomic write-then-swap, checksum, migration hook (R8).
- Level progression (`currentLevelIndex`) and per-level scaling of boss HP and loot tier.

**Exit criteria**
- EditMode simulation of 10,000 loot rolls matches the authored table weights within tolerance, and the pity counter guarantees its floor.
- Auto-Equip provably always selects the highest-Item-Power item per slot (property test).
- Save survives a force-kill mid-run and an app restart; a v1 save file loads correctly after a deliberate schema bump to v2.
- **Boss defeat → back in a run in under 5 seconds**, measured on device with one tap per screen.
- Stat points measurably change outcomes: a maxed-Damage profile kills the same boss meaningfully faster than a fresh one.

---

## Sprint 4 — Monetization hooks, FTUE, and the performance/readability pass

**Goal:** a soft-launch-ready build that produces the data needed to decide whether to keep going.

**Build**
- Real ad SDK behind `IAdService` (LevelPlay or AdMob): **rewarded — double loot** in `LootPhase`, **rewarded — resurrect** in `BossEncounter`; frequency caps and a no-fill fallback path.
- Unity IAP behind `IIapService`: keys/chests, restore handling. **Battle pass ships as a seam only** (`IBattlePassService`, no server, no purchase flow) per R7.
- FTUE: first three runs on a scripted difficulty ramp with gesture prompts; no menu depth before run 3.
- Analytics events: run start/end, force at boss, gate hits/misses, death cause, ad opportunity vs. ad shown vs. reward granted, loot rarity granted, session length, level index reached.
- Art/readability pass on the dark-fantasy look: emissive gates, rim-lit units, bloom (R5); CPI creative test of dark vs. brighter variant.
- Device tier auto-detection + quality settings; final profile pass across low/mid/high reference devices; soak test for pool leaks.

**Exit criteria**
- Rewarded-ad flow works end to end with the real SDK in test mode, and **fails gracefully** with no fill (reward path never grants twice, never grants zero after a completed view).
- Low-tier reference device holds the 30 fps floor and < 350 MB RAM through a full run + boss + loot.
- Analytics events verified arriving in the dashboard with correct payloads.
- 20-run automated soak leaves pool counts and memory flat.
- Build size under the download threshold; cold start < 8 s on low tier.

---

## Sequencing rationale

| Decision | Why |
|---|---|
| Crowd rendering + pooling in Sprint 1, not "optimization later" | The rendering approach (instanced VAT vs. GameObjects) is architectural. Retrofitting it after gameplay is built means rewriting gameplay. |
| Gesture separation proven in Sprint 2 | It is the highest-risk *design* question (R1). If drag-plus-flick doesn't work, the combat design must change while it's still cheap. |
| Full state machine before the meta layer | The loop's shape (and the `RunResult` contract) constrains loot and progression; building meta first would couple it to a moving target. |
| Monetization last | Ad placements and pacing are only meaningful once the loop's rhythm exists. Building them earlier means tuning them twice. |
| Battle pass deferred entirely | Backend + LiveOps scope that an unproven core loop cannot justify (R7). |

## Explicitly out of scope for the MVP

Battle pass purchase flow and seasonal content · server-authoritative saves and receipt validation · cloud save · leaderboards/social · gear affix rolls beyond the `rolledTier` placeholder · more than one biome/boss archetype family · iOS build (architecture stays portable, but only Android is validated).
