# 06 — Greybox Implementation Notes

What the current build implements, where it consciously deviates from docs 01–05, and what comes next. This is the doc to read before changing code.

## What ships in this build

- **Full closed loop:** Menu → RunLoading → RunnerLoop → BossEncounter → LootPhase → StatUpgrade → Menu, as a plain-C# state machine (`GameStateMachine`, states in `Assets/Scripts/Gameplay/States/`).
- **Input:** positional lane drag + velocity flicks via the pure-C# `GestureClassifier` (doc 02), driven by `InputRouter` (EnhancedTouch + editor mouse/keyboard sim). Intents flow through ScriptableObject event channels.
- **Crowd:** array-based `CrowdController` (lane-bounded golden-angle formation, spring-damper steering) rendered with `Graphics.RenderMeshInstanced` and a hand-written URP shader; per-tier render caps with display-count inflation; hero scale expresses over-cap growth.
- **Gates/enemies:** trigger-free lane-space checks against the crowd centroid; all force math through the tested `GateMath` (soft cap → overflow bonus).
- **Boss:** logic in `BossEncounterState` via pure `BossSim`, consuming the `RunResult` contract; view-only `BossView` with telegraphed attacks (the shield window).
- **Meta:** loot rolls (weights + pity + overflow luck), Item-Power Auto-Equip, stat points, versioned checksummed save with atomic writes.
- **Monetization seams:** `IAdService`/`IIapService`/`IBattlePassService` with mocks wired to the loot-double and resurrect touchpoints.
- **64 EditMode tests** that also run Unity-free via `tooling/CoreTests` (`dotnet test`), plus a serialized-file lint (`tooling/lint_unity_yaml.py`) and a GameCI workflow.

## Conscious deviations from the architecture docs

| Doc said | Build does | Why / when to revisit |
|---|---|---|
| Additive async scenes per state (doc 03) | One scene; states toggle logical roots | Removes first-open failure modes; `IGameState` is unchanged, so the scene split is a mechanical refactor when content grows |
| VAT-baked crowd animation (doc 04) | Procedural run-bob in the shader | VAT needs real animations to bake; revisit with the art pass |
| TMP for UI | Legacy `UnityEngine.UI.Text` | Avoids the TMP Essentials import step in a text-authored repo; swap during the art pass |
| Treadmill/floating origin (doc 04) | Crowd advances in world space | Runs are finite (~200 m); precision is a non-issue below ~1 km. Revisit only for endless modes |
| Chunk prefabs | Chunks are data (`ChunkDefinition`) spawning pooled gates/enemies | Same authoring granularity with zero prefab YAML |
| Addressables (doc 04) | Resources for the 2 runtime-loaded assets | At greybox scale Resources is fine; Addressables land with real content volume |

## Known greybox limitations

- Gate/enemy labels use `TextMesh` with the built-in font. If a URP version renders them invisible, the force counter in the HUD still carries the information; replace with world-space canvas or TMP in the art pass.
- The boss fight is stationary DPS + timed shield windows — pattern variety is Sprint-2-polish scope.
- Analytics events and FTUE (docs' Sprint 4) are not in this build.
- `MockAdService` auto-grants after 1.2 s; real SDK binding will surface no-fill and cancel paths that the states already handle (`granted == false`).

## Where to start for the next milestone

1. **Feel pass on device:** tune `InputSettings` (thresholds are data), `RunSpeedMetersPerSec`, spring stiffness in `CrowdController`.
2. **Content pass:** `ContentFactory` is the single source — hand-author `ChunkDefinition` assets to replace the generated patterns (menu: BattleRunner → Regenerate Content).
3. **Sprint 4 items (doc 05):** real ad SDK behind `IAdService`, Unity IAP behind `IIapService`, analytics events, FTUE, device-tier render-scale.
