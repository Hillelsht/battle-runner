# Battle Runner

A hybrid-casual Android game built in Unity: a 3-lane **gate-multiplier runner** (in the vein of *Count Masters* / *Mob Control*) fused with **dark fantasy RPG progression** — post-boss loot into 3 gear slots, one-tap Auto-Equip, stat points, and sub-5-second meta loops.

This repo contains the **playable greybox MVP**: the full loop (run → gates → boss → loot → stat points → save → next level) with procedural greybox art, a GPU-instanced crowd, and mock ad/IAP services behind the real monetization seams.

## Getting Started

**Requirements:** Unity **6.3 LTS** (`6000.3.22f1` or newer) with the **Android Build Support** module. Do not use Unity 6000.0.x below `6000.0.58f2` — those releases are affected by [CVE-2025-59489](https://unity.com/security/sept-2025-01). Any newer Unity 6 stream also works; the project upgrades forward on open, which is expected and safe.

1. Open Unity Hub → **Add project from disk** → select this repo folder.
2. First open: packages resolve, then the project **configures itself** — a URP pipeline asset is created and assigned (`Assets/Settings/`), and the content set is generated (`Assets/Content/` + `Assets/Resources/GameConfig.asset`). If anything looks unconfigured, run **BattleRunner → Setup Project (URP + Content)** from the menu bar.
3. Open `Assets/Scenes/Main.unity` and press **Play**.

The scene contains a single `Bootstrap` object; the camera, lighting, UI, track, and crowd are all constructed at runtime.

### Controls

| Input | On device | In the editor |
|---|---|---|
| Steer lanes | horizontal drag (positional) | hold left mouse + drag, or A / D |
| Cast spell | flick **up** (fast) | W (or fast upward mouse flick) |
| Raise shield | flick **down** (fast) | S (or fast downward mouse flick) |
| Perf overlay | 3-finger tap | F1 |

### Expected first-open behaviors (not bugs)

- `Assets/UniversalRenderPipelineGlobalSettings.asset` appears on its own — URP creates it.
- The Game view shows a "No cameras rendering" placeholder in **edit** mode — the camera is built at runtime; press Play.
- A `Library/` folder and `packages-lock.json` are generated locally (both git-ignored).

## Tests

- **In Unity:** Window → General → **Test Runner** → EditMode → Run All (64 tests: gate math incl. soft-cap overflow, the gesture confusion suite, loot distribution + pity, save migration + checksum, crowd math, boss sim, state machine).
- **Without Unity:** `dotnet test tooling/CoreTests/CoreTests.csproj` runs the identical test sources against the same core code (the core assembly is engine-free by design).
- **Serialized-file lint:** `python3 tooling/lint_unity_yaml.py` validates the hand-written scene/material/meta files.

## Android build

**Locally:** File → Build Profiles → Android → Switch Platform → Build. Package id `com.hillelsht.battlerunner`, portrait, min SDK 23.

**In CI:** `.github/workflows/unity-ci.yml` runs on every push:
- core `dotnet test` + YAML lint — always;
- headless Unity EditMode tests + an Android APK artifact — once you add a Unity license secret (one-time): repo **Settings → Secrets and variables → Actions** → add `UNITY_LICENSE` (the contents of your `.ulf` file — see [game.ci activation docs](https://game.ci/docs/github/activation)), or `UNITY_EMAIL` + `UNITY_PASSWORD`. Until then those two jobs skip themselves and stay green.

## Architecture

Read in order — the risk critique deliberately comes first:

| Doc | Contents |
|---|---|
| [01 — Technical Risks & Design Bottlenecks](docs/01-technical-risks.md) | The concept's failure modes and the mitigations this codebase commits to |
| [02 — Input Architecture](docs/02-input-architecture.md) | Drag-vs-flick gesture separation; the classifier is pure C# and fully unit-tested |
| [03 — System & Data Architecture](docs/03-system-data-architecture.md) | ScriptableObject data model, save model, game state machine |
| [04 — Performance & Mobile Optimization](docs/04-performance-strategy.md) | Instanced crowd, pooling, device-tier budgets |
| [05 — MVP Roadmap](docs/05-mvp-roadmap.md) | The four-sprint plan this build implements the core of |
| [06 — Greybox Implementation Notes](docs/06-greybox-implementation-notes.md) | What this build ships, conscious deviations, and what's next |

### Code layout

```
Assets/Scripts/Core      engine-free game logic (gestures, gate math, stats, loot, save, crowd math, boss sim, flow)
Assets/Scripts/Data      ScriptableObject definitions + event channels + ContentFactory
Assets/Scripts/Gameplay  MonoBehaviours: bootstrap, states, input router, track, crowd, combat
Assets/Scripts/Meta      services (save, mock ads/IAP, battle-pass seam) + code-built uGUI screens
Assets/Scripts/Editor    URP auto-setup + content generation
Assets/Tests/EditMode    NUnit suite (runs in Unity Test Runner AND under plain dotnet)
tooling/                 mirrored csprojs for Unity-less testing, meta generator, YAML lint
```

## Monetization status

Rewarded-ad touchpoints (loot doubling, resurrect) and the IAP seam are **implemented against mock services** — flows work end to end with a simulated ad delay. Binding LevelPlay/AdMob and Unity IAP is a post-greybox step and requires network accounts. The battle pass ships as an interface seam only, by design (see doc 01, R7).
