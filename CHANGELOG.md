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
| Content | 5 levels, 2 bosses, 15 gear items, 4 rarities |
| Art | Greybox — procedural meshes, code-built uGUI, no imported assets |
| Tests | 67, green under both `dotnet test` and Unity's Test Runner |
| Android build | Automated: ARM64 / IL2CPP APK published to Releases |
| Monetization | Rewarded-ad and IAP flows wired to **mock** services only |
| Docs | Enforced — `tooling/check_docs.py` gates pushes locally and in CI |
| Not started | Real ad SDK, FTUE, analytics, battle pass, art pass |

**Known open item:** v0.1.1's on-device appearance has not been confirmed by a
human. The build is verified correct (URP active, shader variants shipped, tests
green) but nobody has looked at a frame since the fixes.

**Unreleased since v0.1.1:** documentation is now enforced rather than trusted.
`tooling/check_docs.py` cross-checks the facts embedded in the docs (test count,
pinned Unity version, doc index, code layout, newest release) and requires a docs
change whenever code changes. It runs in three places — a Claude Code `PreToolUse`
gate, the `.githooks/pre-push` git hook, and the `docs-check` CI job — so this file
cannot silently fall behind the code again. See
[Keeping docs honest](README.md#keeping-docs-honest).

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
