# 13 — Audio: fifteen cues, twenty-one files, and two beds played on real instruments

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
could not be re-run. Output is 22.05 kHz mono 16-bit WAV: **2.58 MB in the repo** across 21
files, which Unity re-encodes to Vorbis for roughly a quarter of a megabyte on the device.
**78% of that is the two 24-second beds**; the fifteen cues and their six variants come to
0.56 MB between them.

**What synthesis can and cannot do.** This was originally written as "melody does not", and
that was wrong — it was a statement about the first attempt, not about synthesis. What the
first pass produced was *ambience*: a stack of detuned sine drones, filtered noise for wind and
a slow amplitude swell. Played back it was correctly described as *"just a noize, not a music,
like an ocean sound"*, and that description was accurate, because it contained no notes.

It plays notes now. What is still true is that it will not sound *recorded*: this is a small
instrument in a small room, not an orchestra, and no free orchestra is reachable from here.
Every clip loads **by name** through `Resources`, so any single file can be swapped for a real
recording later without touching a line of code.

## The synthesis had a ceiling, and it was reached

The first pass replaced a noise drone with composed music — real harmony, real notes, played on
a Karplus-Strong string model. Told plainly that it was still bad, the honest answer is not a
better oscillator. **Karplus-Strong is a plucked string and a summed sine stack is a pad, but
neither of them is a cello.**

So the beds are no longer synthesised. They are **played**, on a General MIDI bank, by a
SoundFont renderer written for this project in `tooling/sf2.py`.

**The licence covers exactly this use.** GeneralUser GS v2.0.3: *"You may use GeneralUser GS
without restriction for your own music creation, private or commercial"*, and the samples
*"allow full use in music production, including the ability to make profit from musical
recordings created with GeneralUser GS"*. A rendered bed **is** a musical recording created with
it. No share-alike, no attribution requirement, no non-commercial clause.

**It is a build-time dependency and nothing else.** The 32 MB bank is downloaded into
`tooling/.cache` — already git-ignored — and never committed. Playing it *at runtime* would mean
bundling FluidSynth or TinySoundFont as a native `.so` per ABI, 30 MB resident and +32 MB of
install size on a title where install size is the most conversion-sensitive number there is;
that trade is indefensible. Rendering offline costs none of it, and the device still sees two
WAV files. It is the same move the meshes, the sky, the road surfaces and the UI sprites already
make: generate offline, commit the output, ship no tool.

`sf2.py` implements preset and instrument zone resolution, key ranges, the sample loop, tuning
and the volume envelope — the things that decide whether a note sounds like the instrument.
Modulators, the low-pass filter, LFOs, velocity layers and pan are deliberately not implemented:
the mix is mono, and the rest buys detail a 22 kHz bed under a runner cannot carry. Verified by
autocorrelation against the equal-tempered frequency: **nylon guitar, cello, strings, choir,
harp, double bass and oboe all land within 7 cents**. (Timpani reads wildly off, because a
timpani is a drum — its autocorrelation finds a membrane mode, not a pitch.)

### What plays what

The dark-fantasy town theme everyone actually means is a **solo acoustic guitar over a held low
string** — not an orchestra, and not a synthesiser pretending to be one.

| | ambient bed | boss bed |
|---|---|---|
| melody | nylon guitar, arpeggiated | fast strings, block chords |
| root | cello | double bass |
| pad | slow strings | — |
| voice | concert choir, barely there | concert choir |
| pulse | timpani on the bar and half-bar | timpani at 80 bpm |

Rendered at **2× and decimated**. Linear-interpolating a 44 kHz violin sample straight down to
22 kHz aliases its bow noise into audible grit; low-passing first and dropping every other
sample costs nothing offline and is the difference between strings and sizzle.

**The balance was measured, not guessed.** The first render put **43% of the bed's energy under
160 Hz and 8% in the guitar's own register** — the cello and the timpani were burying the only
line that moves. The guitar went up an octave and up in level, the cello and timpani came down,
and it now reads 17% / 67% / 14% across bass, low-mid and mid. The boss bed had the same problem
in reverse: normalising to a peak set by timpani transients left it at 0.087 RMS against the
ambient bed's 0.131 — **a fight layer quieter than the calm one**. Sustained content up,
transients down, and it sits at 0.114 with 59% of its weight in the bass, which is what a boss
theme should feel like.

If the bank cannot be downloaded, the synthesised beds are still there as a fallback and the
script **says so out loud** — a quietly inferior file that looks identical in a diff is worse
than a failure.

### The mode, and the room

Two pieces survive from the synthesised version, and both still earn their place.

- **A Schroeder reverb** — four parallel combs into two series allpasses, both computed a
  delay-period at a time for the same reason. The comb delays are **mutually prime on
  purpose**: combs at related delays reinforce the same partials, and the "room" then comes
  out as a ringing pitch rather than as a space, which is the commonest way a hand-rolled
  reverb sounds wrong.
- **D natural minor** — the Aeolian mode, and deliberately *not* the harmonic minor a
  "dramatic" progression reaches for. The ambient bed's `v` chord stays **minor**, so the
  harmony never resolves and the loop has no seam the ear can find. The boss bed raises that
  third to a major `V`, and the C sharp against a D minor tonic is the one interval in the key
  that genuinely wants to resolve — the difference between "somewhere dark" and "something is
  about to happen".

### The beds

`mus_bed` is twenty-four seconds: eight bars of **i–i–VI–VI–iv–iv–v–v**, four arpeggiated
guitar voices per bar over a bowed cello root, with timpani on the bar and the half-bar. Three
seconds a chord is slow enough that the progression reads as atmosphere rather than as a tune
the player tires of on the fortieth round.

`mus_boss` is the same length, key and room so the two cross-fade at any point without a key
change — which would be more distracting than the fight. What changes is the harmony
(**i–VI–iv–V**, twice) and an 80 bpm timpani pulse.

**The loop wraps its tail rather than cross-fading.** The equal-power cross-fade the old beds
used is right for ambience and wrong for music: it overlaps the last bar with the first, so two
different chords sound at once for a second and a half, once per loop, forever. Each bed is now
rendered longer than its musical length and the overhang — the final chord's decay and the
reverb tail — is **added back at the head**, which is exactly where that sound belongs when the
loop comes round. Sample-accurate, no fade, harmony intact.

Every voice in the bed now decays to nothing on its own, so nothing needs phase-snapping to
survive the wrap; the `wrapping()` helper stays for the synthesised fallback, which still has a
choir pad that never decays. Measured, the two loops join at **0.17× and 0.02×** of the local
99th-percentile sample step — inaudible.

### Five chords were wrong, and a spectral check is what caught every one

Getting an interval wrong does not throw, does not sound obviously broken, and changes the
**mode**. The first pass had `Gm` as `(-7, -3, 0, 5)` — a B natural, which is G *major* — and
both A chords with a C sharp, which is A major and therefore precisely the harmonic-minor
leading note the ambient bed is documented as not using.

An FFT of the rendered file found it: the G minor bar's strongest partial sat at **370.5 Hz**,
and 370.5 is F sharp, not the F natural a G minor chord is made of. The chord table now writes
the semitone map out in full above it.

It happened twice more when the harp cues were written. `loot_reveal`'s docstring says D major
and it was playing **E major**; `gate_multiply` says D minor and it was playing **E minor** —
both off by a whole tone, both invisible in review, both caught by measuring the rendered file
against the note names its own comment claimed. After the fix, every bar and every cue's
strongest partials are the chord tones written down for them.

The same check was almost fooled a second time. A first "seam ratio" compared the wrap
discontinuity against the file's **mean** sample-to-sample step and reported the boss bed at
2.9× — which reads as a click. Measured against the local 99th percentile step, where a
transient at the loop point actually lives, it is **0.17×**. Same class of mistake as the
texture seam metric in doc 14, and caught the same way.

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

### Attack, body, tail — and a room

Every cue in the first pass was one waveform times one envelope, which is why "the effects
sounds are too simple" was a fair description. A real impact has a transient that is almost all
noise and lasts a few milliseconds, a body that carries the pitch, and a tail that carries the
size; one envelope over all three makes them decay together, and decaying together is exactly
what makes a sound read as synthetic. `layer()` sums separately-enveloped stages. A gate
subtract is now grit gone in 30 ms, a swept body in 110, and a filtered rumble carrying on for
a third of a second — that last stage is what reads as **weight**.

`room()` sends a little of each cue through the **same** Schroeder network the music uses. This
is the largest single change to how the effects sound, and it is not an effect, it is a *place*:
fifteen dry one-shots over a bed with a hall on it is a soundboard triggered next to a score.
It is deliberately small, and deliberately **not** applied to the cues that fire several times
a second — a tail on a sound heard six times in a second is mud, not depth, so `enemy_bite` and
`ui_tap` stay dry.

### Variants, for the four cues heard most

A gate add fires several hundred times in a run, and no amount of pitch jitter stops one
waveform heard that often from flattening into a beep the ear stops registering as an event.
`CueMix.Variants` declares how many recordings exist and `AudioDirector` picks one per play.
They are separate **syntheses**, not the same sample retuned: the three gate adds measure
fundamentals of 874.8, 880.2 and 885.6 Hz with an RMS difference between them larger than the
signal's own RMS.

Index 0 is the base name, so adding a variant to an existing cue can never rename the file the
game already ships — a test pins that, because the symptom would be silence on a cue that used
to work. A missing variant falls back to the base clip rather than to null, because an event
that *sometimes* makes no sound reads as a gameplay bug rather than as an asset problem.

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
shader's. It expands variant counts the same way `CueMix.ClipAt` does, because a cue declaring
three variants against a synthesiser that makes two plays silence a third of the time.

Verified against a deliberate one-character drift, and against a deliberate variant-count
drift: both report every consequence (the clip Core wants that is not made, the clip made that
nothing plays, and the missing file). The clip-name scan is scoped to the `SOUNDS` table
because the first version matched the argparse calls further down the same file and reported
that the synthesiser makes a clip called `--report`.


# The third attempt at the music, and why the first two were not badly played

The report after the second pass was *"music is bad"* — the same words as after the first.

The second bed is not badly played. It is eight bars of D natural minor on a nylon guitar over
a cello, correctly voiced, in a real room, with its chords verified by FFT. **It is the wrong
music.** It is a twenty-four-second ambient loop at roughly forty beats a minute, playing under
a forty-second sprint in which the player makes a lane decision every two seconds. A score that
slow does not merely fail to support that — it works against it, because the tempo the ear is
given is the tempo the hands expect.

So the run bed is now **eighty-seven seconds of D aeolian at 132 BPM**: a frame-drum and
low-string ostinato under a modal melody, in four sections.

| bars | section | what plays |
|---|---|---|
| 0–7 | the engine starts | drums and bass only |
| 8–23 | the tune | melody on fast strings, full kit |
| 24–31 | the breakdown | drums thin, nylon guitar alone |
| 32–47 | the return | melody an octave up, everything in |

Four sections rather than one loop twelve times, because a four-bar figure repeated for ninety
seconds is the thing the ear times and then stops hearing — the same failure the ambient loop
had, at a different speed. The bed is `loop = true` and is never restarted per round, so a
player hears the whole arrangement across successive rounds rather than the first twenty
seconds forty times.

The progression is **i – VII – VI – VII** (Dm – C – Bb – C). The VII is a whole tone *below* the
tonic and pulls back up to it without the leading-note pull of a harmonic-minor V — a run should
not sound like it is resolving every four bars. That pull is what the boss bed keeps.

## The drums are synthesised, and the reason is worth recording

GeneralUser-GS has a standard GM kit on bank 128, and this project's minimal SoundFont reader
resolves **exactly one of its keys**. Probed across thirty-five kit keys, only the crash cymbal
sounds; every other key falls outside the zone the reader picks and returns silence.

Fighting the reader for a kick drum is the wrong trade when a membrane is four lines of
arithmetic: a noise burst through a resonant band for the skin, a pitched thump an octave below
for the body, fast exponentials on both. It also lets the drum be **tuned to the key**, which a
sampled kit cannot be.

## What measuring the render caught, again

Three things, and two were real bugs that sounded plausible in the source:

- **The chords were never played.** `DRIVE_CHORDS` holds three voices per bar and the code used
  `chord[0]` — the root — and nothing else. An FFT of bar 0 heard a bare D–A fifth, which is
  neither major nor minor; bar 3 had no E in it at all. The progression existed only in the
  comments. The cello now plays all three voices.
- **The pulse was at half speed.** The low drum sat on beats 1 and 3 only, so the strongest
  periodicity in the render was the *half bar*: the measured tempo came back at **66 BPM in a
  track written at 132**. That is the original complaint reproduced exactly, at twice the
  written speed.
- **And then the groove went flat.** Putting a stroke on every eighth at similar gains made the
  onset envelope uniform — the beat, the eighth and the half bar all autocorrelated within 12%
  of each other, which is not a groove but an undifferentiated stream. A hand drum is loud on
  one and quiet everywhere else, and **that difference is the pattern**. `DRUM_PATTERN` now
  spans 1.00 down to 0.22.

One measurement mistake of my own is worth recording next to them: the first tempo check took
the **argmax** of the onset autocorrelation and reported 66 BPM. In 4/4 the half bar always
correlates strongly, so argmax says nothing about whether the beat is there — the right question
is whether the one-beat lag is a local peak, which is what the check asks now.

## It is the third attempt

If driving battle music is also wrong, the honest next step is a reference track rather than a
fourth guess. What can be stated as measured rather than intended: 132 BPM written with a stroke
on every beat and a 4.5:1 accent range, an eighth-note bass ostinato, chord tones present in
every bar, four sections with the breakdown 36% quieter than the tune, no step discontinuity at
the loop point, and the fight layer louder than the calm one.
