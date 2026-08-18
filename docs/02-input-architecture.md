# 02 — Input Architecture

Goal: lane control and spell gestures on the same thumb, with no perceived latency and a near-zero false-trigger rate.

## Design decision: drag vs. flick, not swipe vs. swipe

As argued in [R1](01-technical-risks.md#r1-gesture-ambiguity-between-lane-control-and-spell-casting--highest-gameplay-risk), the two inputs are separated by *motion signature*, not by axis alone:

| Input | Motion signature | Result |
|---|---|---|
| Lane control | **Positional horizontal drag** — sustained contact, finger X maps continuously to crowd target X | Crowd steers smoothly across the 3 lanes (lane snapping applied on top of a continuous offset) |
| Shield | **Downward flick** — brief contact, high velocity, dominant Y− | Temporary shield buff |
| Spell | **Upward flick** — brief contact, high velocity, dominant Y+ | High-damage cast |

A drag is slow and long-lived; a flick is fast and dies in ~100 ms. That difference lets the classifier commit almost immediately.

## Component layout

```
InputRouter (MonoBehaviour, the ONLY class polling touches)
  └── GestureClassifier (pure C#, no UnityEngine dependencies beyond structs)
        emits → InputIntent events
                  ├─ LaneTargetChanged(float normalizedX)   // continuous, every frame while dragging
                  ├─ FlickUp()                               // spell intent
                  └─ FlickDown()                             // shield intent

ScriptableObject event channels (see doc 03)
  ├─ LaneTargetChannel      ← InputRouter publishes; CrowdController subscribes
  └─ CombatIntentChannel    ← InputRouter publishes; SpellSystem / ShieldSystem subscribe
```

- **`InputRouter`** uses the New Input System's `EnhancedTouch` API. It samples the primary touch each frame, normalizes positions/velocities by `Screen.dpi` (thresholds are authored in centimeters, not pixels — critical across the Android device spread), and feeds samples to the classifier.
- **`GestureClassifier`** is a pure C# state machine operating on injected `(position, time)` samples. Because it has no MonoBehaviour dependency, it is **unit-testable in EditMode**: recorded touch traces (clean drags, sloppy diagonal flicks, taps) become regression tests for the confusion matrix.
- Gameplay systems **never read touches**. They subscribe to intent events. This gives free keyboard/mouse simulation in-editor (A/D → lane target, W/S → flicks) and lets bots drive the game for soak tests.

## Classifier state machine

```
        touch down                  displacement ≥ D_commit (~0.8 cm)
Idle ─────────────▶ TouchActive ───────────────────────────────┐
                        │                                      ▼
                        │ touch up before D_commit      axis test:
                        │ and duration < 150 ms         |dy| > 1.5·|dx| ? ──▶ VerticalCandidate
                        ▼                                      │
                      (Tap — reserved,                          └─ else ──▶ LaneDrag
                       currently ignored)

LaneDrag:            every frame emit LaneTargetChanged(fingerX). Vertical motion is IGNORED
                     until touch up. (No spell can fire mid-drag — by design; see notes.)

VerticalCandidate:   confirm on touch up OR when flick velocity ≥ V_min (~25 cm/s):
                       dy > 0 → FlickUp, dy < 0 → FlickDown.
                     If velocity never reaches V_min and contact persists > 200 ms,
                     reclassify as LaneDrag (it was a sloppy drag start).

Any state ──touch up──▶ Idle
```

### Tuning constants (authored on an `InputSettings` ScriptableObject)

| Constant | Start value | Purpose |
|---|---|---|
| `D_commit` | 0.8 cm | Displacement before classification — small enough to be imperceptible |
| Axis ratio | `|dy| > 1.5·|dx|` | Bias toward LaneDrag; a false dodge is cheaper than a wasted spell |
| `V_min` | 25 cm/s | Minimum flick velocity — separates flicks from slow vertical wander |
| Flick window | 200 ms | Max contact time for a flick before demotion to drag |

All four are data, not code — tunable per playtest build without recompiling.

## Why this has no perceived lag

- Lane control commits at 0.8 cm of travel and then streams *continuously* — there is no discrete "swipe recognized" moment to wait for. The crowd's steering interpolation (~0.1 s smoothing) visually absorbs the classification window entirely.
- Flicks resolve on velocity threshold, typically 60–120 ms after touch down — comparable to the spell's own wind-up animation, so the cast *feels* instant.

## False-trigger safety rails

1. **Asymmetric costs, asymmetric thresholds.** The axis ratio biases toward LaneDrag because a mis-fired spell wastes a cooldown (player-visible loss) while a mis-read drag self-corrects in the next frame.
2. **Cooldown gating at the system, not the classifier.** `SpellSystem`/`ShieldSystem` ignore intents while on cooldown and pulse the UI icon — the classifier stays stateless about gameplay.
3. **One gesture per contact.** A touch that classified as LaneDrag can never emit a flick; the player must lift to cast. This single rule eliminates the entire "dodge accidentally casts" class of bugs.
4. **Dead zone at screen edges** (~0.5 cm) where Android navigation gestures live.

## Acceptance tests (Sprint 2 exit)

- EditMode: recorded-trace suite ≥ 98% correct classification on clean gestures, ≥ 90% on deliberately sloppy diagonal traces, zero flicks emitted from any drag trace.
- On device: 20-run play session by a fresh player with logged intents — zero unintended spell casts reported, median flick-to-cast latency < 120 ms.
