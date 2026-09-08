# 13 — Audio: fifteen sounds and two beds, all synthesised

## The starting point was zero

Not "placeholder audio". Nothing. A grep for `AudioSource`, `AudioClip`, `AudioListener`,
`AudioMixer` or `PlayOneShot` across the whole repository returned no hits in any `.cs` file.
There were no audio assets of any kind, no `ProjectSettings/AudioManager.asset`, and — the
detail that would have wasted an afternoon — **no `AudioListener` anywhere**. `Main.unity`
contains one GameObject and no camera; the runtime camera built in `GameBootstrap.cs:197`
never added one. Every source the game might have played would have played to nobody.

The only audio-adjacent facts were `com.unity.modules.audio` sitting unused in
`Packages/manifest.json`, and `docs/04-performance-strategy.md:71` specifying a budget for a
system that did not exist: *"compressed in memory, streaming for music, pooled AudioSources
with a hard voice cap (~16)"*.

## Why it is synthesised

**No free audio host is reachable from the build container.** Freesound, OpenGameArt and
kenney.nl all fail to connect, and the Kenney mirror that supplies the scenery models carries
2D, UI, icons and 3D only — no audio.

It is also the move this project already makes everywhere else. The meshes, the sky, the road
surface, the cobbles and every UI sprite are generated rather than imported, for the same
reasons: nothing to license, nothing to import, deterministic, and reviewable as a diff.

`tooling/synth_audio.py` needs **numpy and the standard library** — no ffmpeg, no sox, no
scipy, no soundfile, none of which are present here, and requiring them would mean the script
could not be re-run. Output is 22.05 kHz mono 16-bit WAV: **2.07 MB in the repo**, which Unity
re-encodes to Vorbis for about 200 KB on the device.

**What synthesis can and cannot do.** Drones, wind, impacts, bells and stingers synthesise
well. Melody does not. This is a dark-ambient bed and a set of readable impacts, not a score.
Every clip loads **by name** through `Resources`, so any single file can be swapped for a real
recording later without touching a line of code.

### The fifteen cues

| Cue | What it is |
|---|---|
| Gate add | A two-partial bell. The sound heard most in a run, so it is short and sits low in the mix |
| Gate multiply | A four-note rising arpeggio. The best thing that happens in a run should go *up* |
| Gate subtract | A pitch-swept thud with a dirty edge — loss, inside the quarter-second available to read it |
| Enemy bite | Short, dry, percussive; it fires per pack and must not accumulate into mush |
| Spell cast | An upward whoosh: the wind-up the player feels in their thumb |
| Spell hit | Low body, bright transient, long tail. The only run sound allowed to be genuinely loud |
| Shield raise | A rising hum that stops rather than fades, so "I am covered" has an edge |
| Shield block | The brightest transient in the game — four metal partials over a filtered strike |
| Boss telegraph | A rising drone under a 3.2 Hz heartbeat |
| Boss blow | The loudest cue in the table, and the highest priority in the pool |
| Boss hit | Small and dry, pitched above the boss's own register so it cuts through |
| Boss death | Three descending waves, matching the three shockwaves the VFX already draws |
| Loot reveal | A bright major chord. The one unambiguously *good* sound in the game |
| UI tap | A soft wooden click, deliberately quiet — it fires on every button |
| Round start | A low horn. The only moment the player is *told*, rather than shown, that somewhere new has begun |

Three implementation details that are not cosmetic:

- **Sweeps integrate the frequency.** Writing `sin(2π·f(t)·t)` for a pitch sweep produces an
  audible artefact; `sin(2π·∫f dt)` is phase-continuous and does not.
- **Every one-shot ends at silence.** Measured on the first pass, `shield_raise` ended at 0.46
  amplitude — a step discontinuity, which is a click, on every single block. All one-shots now
  go through a normalise-and-ramp helper. A test of the written files confirms every `sfx_*`
  starts and ends below 0.01.
- **The beds cross-fade head-to-tail with equal power.** A bed that clicks once every twenty
  seconds is worse than no bed: the ear locks onto the period and hears nothing else. Equal
  power rather than linear, because a linear cross-fade of two uncorrelated signals dips about
  3 dB in the middle and the loop point becomes audible as a dropout instead of a click.

## The system

`AudioDirector` implements `IAudioService`, and is a plain service like the ad and save
services rather than a MonoBehaviour — giving it a component would put it in the scene.
`GameFlowController` drives its `Tick` with **unscaled** time, so a cross-fade cannot stall.

- **An `AudioListener` on the camera**, which is where the player's ears are. One line, and
  without it nothing below is audible.
- **Sixteen pooled voices**, prewarmed at boot, never allocated during a run — doc 04's own
  number. They live under a `DontDestroyOnLoad` root and *not* under `ArenaRoot`, because
  `CreateArena` ends with `ArenaRoot.SetActive(false)` and an `AudioSource` on an inactive
  GameObject does not play. That would have made the menus silent and delayed the first round's
  music by a whole round.
- **Eviction is by priority, not by age.** A boss blow lost because the army was passing gates
  is a fairness problem, not an audio one, so a louder cue takes the quietest lower-priority
  voice. A test pins that no gate, bite or UI cue can ever evict a boss blow or a boss death.
- **A retrigger gate per cue.** Weaving a Ladder fires six add-gates in well under a second,
  and a dozen identical bells inside 200 ms is a buzz rather than six times the payoff. A test
  keeps the gate cues under 0.12 s so a burst is never swallowed, and the rare long cues above
  0.3 s so they cannot stack on themselves.
- **2D, not spatialised.** The camera looks down a corridor from behind the army; panning a
  gate chime by its lane is a cue the player cannot act on and a distraction from one they can.

Sound is triggered from the events, not the inputs. `ShieldSystem.Raised` rather than the flick
handler, because `TryRaise` silently refuses while on cooldown and a sound on a shield that did
not go up teaches the player that the flick worked when it did not. The boss telegraph fires on
the single frame the wind-up window opens, not in the per-frame telegraph update, which would
retrigger it sixty times a second and turn the one warning the player gets into a drone.

The UI tap is added inside `UiFactory.ActionButton` — the one place every button in the game is
built — so a screen written later is audible without anyone remembering to make it so.

## Eight worlds, one bed

A twenty-second bed is most of a megabyte, and eight of them would outweigh the entire rest of
the project. So **one bed is bent per world**, the same "one asset, themed" move the sky, the
road and the scenery pack already make.

`Core/Audio/MusicMood` derives the bend from two things the player can already *see*:

- **How far they can see.** Fog is the strongest cue of enclosure a world has, so `FogEnd`
  drives a low-pass cutoff. The Sunken Crypt fogs out at 105 m and sounds muffled; the Bone
  Wastes at 185 m sounds open. A test asserts exactly that pair.
- **How cold the sky is.** The zenith's blue-to-red balance drives pitch — using the balance
  rather than the brightness keeps it independent of how dark a world is. The Frozen Reach sits
  above Ember Fields, and a test asserts that too.

Deriving rather than authoring means a ninth world cannot ship sounding exactly like the first.
A test requires every pair of the eight to differ, and another proves the derivation never
leans on its own clamps — which would mean two worlds silently landing on the same rail.

A second looping source carries the boss layer — a heartbeat at 84 bpm, a fifth, and a hint of
choir — cross-faded in when a fight starts and out when it ends or the loot card appears.

## Where the preference lives, and why not the obvious place

`PlayerProfile` is **per save slot**, and `GameContext.SaveProfile()` refuses to write while
`ActiveSlot < 0`. A mute preference stored there would be unwritable on exactly the screens
that offer the button, and would reset when the player changed slot. It lives in `PlayerPrefs`,
which nothing else in the project uses.

`UiFactory` has no `Toggle` and no `Slider` — the entire UI is `Text`, `Image` and `Button`. So
the main menu gets a third `ActionButton` that swaps its own label between `SOUND ON` and
`SOUND OFF`, which is the arm/disarm pattern `SlotSelectScreen` and `SkillTreeScreen` already
use. No new widget.

## The coupling that had to be pinned

Core names every clip it will load; the Python synthesiser decides which files exist. Nothing
at compile time connects them, and a name drifting on either side produces exactly one symptom:
silence, with a warning nobody reads.

`tooling/lint_unity_yaml.py` now checks the two tables against each other **and** against the
files on disk, in the same place it already pins the crowd's scale constants against the
shader's. Verified against a deliberate one-character drift: it reports all three consequences
(the clip Core wants that is not made, the clip made that nothing plays, and the missing file).
