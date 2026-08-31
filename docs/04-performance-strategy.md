# 04 — Performance & Mobile Optimization Strategy

Target device class: ~2–3 GB RAM Android, Adreno 5xx / Mali-G71 class GPU, mid-range 2019 hardware. The gate-multiplier genre puts its heaviest load at its most dramatic moment (a huge crowd hitting a gate), so performance is a design constraint, not a polish task.

## Budgets (enforced per sprint, not at the end)

| Metric | Low tier | Mid tier | High tier |
|---|---|---|---|
| Frame rate | 30 fps floor | 60 fps | 60 fps |
| Simulated units (hard cap) | ~100 | ~200 | ~300 |
| Draw calls (gameplay) | < 60 | < 90 | < 120 |
| RAM (total process) | < 350 MB | < 450 MB | — |
| Cold start → main menu | < 8 s | < 6 s | — |
| Build size (initial download) | < 150 MB | — | — |

Tier is auto-detected on first launch (`SystemInfo` memory/GPU heuristics + a short boot benchmark), stored in the profile, and overridable in settings.

## 1. Crowd rendering — the central problem

**Never** one GameObject per unit. The crowd is *data*, drawn in bulk:

- **`CrowdController`** owns a `NativeArray<UnitData>` (position, formation slot, animation phase, alive flag). Formation slots use a **golden-angle spiral mapped onto a road-shaped envelope** so the crowd grows and shrinks visually from the center without reshuffling — adding 200 units on a `×` gate never reorders existing ones. The envelope is the load-bearing part: **width saturates at ~0.36 of a lane and never grows with the count**, because a crowd wider than its lane covers every lane at once and steering stops being visible. Count is carried by depth and density instead, with depth bounded by what the camera can actually frame behind the anchor.
- **Rendering:** `Graphics.RenderMeshInstanced` (or `RenderMeshIndirect` when unit counts get large) with a per-instance buffer of transforms + animation time. One material, one mesh → **one draw call for the whole crowd**.
- **Animation:** bake run/attack/death cycles into a **vertex animation texture (VAT)**; a URP Shader Graph samples position offsets per vertex from the texture using a per-instance time offset. No `Animator`, no CPU skinning, no `SkinnedMeshRenderer`. Per-instance phase offset prevents lockstep-robot look.
- **LOD by distance:** rear units use a lower-poly mesh variant and can drop to a billboard beyond a threshold. Because they're instanced by mesh, this is a second draw call, not hundreds.
- **Display-count inflation (R2):** `RunState.forceCount` is a `long` and is the source of truth for damage, UI, and boss math. Rendered bodies saturate at the tier cap; beyond it, growth is expressed by the counter, a scale bump on the Hero, and a VFX burst. Playtesters do not count sprites — they read the number.
- **Steering:** a single centroid target from `LaneTargetChanged` plus per-unit spring-damper toward its formation slot. Vectorized loop; move to Burst/Jobs **only if profiling shows it** (200 units of simple math is ~0.1 ms on the CPU budget — premature Jobs adds complexity for nothing).

**No per-unit physics** (R3). Gates/obstacles/enemy packs are trigger volumes tested against the crowd's centroid and bounding width; the outcome is arithmetic on `forceCount` plus pooled VFX.

## 2. Object pooling

A single generic pool used everywhere; nothing gameplay-related is ever `Instantiate`d mid-run.

```csharp
public class ObjectPool<T> where T : Component {
    // LIFO Stack<T> (cache-friendliest reuse), prewarm(n), Get(), Release(t),
    // inactive objects parented under a pool root, optional growth cap w/ warning log
}
public interface IPoolable { void OnSpawned(); void OnDespawned(); }  // reset state here, never in OnEnable
```

Pooled categories and prewarm counts (prewarmed during `RunLoading`, behind the transition mask, so no hitch is ever visible):

| Category | Prewarm | Notes |
|---|---|---|
| Unit visual slots | tier cap | only if any unit needs a GameObject (Hero, special units); crowd body is instanced data |
| Track chunks | 6 | recycled behind the camera |
| Gates | 12 | includes glyph/number text mesh |
| Obstacles / enemy packs | 20 | |
| Projectiles & spell VFX | 30 | |
| Floating damage numbers | 40 | TextMeshPro, pooled — a classic GC offender |
| Death/impact particles | 20 | |

**Treadmill world.** The track is built from pooled `ChunkDefinition` prefabs; chunks that pass behind the camera are released and re-acquired ahead. Either the player is quasi-stationary and the world scrolls, or a **floating-origin shift** re-centers the world every ~500 m to avoid float-precision jitter in long runs.

## 3. GC and CPU hygiene

- **Zero per-frame allocations in the run loop.** Verified with the Profiler's GC Alloc column; the target is a flat 0 B/frame during `RunnerLoop`. Common offenders to ban: LINQ in Update, string concatenation for the counter UI (use cached `TextMeshPro.SetText` with a number formatter), `foreach` over interfaces that box, `GetComponent` in Update, `Camera.main`.
- **Event-driven UI.** The force counter updates on change, not per frame; UI canvases are split so a counter change doesn't dirty the whole canvas.
- **One `Update` per system**, not per entity — `CrowdController.Tick()` loops the array itself.
- **Fixed timestep** raised to 0.033 s (physics is barely used) to cut FixedUpdate overhead.

## 4. Rendering & asset pipeline

- **URP mobile settings:** single realtime directional light; **baked/mixed lighting** for the environment; realtime shadows **off** on low tier (blob-shadow decals under the crowd instead), a single cascade on mid/high; HDR off on low tier; MSAA off (a lightweight FXAA/none instead); Depth/Opaque textures disabled unless a specific effect needs them; **Render Scale 0.7–0.8** on the low tier — the single highest-leverage knob for GPU-bound fill on this hardware.
- **Post-processing:** bloom only (it sells the emissive gates and spells, per R5), disabled entirely on low tier with emissive intensity compensated up.
- **Textures:** ASTC compression, atlased per environment set, mipmaps on, texture streaming for boss/environment sets.
- **Addressables** for level/boss/gear content, so build size stays under the download threshold and unused level sets aren't resident.
- **Shaders:** a small, shared set (crowd VAT, environment lit, emissive gate, UI); **shader variant stripping** + prewarm at boot to avoid mid-run compile hitches — a very common cause of "the game stutters the first time a spell is cast".
- **Audio:** compressed in memory, streaming for music, pooled AudioSources with a hard voice cap (~16).

## 5. Measurement discipline

- **Every sprint ends with a device profile pass** on the designated low-end reference phone — this is part of each sprint's definition of done, not a Sprint 4 activity.
- Unity Profiler over ADB + a lightweight in-build overlay (fps, frame time, draw calls, unit count, GC allocs) toggled by a debug gesture.
- A **soak test scene** driving 20 automated runs via scripted input intents (possible because gameplay listens to intents, not touches — see doc 02) to catch pool leaks and memory growth.
- Regression guardrail: a scripted worst-case scene (max crowd + boss + full VFX) whose frame time is recorded each sprint; a regression blocks the sprint from closing.
