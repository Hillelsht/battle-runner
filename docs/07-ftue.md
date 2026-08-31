# 07 — First-Time User Experience

Nothing in the game taught any control. That is not a polish gap, it is a retention
cliff: simulate a player who never learns to steer against the shipped level 1 content
and they take only the lane-0 gates, are reduced to **zero force by the enemy pack at
16.7 s**, and get the resurrect prompt. A first run that ends in death inside twenty
seconds is what this system exists to prevent.

## The four lessons

| # | Teaches | Armed when | Holds the run? | Satisfied by |
|---|---|---|---|---|
| 1 | Drag to steer | 8 m into the run — once the road has visibly moved | yes | the crowd **arrives** in another lane |
| 2 | Gate vocabulary | a gate within 20 m of the crowd's leading plane | no | any gate applied |
| 3 | Flick up to cast | an enemy pack within 12 m of the leading plane, spell ready | yes | `SpellSystem.Cast` |
| 4 | Flick down to block | the boss's first telegraph, shield ready | yes | `ShieldSystem.Raised` |

Only one prompt is ever on screen, and a step that has been resolved never returns.

## Why the prompts say "lift your thumb"

`GestureClassifier` is **one gesture per contact** by design — a touch that has
classified as `LaneDrag` can never emit a flick, which is what kills the
"dodge accidentally casts" bug class. The consequence is that a player steering with
their thumb held down *physically cannot cast*. Both flick prompts therefore lead with
`Lift your thumb, then swipe up/down fast`; without that line the prompt reads as broken
input.

## Holding the run

A held prompt sets the run's forward speed to zero for that frame. It does **not** touch
`Time.timeScale`, which appears nowhere in this project and should stay that way:

- cooldowns, the HUD, input and the ad service keep running on wall-clock time — a
  stalled rewarded-ad callback is the one failure that can genuinely wedge a run
- the crowd's steering spring still runs at full speed, so the steer lesson has live
  feedback while the world stands still
- gates, packs and the finish line all resolve against `CrowdController.FrontZ`, which
  is derived from the frozen anchor — so no crossing can fire during a hold

## It cannot strand anyone

Every step carries a six-second patience, drawn as a draining bar so the player can see
the game is about to move on. On expiry the step is marked taught and dismissed anyway.
`TutorialDirectorTests` proves this directly: it arms every step, ticks ten seconds of
frames with no player input at all, and asserts the run is released and the tutorial
completes. Zero and negative frame deltas are covered too, so a paused or clock-skewed
frame cannot rewind the deadline.

## Persistence

`PlayerProfile.TutorialMask` is a bitmask of resolved steps, added at schema **v3**.

The v2 → v3 migration marks every step taught: an existing save belongs to someone who
already played, and coaching a veteran is wrong. That makes the *new*-profile path the
dangerous one — `FileSaveService.NewProfile()` used to push a `SchemaVersion = 1` object
through `Migrate()`, which would have run that step and **silently skipped the tutorial
for every new player**. A fresh profile is now stamped at the current schema so no
migration runs; it initialises its lists inline, so it never needed the migrations'
null-healing.
