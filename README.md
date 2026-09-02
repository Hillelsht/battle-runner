# Battle Runner

A hybrid-casual Android game built in Unity: a 3-lane **gate-multiplier runner** (in the vein of *Count Masters* / *Mob Control*) fused with **dark fantasy RPG progression** — post-boss loot into 3 gear slots, one-tap Auto-Equip, stat points, and sub-5-second meta loops.

This repo contains the **playable greybox MVP**: the full loop (run → gates → boss → loot → stat points → save → next level) with procedural greybox art, a GPU-instanced crowd, and mock ad/IAP services behind the real monetization seams.

## Status

**Latest build: [v0.1.2](https://github.com/Hillelsht/battle-runner/releases/tag/v0.1.2)** —
[download the APK](https://github.com/Hillelsht/battle-runner/releases/download/v0.1.2/battle-runner-v0.1.2.apk)
(~30 MB, ARM64 / IL2CPP, debug-signed, sideloadable).

[CHANGELOG.md](CHANGELOG.md) is the live status page: what stage the project is at,
what each release changed, and what is still open. Keep it current — `tooling/check_docs.py`
enforces that docs move whenever code does (see [Keeping docs honest](#keeping-docs-honest)).

## Getting Started

**Requirements:** Unity **6.3 LTS** (`6000.3.22f1` or newer) with the **Android Build Support** module. Do not use Unity 6000.0.x below `6000.0.58f2` — those releases are affected by [CVE-2025-59489](https://unity.com/security/sept-2025-01). Any newer Unity 6 stream also works; the project upgrades forward on open, which is expected and safe.

1. Open Unity Hub → **Add project from disk** → select this repo folder.
2. First open: packages resolve, then the project **configures itself** — a URP pipeline asset is created and assigned (`Assets/Settings/`), and the content set is generated (`Assets/Content/` + `Assets/Resources/GameConfig.asset`). If anything looks unconfigured, run **BattleRunner → Setup Project (URP + Content)** from the menu bar.
3. Open `Assets/Scenes/Main.unity` and press **Play**.

The scene contains a single `Bootstrap` object; the camera, lighting, UI, track, and crowd are all constructed at runtime.

### Controls

| Input | On device | In the editor |
|---|---|---|
| Steer lanes | horizontal drag (positional) | hold left mouse + drag, or **←/→** (or A/D) |
| Cast spell | flick **up** (fast) | **↑** or W (or a fast upward mouse flick) |
| Raise shield | flick **down** (fast) | **↓** or S (or a fast downward mouse flick) |
| Perf overlay | 3-finger tap | F1 |

On a first run a coaching prompt introduces each control the moment it first matters. Every prompt times out on its own — nothing ever waits on you. **NEW GAME** on the main menu wipes the save and replays it (two taps, since it cannot be undone).

### Expected first-open behaviors (not bugs)

- `Assets/UniversalRenderPipelineGlobalSettings.asset` appears on its own — URP creates it.
- The Game view shows a "No cameras rendering" placeholder in **edit** mode, and the Scene view shows only an empty ground plane with a lone `Bootstrap` object. Everything — camera, lighting, UI, track, crowd — is created when you press **Play**, so switch to the **Game** tab to actually see the game.
- A `Library/` folder and `packages-lock.json` are generated locally (both git-ignored).

## Tests

- **In Unity:** Window → General → **Test Runner** → EditMode → Run All (140 tests: gate math incl. soft-cap overflow, the gesture confusion suite, loot distribution + pity, save migration + checksum, crowd math incl. the formation envelope and lane partition, boss sim, state machine).
- **Without Unity:** `dotnet test tooling/CoreTests/CoreTests.csproj` runs the identical test sources against the same core code (the core assembly is engine-free by design).
- **Serialized-file lint:** `python3 tooling/lint_unity_yaml.py` validates the hand-written scene/material/meta files.

## Android build

**Locally:** File → Build Profiles → Android → Switch Platform → Build. Package id `com.hillelsht.battlerunner`, portrait, min SDK 23.

The player targets **ARM64 with the IL2CPP backend** — required because current Android
devices are 64-bit and refuse to install ARMv7-only packages ("App not installed"), and
because Google Play mandates 64-bit. IL2CPP needs the **Android NDK** (installed with the
Android Build Support module); the first IL2CPP build is noticeably slower than Mono.

**In CI:** `.github/workflows/unity-ci.yml` runs on every push (core `dotnet test` +
YAML lint always; headless Unity EditMode tests when activation is configured).
`.github/workflows/release-apk.yml` builds the APK and publishes it to **Releases** —
run it from the Actions tab ("Release APK" → Run workflow → enter a tag like `v0.1.0`),
or by pushing a `v*` tag. It is deliberately not on every push: an IL2CPP Android build
takes 30–40 minutes of Actions quota.

Both need a **complete** Unity activation strategy under repo
**Settings → Secrets and variables → Actions**:

  `UNITY_EMAIL` and `UNITY_PASSWORD` are required for **every** licence type. This is not a
  configuration choice: GameCI's `activate.sh` only branches on
  `UNITY_SERIAL && UNITY_EMAIL && UNITY_PASSWORD` or on `UNITY_LICENSING_SERVER`, and
  `UNITY_LICENSE` is never even passed into the build container — it is parsed on the host
  purely to derive the serial. A `.ulf` on its own therefore cannot activate anything.

  | Licence | Secrets to set |
  |---|---|
  | Personal (free) | `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_LICENSE` (entire `.ulf` contents) |
  | Pro / Plus | `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL` |

  See the [game.ci activation docs](https://game.ci/docs/github/activation). The
  `licence-check` job prints which secrets are present (never their values) and fails with
  instructions if the set is incomplete; with no secrets at all, the Unity jobs simply skip.

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
| [07 — First-Time User Experience](docs/07-ftue.md) | The four coaching beats, why the run holds instead of pausing, and the proof it cannot strand a player |
| [08 — Progression](docs/08-progression.md) | The talent tree, why flat stat points were the wrong shape, the new run-facing stat axes, and save slots |
| [09 — Monetization setup](docs/09-monetization-setup.md) | The accounts only you can create, step by step, with hands-on vs. waiting time — and the 14-day Play Console clock to start today |

### Code layout

```
Assets/Scripts/Core      engine-free game logic (gestures, gate math, stats, loot, save, crowd math, boss sim, flow)
Assets/Scripts/Data      ScriptableObject definitions + event channels + ContentFactory
Assets/Scripts/Gameplay  MonoBehaviours: bootstrap, states, input router, track, crowd, combat
Assets/Scripts/Meta      services (save, mock ads/IAP, battle-pass seam) + code-built uGUI screens
Assets/Scripts/Editor    URP auto-setup, content generation, and build guards
                         (PipelineGuard, ShippedShaderCheck) that fail a build rather
                         than let it ship an unrenderable player
Assets/Tests/EditMode    NUnit suite (runs in Unity Test Runner AND under plain dotnet)
tooling/                 mirrored csprojs for Unity-less testing, meta generator,
                         YAML lint, the docs checker, and its Claude Code gate
.githooks/               versioned git hooks (pre-push docs check)
```

### Build guards

v0.1.0 shipped an entirely magenta player while the build reported `Errors: 0`.
Nothing was going to catch that except a person installing the APK, so two guards
now fail the build outright and a third degrades safely at runtime:

| Guard | When | Prevents |
|---|---|---|
| `PipelineGuard` | pre-build | Building with no render pipeline assigned, which strips every URP shader variant and renders the whole game magenta |
| `ShippedShaderCheck` | post-build | The crowd shader packing as an empty stub (< 2 KB) even when the build reports `Errors: 0` |
| `ShaderSafety` | runtime | A shader unusable on the active pipeline — substitutes a stock shader and logs, instead of drawing magenta |

## Keeping docs honest

Docs rot quietly: a test count drifts, a Unity version moves in `ProjectSettings`
but not here, a new doc lands that nothing links to. `tooling/check_docs.py`
cross-checks the facts that are machine-verifiable and refuses a push when code
changed without any docs change.

```bash
python3 tooling/check_docs.py                      # facts only
python3 tooling/check_docs.py --range HEAD~3..HEAD # + staleness for a commit range
```

It verifies the documented test count matches the real `[Test]` count, the README
names the pinned Unity version, every file in `docs/` is linked from the README (and
every README link resolves), every `Assets/Scripts/*` directory appears in the code
layout, and the README's newest version reference matches the newest `CHANGELOG.md`
release.

### Three places it runs

| Layer | File | Fires | Skippable? |
|---|---|---|---|
| Claude Code | `.claude/settings.json` → `tooling/hook_docs_gate.sh` | when an agent proposes a `git push` | yes, and only affects Claude Code |
| Git | `.githooks/pre-push` | on any real `git push` | `git push --no-verify` |
| CI | `docs-check` job in `unity-ci.yml` | on every push and PR | no |

The first two are conveniences that fail fast on your machine; CI is the one that
actually holds the line, so forgetting to enable a local hook cannot let stale docs
through.

**Enable the git hook once per clone** (the Claude Code hook needs no setup — it
lives in the repo and loads with the project):

```bash
git config core.hooksPath .githooks
```

For a change that genuinely needs no docs — a pure rename, say — put `[skip-docs]`
**on its own line** in a commit message and the staleness rule stands down (the fact
checks still run). It has to stand alone, git-trailer style: matched as a substring,
any commit that merely discusses the escape hatch would disarm it.

## Monetization status

Rewarded-ad touchpoints (loot doubling, resurrect) and the IAP seam are **implemented against mock services** — flows work end to end with a simulated ad delay. Binding LevelPlay/AdMob and Unity IAP is a post-greybox step and requires network accounts. The battle pass ships as an interface seam only, by design (see doc 01, R7).
