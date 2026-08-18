# 03 — System & Data Architecture

Two hard rules drive the data model:

1. **Static game data lives in ScriptableObjects** — designer-authored, hot-swappable, no scene references, safe to load via Addressables.
2. **Mutable state lives in plain serializable C#** — `PlayerProfile` (persistent), `RunState` (transient), `RunResult` (the phase-to-phase contract). ScriptableObjects are never mutated at runtime.

## 1. Static data — ScriptableObject catalog

```
Definitions/ (all ScriptableObjects, all referenced by stable string/GUID Id)
├── StatDefinition          Id, display name, icon, description
│                           (MVP stats: Damage, Health, SkillCooldown)
├── GearItemDefinition      Id, GearSlot (Weapon|Armor|Relic), Rarity (Common..Legendary),
│                           StatModifier[] , art refs, flavor text
├── LootTableDefinition     WeightedEntry[] { GearItemDefinition, weight },
│                           rarity-curve override per level tier, pity counter rules
├── SpellDefinition         Id, damage/shield values, cooldown, duration, VFX/SFX refs
├── EnemyDefinition         force cost on contact, visual set, formation size
├── BossDefinition          HP curve (by level index), attack pattern set, LootTableDefinition ref
├── LevelDefinition         ordered ChunkDefinition[] , par-force curve (see R4),
│                           BossDefinition ref, LootTableDefinition ref
├── ChunkDefinition         a ~30 m slice of track: gate placements (+ / × / − with values),
│                           obstacles, enemy packs
└── InputSettings           gesture thresholds (see doc 02)
```

```csharp
[Serializable] public struct StatModifier {
    public StatDefinition stat;
    public ModifierKind kind;   // Flat | Percent
    public float value;
}
```

**Item Power** (drives Auto-Equip, see R6) is computed, not authored:
`ItemPower = Σ (statWeight[stat] × normalizedValue)` with per-stat weights on a single `BalanceSettings` SO — one tunable place, deterministic comparisons.

### Event channels

Cross-system signals use the **ScriptableObject event-channel pattern** (`VoidEventChannel`, `FloatEventChannel`, `RunResultEventChannel` …): an SO asset holding a C# event that publishers raise and subscribers listen to. Systems reference the shared asset instead of each other — no singletons for gameplay wiring, trivially mockable in tests.

## 2. Runtime & save data — plain C#

```csharp
// Persistent — serialized to JSON on disk
public class PlayerProfile {
    public int    schemaVersion;          // day-1 requirement (R8)
    public List<GearItemInstance> inventory;
    public Dictionary<GearSlot, string> equippedByInstanceId;
    public Dictionary<string, int> statPointsSpent;   // StatDefinition.Id → points
    public int    unspentStatPoints;
    public int    currentLevelIndex;
    public long   softCurrency; public int keys;
}

public class GearItemInstance {           // an owned roll of a definition
    public string instanceId;             // GUID
    public string definitionId;           // → GearItemDefinition
    public int    rolledTier;             // future-proofing for affix rolls; MVP: 0
}

// Transient — exists only during RunnerLoop
public class RunState {
    public long  forceCount;              // long: ×-gates overflow int fast
    public float distance;
    public float spellCooldownRemaining, shieldCooldownRemaining;
    public int   gatesHit;
}

// The phase-to-phase contract (R9) — produced by RunnerLoop, consumed by Boss & Loot
public class RunResult {
    public long      finalForceCount;
    public StatSheet heroStats;           // snapshot, already gear+points resolved
    public int       spellChargesRemaining;
    public float     distance; public int gatesHit;
    public bool      reachedBoss;
}
```

### StatSheet resolution

`StatSheet.Resolve(baseStats, equippedItems, statPointsSpent)`:
per stat, `final = (base + Σ flat) × (1 + Σ percent)`. Resolved once at run start and once per equip change — never per frame. Consumers: hero damage/HP in the boss fight, crowd damage contribution, spell cooldown scaling.

### Save system

- JSON via a `SaveService` (interface `ISaveService` → `FileSaveService`): serialize `PlayerProfile`, **write to temp file, then atomic swap** to survive interrupted writes; append checksum line.
- `schemaVersion` + ordered migration functions (`Migrate_1_to_2(json)` …) run on load.
- Cloud save is post-MVP behind the same interface.

## 3. Game state machine

Plain C# — no MonoBehaviour state, no scene-coupled flow logic.

```
Boot ─▶ MainMenu ─▶ RunLoading ─▶ RunnerLoop ─▶ BossEncounter ─▶ LootPhase ─▶ StatUpgrade ─▶ MainMenu
                        ▲                │  died &   │ died ▶ (resurrect ad?) ─▶ back in, else LootPhase(loss)
                        └────────────────┴───────────┘
```

```csharp
public interface IGameState {
    void Enter(GameContext ctx);   // ctx: services, PlayerProfile, current RunResult
    void Tick(float dt);
    void Exit();
}
public class GameStateMachine { /* current state, TransitionTo(IGameState), guarded re-entry */ }
```

| State | Owns | Notes |
|---|---|---|
| `Boot` | service init, save load, quality-tier detect | splash-time budget < 8 s |
| `MainMenu` | menu scene/UI | Play, gear screen (optional read), settings |
| `RunLoading` | additive async load of run scene, pool prewarm, `StatSheet` resolve | masked by transition screen |
| `RunnerLoop` | treadmill, gates, crowd, gestures | produces `RunResult` on finish/death |
| `BossEncounter` | boss scene logic | consumes `RunResult`; resurrect-ad touchpoint on death |
| `LootPhase` | loot roll from `LootTableDefinition`, reveal UI, **Auto-Equip**, loot-doubling-ad touchpoint | target < 5 s with one tap |
| `StatUpgrade` | stat point spend UI (3 buttons + "recommended") | skippable |

Scenes load **additively and async**; each state owns activating/deactivating its scene + UI root. Monetization touchpoints are declared per state through service interfaces:

```csharp
public interface IAdService  { bool IsRewardedReady(AdPlacement p); void ShowRewarded(AdPlacement p, Action<bool> onDone); }
public interface IIapService { /* keys/chests catalog, purchase flow */ }
public interface IBattlePassService { /* seam only in MVP — see R7 */ }
```

Mock implementations ship first (Sprint 2–3); a real ad SDK (LevelPlay or AdMob) binds in Sprint 4. Nothing in gameplay references an SDK type directly.

## 4. Assembly layout

```
BattleRunner.Core        // pure C#: state machine, StatSheet, GestureClassifier, save model — unit-testable, no UnityEngine scene deps
BattleRunner.Data        // ScriptableObject definitions + event channels
BattleRunner.Gameplay    // MonoBehaviours: InputRouter, CrowdController, gates, boss
BattleRunner.Meta        // loot, equip, stat upgrade, monetization service interfaces + mocks
BattleRunner.Tests       // EditMode tests against Core (+ PlayMode smoke tests)
```

Dependency direction: `Gameplay/Meta → Data → Core`. `Core` references nothing above it — this keeps the classifier, stat math, loot rolls, and save migrations fully testable in EditMode CI.
