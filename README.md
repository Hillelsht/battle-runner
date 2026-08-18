# Battle Runner

A hybrid-casual Android game built in Unity: a 3-lane **gate-multiplier runner** (in the vein of *Count Masters* / *Mob Control*) fused with **dark fantasy RPG progression** inspired by the Diablo II aesthetic — post-boss loot drops, three gear slots, stat points, and fast sub-5-second meta loops.

## Game Vision

- **Runner core:** 3-lane movement through math gates (`+`, `×`, `−`) that grow or shrink the player's force.
- **Active combat gestures:** vertical swipes cast a shield (down) or a high-damage spell (up) on top of lane control.
- **Boss fights:** each run ends in a boss encounter that consumes the force and stats accumulated during the run.
- **Fast RPG progression:** loot drops into 3 gear slots (Weapon / Armor / Relic), one-tap Auto-Equip, stat points (Damage, Health, Skill Cooldown) spent between runs.
- **Monetization:** hybrid-casual — rewarded ads (loot doubling, resurrect), IAP (keys/chests), $3.99 monthly battle pass (post-MVP).

## Architecture Documentation

Read in order — the risk critique deliberately comes first:

| Doc | Contents |
|---|---|
| [01 — Technical Risks & Design Bottlenecks](docs/01-technical-risks.md) | Critique of the concept's failure modes and the mitigations baked into this architecture |
| [02 — Input Architecture](docs/02-input-architecture.md) | Separating lane control from spell gestures without lag or false triggers |
| [03 — System & Data Architecture](docs/03-system-data-architecture.md) | ScriptableObject data model, save model, and the game state machine |
| [04 — Performance & Mobile Optimization](docs/04-performance-strategy.md) | Object pooling, crowd rendering, and budgets for low-end Android |
| [05 — MVP Roadmap](docs/05-mvp-roadmap.md) | Four testable development sprints |

## Baseline Technical Assumptions

| Area | Decision |
|---|---|
| Engine | Unity 6 LTS |
| Render pipeline | URP (mobile-tuned) |
| Input | New Input System + EnhancedTouch |
| Language / data | C#; static game data as ScriptableObjects, saves as versioned JSON |
| Min spec | ~2–3 GB RAM Android, Adreno 5xx class GPU, 30 fps floor (60 fps on mid-tier) |
| Force model | Crowd of units (Count Masters style) led by a distinct **Hero** unit that carries gear, scale, and spell VFX |
