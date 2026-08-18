# 01 — Technical Risks & Design Bottlenecks

A critique of the concept before any code is written. Each risk lists the failure mode and the mitigation that the rest of this architecture commits to. Ordered by expected impact.

## R1. Gesture ambiguity between lane control and spell casting — *highest gameplay risk*

**Failure mode.** If lane switching is a discrete horizontal *swipe* and spells are vertical *swipes*, the recognizer must wait to see which one the player meant. Every real swipe is diagonal-ish; thumbs are sloppy during panic moments — exactly when players cast shields. The result is either input lag (waiting to classify) or false triggers (spell fires when the player meant to dodge), and both are fatal in a reflex genre.

**Mitigation (design change, not just code).** Make the two inputs *physically different motions*:

- **Lane control = positional horizontal drag.** The finger's X position continuously maps to the crowd's target lane/offset (this is what Count Masters actually does — it is not swipe-based). A drag has low velocity and sustained contact.
- **Spells = high-velocity vertical flick.** Short contact, high speed, dominant Y axis.

Because one is positional and sustained and the other is ballistic and brief, classification can happen within ~0.8 cm of finger travel using a dominant-axis ratio, giving near-zero perceived latency and a very small confusion window. Full design in [doc 02](02-input-architecture.md).

## R2. Crowd rendering on low-end Android

**Failure mode.** The naive implementation — one GameObject per unit with a `SkinnedMeshRenderer`, `Animator`, `Rigidbody`, and `Collider` — dies at 50–80 units on an Adreno 5xx device. Multiplier gates promise hundreds.

**Mitigation.**
- One logical `CrowdController`; **zero per-unit MonoBehaviours, Animators, or physics bodies**.
- GPU instancing with **baked vertex-animation textures (VAT)**: skinned animation is baked to a texture and played back in the vertex shader, so 200 animated units cost one draw call and no CPU skinning.
- **Display-count inflation:** simulate at most ~100–300 real units (per quality tier); when gate math exceeds the cap, show the true number on the counter ("×1,024") and scale damage from the number, not from rendered bodies. Players read the number, not the exact headcount.

Full design in [doc 04](04-performance-strategy.md).

## R3. Physics blow-up

**Failure mode.** Per-unit colliders + rigidbodies make gate/obstacle interactions O(units × colliders) and cause frame spikes precisely at the moments of highest on-screen drama (big crowd hits a gate).

**Mitigation.** No unit-level physics. The simulation runs in *lane space*: gates, obstacles, and enemy packs are trigger volumes evaluated against the crowd's centroid, width, and count. Deaths/spawns are arithmetic on the count plus pooled VFX — never physical collisions.

## R4. Exponential ×-gate math breaks the balance curve

**Failure mode.** Chained ×2/×3 gates grow force geometrically. A player who hits two extra multipliers trivializes the boss; one who misses two gets walled. Tuning becomes impossible and the loot/stat meta loses meaning because gate luck dwarfs gear.

**Mitigation.**
- Author levels in chunks with a **par-force curve** (expected force at each distance); multiplier placement is validated against it in-editor.
- **Soft cap** on effective force per level; overflow converts to secondary rewards (bonus boss damage %, loot-luck) at a diminishing rate, so hitting every gate still feels rewarding without breaking difficulty.
- Boss HP scales off level definition, not off the player's realized force, so gear/stat progression remains the long-term power lever.

## R5. Diablo II aesthetic vs. hybrid-casual readability & marketability

**Failure mode.** Dark, desaturated palettes fight the genre: gates must be readable in ~0.5 s on a 6-inch screen in sunlight, and hybrid-casual UA creatives depend on instant visual parsing. A muddy screen also raises CPI.

**Mitigation.** Dark *environments*, bright *gameplay*: high-emissive gate glyphs and frames, rim-lit units, saturated spell VFX against desaturated backgrounds. Treat this as a testable hypothesis — run CPI creative tests with the dark style vs. a brighter variant before committing full art production (Sprint 4 exit criterion).

## R6. "<5 s in menus" vs. meaningful loot

**Failure mode.** Auto-Equip that just picks "newest item" makes loot meaningless; deep affix comparison UIs kill pacing. The two stated goals conflict.

**Mitigation.** Every item resolves to a single scalar **Item Power** (weighted sum of its stat modifiers). Auto-Equip compares Item Power per slot — one tap, deterministic, <1 s. Depth is *read-optional*: rarities and affixes exist on the item card for players who care, but are never required reading. Stat-point spending is a three-button screen with a "recommended" default.

## R7. Battle pass drags in backend/LiveOps scope

**Failure mode.** A monthly $3.99 battle pass implies server-authoritative entitlements, receipt validation, seasonal content pipeline, and remote configuration — none of which belong in an MVP whose core loop is unproven.

**Mitigation.** MVP ships only the **seam**: an `IBattlePassService` abstraction and analytics events, with the track definition designed to come from remote config later. No server, no purchase flow for the pass until retention data justifies it. Rewarded ads and a minimal IAP (keys/chests via Unity IAP) are the only live monetization in the MVP.

## R8. Save & monetization integrity

**Failure mode.** Local JSON saves are trivially editable; rewarded-ad grants (loot doubling, resurrect) are client-trusted. Single-player hybrid-casual can tolerate this at launch, but a corrupted or version-incompatible save that wipes progression is a guaranteed 1-star review.

**Mitigation.** Version the save schema from day 1 with a migration hook; write-then-swap file saves to survive interrupted writes; a lightweight checksum to detect corruption (not to stop determined cheaters). Server-side receipt validation and cloud save are explicitly post-MVP items.

## R9. Runner → Boss → Loot coupling

**Failure mode.** The boss fight "uses accumulated stats/units" — if the boss scene reads live runner internals, the two systems couple and neither can be tested alone.

**Mitigation.** A plain serializable **`RunResult` contract** (final force count, hero `StatSheet` snapshot, spell charges, distance, gates hit) produced when the runner phase ends and consumed by the boss and loot phases. Defined in Sprint 1 before either consumer exists, so every phase is testable with a hand-authored `RunResult`. See [doc 03](03-system-data-architecture.md).
