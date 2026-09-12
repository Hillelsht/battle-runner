#!/usr/bin/env python3
"""Synthesise every sound in the game, from nothing.

    python3 tooling/synth_audio.py            # write Assets/Resources/Audio/*.wav
    python3 tooling/synth_audio.py --report   # sizes and durations, write nothing

WHY SYNTHESISED. No free audio host is reachable from the build container — Freesound,
OpenGameArt and kenney.nl all fail to connect, and the Kenney mirror that supplies the models
carries no audio. Synthesis is also the same move the rest of this project already makes: the
meshes, the sky, the road and every UI sprite are generated rather than imported, and for the
same reasons — nothing to license, nothing to import, deterministic, and reviewable as a diff.

WHAT IT CAN AND CANNOT DO. Drones, wind, impacts, bells and stingers synthesise well. Melody
does not. This is a dark-ambient bed and a set of readable impacts, not a score. Every clip is
loaded BY NAME through Resources, so any single file can be replaced with a real recording
later without touching a line of code.

DEPENDENCIES: numpy and the standard library. No ffmpeg, no sox, no scipy, no soundfile — none
of them are present here, and requiring them would mean this could not be re-run in CI.
Output is 22.05 kHz mono 16-bit WAV; Unity re-encodes to Vorbis for the APK, so the ~2 MB in
the repo is about 200 KB on the device.
"""
import argparse
import os
import struct
import sys
import urllib.request
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sf2                                                        # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "Resources", "Audio")
SR = 22050


# --- helpers ---------------------------------------------------------------

def t(seconds):
    return np.arange(int(seconds * SR)) / SR


def env(n, attack, decay, power=1.0):
    """Attack ramp into an exponential tail. `power` shapes the tail's knee."""
    x = np.arange(n) / SR
    rise = np.minimum(x / max(attack, 1e-4), 1.0)
    return rise * np.exp(-(x / max(decay, 1e-4)) ** power)


def lowpass(x, cutoff_start, cutoff_end=None):
    """
    One-pole low-pass with an optional sweep. Written as an explicit loop rather than an
    IIR call because scipy is not available, and at these clip lengths (under two seconds,
    except the beds) the cost does not matter.
    """
    n = len(x)
    end = cutoff_start if cutoff_end is None else cutoff_end
    # Convert a cutoff in Hz to a one-pole coefficient, per sample.
    hz = np.linspace(cutoff_start, end, n)
    a = np.clip(1.0 - np.exp(-2.0 * np.pi * hz / SR), 0.0, 1.0)
    out = np.empty(n)
    prev = 0.0
    for i in range(n):
        prev += a[i] * (x[i] - prev)
        out[i] = prev
    return out


def sweep(f0, f1, n, curve=1.0):
    """A phase-continuous frequency sweep. Integrating the frequency is what keeps it
    click-free; naively writing sin(2*pi*f(t)*t) does not, and the artefact is audible."""
    f = f0 + (f1 - f0) * (np.linspace(0, 1, n) ** curve)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def noise(n, seed):
    return np.random.default_rng(seed).normal(0.0, 1.0, n)


def normalise(x, peak=0.92):
    m = float(np.max(np.abs(x)))
    return x if m < 1e-9 else x * (peak / m)


def oneshot(x, peak=0.92):
    """
    Normalise AND ramp the last five milliseconds to silence.

    Every one-shot goes through this. A clip that stops while the waveform is away from zero
    is a step discontinuity, which is a click — measured on the first pass, shield_raise
    ended at 0.46 amplitude and would have clicked on every single block. The beds do NOT use
    this: they are cross-faded head-to-tail and a ramp would put a hole in the loop.
    """
    k = min(int(0.005 * SR), len(x))
    out = normalise(x, peak)
    out[len(out) - k:] *= np.linspace(1.0, 0.0, k)
    return out


def loop(x, crossfade=1.4):
    """
    Make a clip loop seamlessly by equal-power cross-fading its tail into its head.

    A bed that clicks once every twenty seconds is worse than no bed at all: the ear locks
    onto the period and then hears nothing else. Equal power rather than linear, because a
    linear cross-fade of two uncorrelated signals dips ~3 dB in the middle and the loop point
    becomes audible as a dropout instead of as a click.
    """
    k = int(crossfade * SR)
    if k * 2 >= len(x):
        return x
    fade = np.linspace(0.0, 1.0, k)
    head = np.sqrt(fade)
    tail = np.sqrt(1.0 - fade)
    out = x.copy()
    out[:k] = out[:k] * head + out[-k:] * tail
    return out[:-k]


def write(name, x, written):
    x = np.clip(x, -1.0, 1.0)
    pcm = (x * 32000).astype("<i2").tobytes()
    path = os.path.join(OUT, name + ".wav")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(pcm)
    written.append((name, len(x) / SR, os.path.getsize(path)))


# --- real instruments ------------------------------------------------------
#
# The beds were synthesised, and synthesis has a ceiling. Karplus-Strong is a plucked string and
# a summed sine stack is a pad, but neither of them is a cello, and told plainly that the result
# was still bad the honest answer is not a better oscillator — it is to stop synthesising.
#
# GeneralUser GS is a General MIDI bank whose licence permits commercial use without restriction
# and explicitly covers "musical recordings created with GeneralUser GS", which is exactly what a
# rendered bed is. It is a BUILD-TIME dependency: 32 MB downloaded into tooling/.cache (already
# git-ignored), never committed, never shipped. The device still sees two WAV files. Same move
# the meshes, the sky, the road surfaces and the UI sprites all make — generate offline, commit
# the output, ship no tool.

SOUNDFONT_URL = "https://raw.githubusercontent.com/mrbumpy409/GeneralUser-GS/main/GeneralUser-GS.sf2"
SOUNDFONT_PATH = os.path.join(REPO, "tooling", ".cache", "soundfont", "GeneralUser-GS.sf2")

# General MIDI programs, chosen for the reference rather than for coverage. The nylon guitar is
# the load-bearing one: the dark-fantasy town theme everyone actually means is a solo acoustic
# guitar, not an orchestra, and no amount of string pad substitutes for it.
GM_NYLON_GUITAR = 24
GM_CELLO = 42
GM_DOUBLE_BASS = 43
GM_HARP = 46
GM_TIMPANI = 47
GM_FAST_STRINGS = 48
GM_SLOW_STRINGS = 49
GM_CHOIR = 52

# MIDI note 38 is D2 — the tonic of everything in this file. Every pitch below is written as a
# number of semitones from it, so a chord reads as intervals rather than as note numbers.
TONIC_KEY = 38

# Rendered at 2x and decimated. Linear-interpolating a 44 kHz violin sample straight down to
# 22 kHz aliases its bow noise into audible grit; low-passing first and then dropping every
# other sample costs nothing offline and is the difference between strings and sizzle.
OVERSAMPLE = 2


def bank():
    """The SoundFont, downloaded and cached, or None if it cannot be had."""
    if getattr(bank, "_cached", "missing") != "missing":
        return bank._cached
    bank._cached = None
    try:
        if not os.path.exists(SOUNDFONT_PATH):
            os.makedirs(os.path.dirname(SOUNDFONT_PATH), exist_ok=True)
            print("fetching GeneralUser GS (32 MB, cached, not committed)...")
            with urllib.request.urlopen(SOUNDFONT_URL, timeout=120) as r:
                data = r.read()
            with open(SOUNDFONT_PATH, "wb") as f:
                f.write(data)
        bank._cached = sf2.load(SOUNDFONT_PATH)
    except Exception as exc:                                      # noqa: BLE001
        # NEVER fatal. The committed WAVs are the deliverable; this script must still run on a
        # machine with no network, and must say plainly that it fell back rather than quietly
        # shipping a worse bed that looks identical in a diff.
        print("WARNING: SoundFont unavailable (%s)." % exc)
        print("         Falling back to synthesis — the beds will be the OLD, worse ones.")
    return bank._cached


def play(sf, program, midi_key, seconds, gain=1.0, release=0.4):
    """One note at the oversampled rate. MIDI key 38 is D2, the tonic of everything here."""
    return sf.note(0, program, int(midi_key), seconds, SR * OVERSAMPLE,
                   gain=gain, release=release)


def decimate(x):
    """Anti-aliased 2:1 downsample. A four-tap half-band is plenty at this ratio."""
    kernel = np.array([0.25, 0.5, 0.25], dtype=np.float64)
    smoothed = np.convolve(x, kernel, mode="same")
    return smoothed[::OVERSAMPLE]


# --- an instrument, a room, and a loop that is musical ----------------------
#
# The bed this replaces was a drone stack, filtered noise and a slow swell. Played back it
# was correctly described as "just a noize, not a music, like an ocean sound" — and that is
# an accurate description of what it was, because it had no notes in it. It was AMBIENCE.
# What follows is a small instrument, a small room, and eight bars of harmony.

def pluck(freq, seconds, seed, damping=0.996, brightness=0.5, gain=1.0):
    """
    Karplus-Strong. A delay line of noise, low-passed a little on every lap.

    A physical string model in about ten lines, and precisely the plucked, decaying,
    slightly-detuned timbre of the dark-fantasy reference. Nothing else available here
    sounds like an instrument: an oscillator with an envelope sounds like a synthesiser,
    and the difference between those two is the whole difference between a score and a bed.

    THE LOOP IS OVER PERIODS, NOT SAMPLES, and it is exact rather than an approximation.
    y[i] = d * 0.5 * (y[i-L] + y[i-L+1]), so every sample in one period depends only on the
    previous period — except the LAST, whose second term wrapped into the period being
    written. Computing that one element separately makes the block form identical to the
    sample-by-sample recurrence, at a hundredth of the cost. A per-sample Python loop over
    the hundred-odd notes in a bed takes tens of seconds; this takes milliseconds.
    """
    length = max(2, int(round(SR / freq)))
    total = int(seconds * SR)
    rng = np.random.default_rng(seed)
    # Excite with LOW-PASSED noise rather than white. White noise gives a bright, buzzy
    # attack that reads as a synth pluck; a softer excitation reads as gut or wound string.
    period = rng.normal(0.0, 1.0, length)
    period = 0.5 * period + 0.5 * np.roll(period, 1)
    period *= np.hanning(length) * 0.5 + 0.5

    blocks = total // length + 1
    out = np.empty(blocks * length)
    for b in range(blocks):
        out[b * length:(b + 1) * length] = period
        nxt = damping * (brightness * period + (1.0 - brightness) * np.roll(period, -1))
        nxt[-1] = damping * (brightness * period[-1] + (1.0 - brightness) * nxt[0])
        period = nxt
    return out[:total] * gain


def comb(x, delay_ms, feedback):
    """A feedback comb, computed a delay-period at a time — exact, and not a Python loop."""
    d = max(1, int(delay_ms * SR / 1000.0))
    y = x.copy()
    for i in range(d, len(y), d):
        end = min(i + d, len(y))
        y[i:end] += feedback * y[i - d:i - d + (end - i)]
    return y


def allpass(x, delay_ms, gain=0.7):
    """Schroeder allpass: flat magnitude, scrambled phase. What turns combs into a room."""
    d = max(1, int(delay_ms * SR / 1000.0))
    y = np.empty(len(x))
    y[:d] = -gain * x[:d]
    for i in range(d, len(y), d):
        end = min(i + d, len(y))
        k = end - i
        y[i:end] = -gain * x[i:end] + x[i - d:i - d + k] + gain * y[i - d:i - d + k]
    return y


def reverb(x, wet=0.35, size=1.0):
    """
    A Schroeder reverb: four parallel combs into two series allpasses.

    The delays are mutually prime on purpose. Combs at related delays reinforce the same
    partials and the "room" comes out as a ringing pitch rather than as a space, which is
    the single most common way a hand-rolled reverb sounds wrong.
    """
    tail = np.zeros(len(x))
    for ms, fb in ((29.7, 0.78), (37.1, 0.75), (41.1, 0.73), (43.7, 0.71)):
        tail += comb(x, ms * size, fb)
    tail /= 4.0
    tail = allpass(allpass(tail, 5.0 * size, 0.7), 1.7 * size, 0.7)
    return x * (1.0 - wet) + tail * wet


def wrapping(freq, body_seconds):
    """
    The nearest frequency that completes a whole number of cycles in `body_seconds`.

    Anything sustained across the WHOLE take has to land back on its starting phase at the
    loop point, or the wrap is a step discontinuity — a click, once per loop, forever. The
    plucked notes and the drum hits are fine because they decay to nothing; the choir pad
    and its tremolo do not, and measured on the first pass the boss bed's seam was three
    times its own mean sample step. The shift needed is inaudible: 146.80 Hz becomes 146.79.
    """
    cycles = max(1.0, round(freq * body_seconds))
    return cycles / body_seconds


def wrap_tail(x, body_seconds):
    """
    Cut a rendered take back to its musical length by folding the OVERHANG onto the head.

    The cross-fade `loop()` does is right for ambience and wrong for music: it overlaps the
    last bar with the first, so two different chords sound at once for a second and a half,
    once per loop, forever. Here the take is rendered longer than the loop and the part that
    runs past the end — the decay of the final chord and the reverb tail — is ADDED back at
    the start, which is exactly where that sound belongs when the loop comes round again.
    Sample-accurate, no fade, and the harmony stays intact.
    """
    n = int(body_seconds * SR)
    if len(x) <= n:
        return x
    out = x[:n].copy()
    over = x[n:]
    k = min(len(over), n)
    out[:k] += over[:k]
    return out


# The mode. D natural minor: the Aeolian scale, which is the sound of the dark-fantasy
# reference and is NOT the harmonic minor a "dramatic" progression usually reaches for. The
# v chord stays MINOR in the ambient bed, so the harmony never resolves and the loop has no
# obvious seam in it. The boss bed raises that third to a major V for a real cadence.
def note(semitones_above_d2):
    return 73.416 * (2.0 ** (semitones_above_d2 / 12.0))       # D2 = 73.416 Hz


# Semitones from D, so a chord is readable as intervals rather than as frequencies:
#   up    D 0  Eb 1  E 2  F 3  F# 4  G 5  G# 6  A 7  Bb 8  B 9  C 10  C# 11
#   down  C# -1  C -2  B -3  Bb -4  A -5  G# -6  G -7  F# -8  F -9  E -10
#
# Written out because getting one of these wrong does not throw, does not sound obviously
# broken, and changes the MODE. The first pass had Gm as (-7, -3, 0, 5) — a B natural, which
# is G MAJOR — and both A chords as (-5, -1, ...), a C sharp, which is A major and therefore
# the harmonic-minor leading note the ambient bed is explicitly supposed not to use. A
# spectral check of the rendered file is what caught it: the Gm bar's strongest partial sat
# at 370.5 Hz, and 370.5 is F sharp, not the F natural a G minor chord is made of.
AMBIENT_CHORDS = [
    (0, 3, 7, 12),        # Dm  (i)     D  F  A  D
    (0, 3, 7, 12),
    (-4, 0, 3, 8),        # Bb  (VI)    Bb D  F  Bb
    (-4, 0, 3, 8),
    (-7, -4, 0, 5),       # Gm  (iv)    G  Bb D  G
    (-7, -4, 0, 5),
    (-5, -2, 2, 7),       # Am  (v)     A  C  E  A   — MINOR: Aeolian, not harmonic
    (-5, -2, 2, 7),
]

def room(x, amount=0.16):
    """
    A short send into the SAME Schroeder network the music uses.

    This is the single largest change to how the effects sound, and it is not an effect —
    it is a place. Fifteen dry one-shots played over a bed with a hall on it is a soundboard
    triggered next to a score; the same fifteen with a little of that hall on them is a game.
    Deliberately small, and deliberately NOT applied to the cues that fire several times a
    second: a tail on a sound the player hears six times in a second is mud, not depth.
    """
    if amount <= 0.0:
        return x
    return reverb(x, wet=amount, size=0.55)


def layer(n, *stages):
    """
    Sum separately-synthesised and separately-enveloped stages into one cue.

    ATTACK, BODY, TAIL. Every cue in the first pass was one waveform times one envelope,
    which is why they were fairly described as "too simple": a real impact has a transient
    that is almost all noise and lasts a few milliseconds, a body that carries the pitch,
    and a tail that carries the size. One envelope over all three makes them decay together,
    and decaying together is exactly what makes a sound read as synthetic.
    """
    out = np.zeros(n)
    for signal, gain in stages:
        k = min(n, len(signal))
        out[:k] += signal[:k] * gain
    return out


# --- the sounds ------------------------------------------------------------

def gate_add(seed=0):
    """
    A struck bell, in three stages. Blue gates ADD, and this is the sound heard most in a
    run, so it stays short and sits low in the mix — but "heard most" is also exactly why it
    had to stop being one sine stack under one envelope.

    The partials are the A major triad the music's own key contains (A, C sharp, E), so a
    gate chime landing over the bed is consonant with it rather than merely loud near it.
    """
    n = int(0.42 * SR)
    x = np.arange(n) / SR
    detune = 1.0 + (seed - 1) * 0.006       # variants sit a few cents apart

    # ATTACK: the hammer. Almost all noise, three milliseconds long, and it is what makes
    # the difference between a bell being struck and a bell simply existing.
    strike = lowpass(noise(n, 500 + seed), 7000, 2200) * env(n, 0.0005, 0.006)

    # BODY: the partials, each with its own decay — high ones die first, as they do on metal.
    body = np.zeros(n)
    for f, g, decay in ((880.0, 0.60, 0.16), (1108.7, 0.20, 0.12),
                        (1318.5, 0.26, 0.10), (2640.0, 0.10, 0.05)):
        body += np.sin(2 * np.pi * f * detune * x) * g * env(n, 0.002, decay)

    # TAIL: the hum the metal is left with, a couple of octaves down and much longer.
    tail = np.sin(2 * np.pi * 440.0 * detune * x) * env(n, 0.02, 0.30)

    return oneshot(room(layer(n, (strike, 0.55), (body, 1.0), (tail, 0.16)), 0.14), 0.85)


def gate_multiply():
    """
    A rising harp run. Multiplying is the best thing that happens in a run, so it goes UP, and
    it takes longer than an add so the difference is legible at a glance rather than by ear.
    """
    sf = bank()
    n = int(0.72 * SR)
    if sf is None:
        out = np.zeros(n)
        for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
            start = int(i * 0.052 * SR)
            m = n - start
            x = np.arange(m) / SR
            out[start:] += (np.sin(2 * np.pi * f * x) * 0.55
                            + np.sin(2 * np.pi * f * 2 * x) * 0.12) * env(m, 0.003, 0.20)
        return oneshot(room(out, 0.16), 0.90)

    rate = SR * OVERSAMPLE
    out = np.zeros(int(0.72 * rate) + rate, dtype=np.float64)
    for i, degree in enumerate([12, 15, 19, 24]):            # D minor, rising
        at = int(i * 0.048 * rate)
        v = play(sf, GM_HARP, TONIC_KEY + degree, 0.6, gain=0.85 - i * 0.06)
        out[at:at + len(v)] += v
    return oneshot(room(decimate(out)[:n], 0.18), 0.90)


def gate_subtract():
    """
    A pitch-swept thud with a dirty edge. Red gates take, and the sound has to land as loss
    inside the quarter-second the player has to react.

    Three stages with three different decays, which is the whole difference from the first
    pass: the grit is gone in 30 ms, the swept body in 110, and a filtered rumble carries on
    underneath for a third of a second. That last stage is what reads as WEIGHT.
    """
    n = int(0.38 * SR)
    grit = lowpass(noise(n, 11), 3200, 600) * env(n, 0.0008, 0.030)
    body = sweep(165, 52, n, curve=0.55) * env(n, 0.001, 0.11)
    rumble = lowpass(noise(n, 12), 190) * 5.0 * env(n, 0.01, 0.26)
    return oneshot(room(layer(n, (grit, 0.55), (body, 0.85), (rumble, 0.30)), 0.12), 0.88)


def enemy_bite(seed=0):
    """
    Short, dry, percussive. It fires per pack and must not accumulate into mush — which is
    also why this one gets NO room: a tail on a cue that can fire six times in a second is
    the definition of mud.

    Variation is in the noise seed and a small pitch offset, so a burst of bites is a burst
    of different bites rather than one sample played six times.
    """
    n = int(0.22 * SR)
    pitch = 1.0 + (seed - 1) * 0.05
    crack = lowpass(noise(n, 23 + seed * 7), 5200, 900) * 1.2 * env(n, 0.0008, 0.035)
    thump = sweep(240 * pitch, 80 * pitch, n, 0.5) * env(n, 0.001, 0.070)
    return oneshot(layer(n, (crack, 1.0), (thump, 0.5)), 0.82)


def spell_cast():
    """An upward whoosh into a snap: the wind-up the player feels in their thumb."""
    n = int(0.55 * SR)
    air = lowpass(noise(n, 31), 400, 6500) * 1.6
    tone = sweep(180, 900, n, 1.6) * 0.35
    return oneshot(room((air + tone) * env(n, 0.06, 0.18), 0.12), 0.86)


def spell_hit():
    """The detonation. Low body, bright transient, long-ish tail — the only run sound allowed
    to be genuinely loud, because it is the payoff for a cooldown."""
    n = int(0.95 * SR)
    body = sweep(320, 44, n, 0.42) * 0.9
    flash = lowpass(noise(n, 37), 9000, 1200) * 0.9 * env(n, 0.0005, 0.10)
    ring = np.sin(2 * np.pi * 138 * (np.arange(n) / SR)) * 0.35 * env(n, 0.01, 0.40)
    return oneshot(room(body * env(n, 0.001, 0.22) + flash + ring, 0.26), 0.95)


def shield_raise():
    """
    A rising hum that stops rather than fades, so the player knows exactly when they are
    covered. Two detuned partials give it the beating that reads as 'a field'.

    The release ramp is not cosmetic. Measured, the first version ended at 0.46 amplitude:
    a one-shot cut off mid-cycle is a step discontinuity, and every shield raise would have
    clicked. Five milliseconds is short enough to still read as a decisive stop.
    """
    n = int(0.5 * SR)
    x = np.arange(n) / SR
    hum = (np.sin(2 * np.pi * 196 * x) + np.sin(2 * np.pi * 197.6 * x)) * 0.5
    swell = np.minimum(x / 0.22, 1.0) ** 1.4
    out = hum * swell * np.exp(-(x / 0.6) ** 3)
    return oneshot(room(out, 0.10), 0.66)


def shield_block():
    """Metal. This is the moment a shield earns its cooldown, so it is the brightest transient
    in the game and one of the loudest."""
    n = int(0.55 * SR)
    x = np.arange(n) / SR
    partials = sum(np.sin(2 * np.pi * f * x) * a
                   for f, a in [(1240, 0.5), (1867, 0.3), (2740, 0.22), (4310, 0.12)])
    strike = lowpass(noise(n, 47), 12000, 3000) * 1.1 * env(n, 0.0004, 0.035)
    return oneshot(room(partials * env(n, 0.001, 0.13) + strike, 0.20), 0.95)


def boss_telegraph():
    """A rising drone under a heartbeat. It has to be readable as 'something is about to
    land' with the phone at arm's length and the music playing."""
    n = int(1.15 * SR)
    x = np.arange(n) / SR
    drone = sweep(70, 128, n, 1.3) * 0.6
    pulse = np.sin(2 * np.pi * 3.2 * x) * 0.5 + 0.5
    air = lowpass(noise(n, 53), 300, 1800) * 0.5
    return oneshot(room((drone * (0.55 + 0.45 * pulse) + air) * env(n, 0.10, 0.9, 2.0), 0.24), 0.88)


def boss_blow():
    """The hit. Deliberately the loudest cue in the table and given the highest priority in
    the voice pool: a blow lost because the crowd was passing gates is a fairness problem."""
    n = int(0.8 * SR)
    body = sweep(190, 38, n, 0.4) * 1.0
    crack = lowpass(noise(n, 61), 8000, 700) * 1.0 * env(n, 0.0004, 0.07)
    return oneshot(room(body * env(n, 0.001, 0.20) + crack, 0.28), 1.0)


def boss_hit():
    """The player's only audible confirmation that their army is doing damage. Small, dry,
    and pitched high enough to sit above the boss's own register."""
    n = int(0.2 * SR)
    return oneshot(room((lowpass(noise(n, 67), 7000, 2000) * 0.9
                      + sweep(600, 300, n, 0.6) * 0.4) * env(n, 0.0006, 0.05), 0.10), 0.78)


def boss_death():
    """Three waves, matching the three-wave death beat the VFX already plays. The audio and
    the shockwaves are the same event; they should not describe different shapes."""
    n = int(2.4 * SR)
    out = np.zeros(n)
    for i, (delay, f0, gain) in enumerate([(0.0, 260, 1.0), (0.22, 180, 0.75), (0.5, 120, 0.55)]):
        start = int(delay * SR)
        m = n - start
        out[start:] += sweep(f0, 30, m, 0.35) * gain * env(m, 0.002, 0.55)
    rubble = lowpass(noise(n, 71), 3000, 200) * 0.7 * env(n, 0.02, 0.9)
    return oneshot(room(out + rubble, 0.34), 1.0)


def loot_reveal():
    """
    A harp arpeggio up a D major triad — the one unambiguously GOOD sound in the game, and
    the one that most obviously wanted a real instrument. Major against a game in D minor,
    deliberately: loot is the moment the world stops being grim at you.
    """
    sf = bank()
    n = int(1.5 * SR)
    if sf is None:
        x = t(1.5)
        chord = sum(np.sin(2 * np.pi * f * x) * g
                    for f, g in [(523.25, 0.5), (659.25, 0.4), (783.99, 0.3), (1046.5, 0.2)])
        return oneshot(room(chord * env(n, 0.01, 0.55), 0.26), 0.90)

    rate = SR * OVERSAMPLE
    out = np.zeros(int(1.5 * rate) + rate, dtype=np.float64)
    for i, degree in enumerate([12, 16, 19, 24, 28]):        # D major, rising, up two octaves
        at = int(i * 0.055 * rate)
        v = play(sf, GM_HARP, TONIC_KEY + degree, 1.3, gain=0.9 - i * 0.09)
        out[at:at + len(v)] += v
    return oneshot(room(decimate(out)[:n], 0.24), 0.90)


def ui_tap():
    """A soft wooden click. Quiet on purpose — it fires on every button in the game."""
    n = int(0.11 * SR)
    return oneshot((sweep(900, 380, n, 0.5) * 0.6
                      + lowpass(noise(n, 79), 4000, 1200) * 0.5) * env(n, 0.0005, 0.028), 0.60)


def round_start():
    """
    A timpani stroke under a low string swell. The only moment the player is TOLD, rather
    than shown, that somewhere new has begun, so it is the one cue allowed to be a fanfare.
    """
    sf = bank()
    n = int(1.6 * SR)
    if sf is None:
        x = t(1.6)
        horn = sum(np.sin(2 * np.pi * f * x) * a
                   for f, a in [(87.31, 0.55), (130.81, 0.35), (174.61, 0.22), (261.63, 0.10)])
        return oneshot(room(horn * env(n, 0.09, 0.7, 2.2), 0.30), 0.88)

    rate = SR * OVERSAMPLE
    out = np.zeros(int(1.6 * rate) + rate, dtype=np.float64)
    for program, degree, gain, delay in [(GM_TIMPANI, 0, 0.75, 0.00),
                                         (GM_SLOW_STRINGS, 12, 0.55, 0.02),
                                         (GM_SLOW_STRINGS, 19, 0.34, 0.04),
                                         (GM_CHOIR, 24, 0.20, 0.06)]:
        at = int(delay * rate)
        v = play(sf, program, TONIC_KEY + degree, 1.4, gain=gain)
        out[at:at + len(v)] += v
    return oneshot(room(decimate(out)[:n], 0.30), 0.88)


# --- the driving bed --------------------------------------------------------
#
# THE THIRD ATTEMPT, AND THE GENRE WAS THE PROBLEM RATHER THAN THE EXECUTION.
#
# The first bed was a drone stack and filtered noise: "just a noize, not a music, like an
# ocean sound", which was accurate because it had no notes in it. The second was the eight
# bars of slow D-minor harmony below — real instruments, real chords, correctly voiced — and
# the report was still "music is bad".
#
# It is not badly played. It is the WRONG MUSIC. It is a twenty-four-second ambient loop at
# roughly forty beats a minute, playing under a forty-second sprint in which the player makes
# a lane decision every two seconds. A score that slow does not fail to support that; it
# actively works against it, because the tempo the ear is given is the tempo the hands expect.
#
# So: a frame-drum and low-string ostinato at 132 BPM under a modal melody, eighty-seven
# seconds long in four sections so the loop is not a four-bar figure repeated. Same key, same
# room, same soundfont — only the pulse and the arrangement change.

# 132 BPM in 4/4. Fast enough to drive, slow enough that the ear can still hear the harmony
# move; at 150+ the bass ostinato turns into a texture and stops being a line.
BEAT = 60.0 / 132.0
BAR = BEAT * 4.0


def frame_drum(freq, seconds, seed, hit=1.0, rate=None):
    """
    A hand-struck frame drum: a taut skin over a shallow body.

    SYNTHESISED RATHER THAN SAMPLED, and for a reason worth recording. GeneralUser-GS has a
    standard GM drum kit on bank 128, and this project's minimal SoundFont reader resolves
    exactly ONE of its keys — every other key falls outside the zone it picks, and returns
    silence. Probed: of thirty-five kit keys, only the crash cymbal sounds.

    Fighting the reader for a kick drum is the wrong trade when a membrane is four lines of
    arithmetic: a noise burst through a resonant band for the skin, a pitched thump an octave
    below for the body, and a fast exponential on both. It also means the drum can be tuned to
    the key, which a sampled kit cannot be.
    """
    rate = rate or (SR * OVERSAMPLE)
    n = int(seconds * rate)
    if n <= 0:
        return np.zeros(0)
    x = np.arange(n) / rate

    # The skin: filtered noise, decaying fast. The bandwidth is what separates a drum from
    # a click — too narrow and it is a pitched bloop, too wide and it is a handclap.
    rng = np.random.default_rng(seed)
    skin = rng.normal(0.0, 1.0, n)
    skin = skin * np.exp(-x * 46.0)
    # A crude two-pole resonance at the skin frequency, run forward in place.
    out = np.zeros(n)
    b = 2.0 * np.cos(2.0 * np.pi * freq * 2.1 / rate) * 0.986
    c = -0.986 * 0.986
    y1 = y2 = 0.0
    for i in range(n):
        y = skin[i] + b * y1 + c * y2
        y2, y1 = y1, y
        out[i] = y
    out *= 0.06

    # The body: a short pitched thump that falls a little, which is what a hand on a skin
    # actually does as the tension releases.
    body = np.sin(2.0 * np.pi * np.cumsum(freq * (1.0 + 0.35 * np.exp(-x * 24.0))) / rate)
    body *= np.exp(-x * 13.0)

    return (out * 0.55 + body * 0.9) * hit


# The run's progression. D aeolian, four bars, i - VII - VI - VII: the modal loop that
# actually drives, because VII (C major) is a whole tone BELOW the tonic and pulls back up to
# it without the leading-note pull of a harmonic-minor V. That pull is what the ambient bed
# saves for the boss, and a run should not sound like it is resolving every four bars.
#
# Written as intervals from D for the same reason as AMBIENT_CHORDS: a wrong semitone here
# does not throw and does not sound broken, it changes the mode. Checked by FFT of the render.
#   up   D 0  Eb 1  E 2  F 3  F# 4  G 5  G# 6  A 7  Bb 8  B 9  C 10
#   down C -2  Bb -4  A -5  G -7
DRIVE_CHORDS = [
    (0, 3, 7),        # Dm   (i)     D  F  A
    (-2, 2, 5),       # C    (VII)   C  E  G
    (-4, 0, 3),       # Bb   (VI)    Bb D  F
    (-2, 2, 5),       # C    (VII)   C  E  G
]

# The melody, as scale degrees above D in the natural minor, with None for a rest. Four bars
# of eighths against the four chords above, so it lands on a chord tone at every bar line.
# D aeolian degrees: 0 D, 2 E, 3 F, 5 G, 7 A, 8 Bb, 10 C, 12 D
DRIVE_MELODY = [
    12, None, 10, 12, None, 8, 7, None,
    10, None, 8, 10, None, 7, 5, None,
    8, None, 7, 8, 10, None, 7, 5,
    3, 5, 7, None, 10, None, 12, None,
]


# The frame-drum pattern, as (beat, gain). Dum on one, a lighter dum on three, teks between.
# THE DYNAMIC RANGE IS THE PATTERN: measured on a first render whose strokes were all within
# 0.5-0.95 of each other, the beat, the eighth and the half bar all autocorrelated within 12%
# and there was no pulse to hear at any of them.
DRUM_PATTERN = (
    (0.00, 1.00),     # dum
    (1.00, 0.34),     # tek
    (1.50, 0.22),
    (2.00, 0.72),     # dum
    (3.00, 0.40),     # tek
    (3.50, 0.26),
)


def driving_bed():
    """
    Eighty-seven seconds of D-aeolian battle music at 132 BPM.

    FOUR SECTIONS, not one loop played twelve times. A four-bar figure repeated for a minute
    and a half is the thing the ear times and then stops hearing, which is the same failure
    the twenty-four-second ambient loop had at a different speed:

        bars  0-7   the engine starts   drums and bass only, no melody
        bars  8-23  the tune            melody on strings over the full kit
        bars 24-31  the breakdown       drums thin out, guitar carries it alone
        bars 32-47  the return          melody an octave up, everything in

    ONE FILE STILL SERVES ALL EIGHT WORLDS, re-pitched and low-passed per world at runtime
    from MusicMood — the same "one asset, themed" move the sky, the road and the scenery make.
    """
    sf = bank()
    if sf is None:
        return _synth_ambient_bed()

    bars = 48
    body = BAR * bars
    overhang = 3.0
    rate = SR * OVERSAMPLE
    n = int((body + overhang) * rate)
    dry = np.zeros(n, dtype=np.float64)

    def place(signal, at_seconds, gain=1.0):
        at = int(at_seconds * rate)
        if at >= n or len(signal) == 0:
            return
        k = min(len(signal), n - at)
        dry[at:at + k] += signal[:k] * gain

    # Two frame drums, tuned a fifth apart: a low one on the downbeats and a high one on the
    # off-beats. Rendered ONCE each and placed repeatedly, because synthesising a resonator
    # per hit for four hundred hits is a minute of Python for no audible difference.
    low_drum = frame_drum(note(0) * 0.5, 0.55, seed=11)
    high_drum = frame_drum(note(7) * 0.5, 0.34, seed=12, hit=0.62)
    ghost = frame_drum(note(7) * 0.5, 0.22, seed=13, hit=0.26)

    for bar in range(bars):
        at = bar * BAR
        chord = DRIVE_CHORDS[bar % 4]
        section_full = bar >= 8 and not (24 <= bar < 32)
        breakdown = 24 <= bar < 32

        # THE PULSE. A frame-drum pattern with the off-beat displaced: the low drum on 1 and
        # 3, the high on the "and" of 2 and on 4, plus ghost strokes on the sixteenths that
        # are what makes a hand drum sound played rather than sequenced.
        # SOMETHING ON EVERY BEAT. The first version put the low drum on 1 and 3 and the high
        # on the off-beats, and measured on the render the strongest periodicity was the HALF
        # BAR: the tempo came back as 66 BPM in a track written at 132. That is the original
        # complaint reproduced exactly, at twice the written speed. The pulse the ear takes
        # is the one that repeats, so every beat now carries a stroke and the accents make
        # the pattern rather than the gaps.
        if not breakdown or bar >= 28:
            for beat, gain in DRUM_PATTERN:
                place(low_drum if beat in (0.0, 2.0) else high_drum, at + BEAT * beat, gain)
        if section_full:
            # Two ghost strokes only. The first version had six, and measured on the render
            # the onset envelope came out FLAT: the one-beat lag autocorrelated at 0.161
            # against 0.153 for the eighth and 0.142 for the half bar, which is not a groove,
            # it is an undifferentiated stream of eighth notes. A hand drum is loud on one and
            # quiet everywhere else; that difference IS the pattern.
            place(ghost, at + BEAT * 2.5, 0.9)
            place(ghost, at + BEAT * 3.75, 1.1)
            # A crash on the section boundaries. Key 49 is the ONE percussion key this
            # project's SoundFont reader resolves out of the whole GM kit, which is why the
            # rest of the drums are synthesised — see frame_drum.
            if bar in (8, 32):
                place(sf.note(128, 0, 49, 2.4, rate, gain=0.30, release=0.6), at)

        # THE BASS OSTINATO. Driving eighths on the root with a walk up to the next chord on
        # the last beat -- the figure that makes this a battle rather than a chord chart.
        root = TONIC_KEY + chord[0]
        for i, beat in enumerate((0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5)):
            degree = chord[0]
            if i == 7:
                nxt = DRIVE_CHORDS[(bar + 1) % 4][0]
                degree = chord[0] + (1 if nxt > chord[0] else -1) * 2
            place(play(sf, GM_DOUBLE_BASS, TONIC_KEY + degree, BEAT * 0.62,
                       gain=0.30 if i % 2 == 0 else 0.20, release=0.10),
                  at + BEAT * beat)

        # THE CHORD, not just its root. The first render played `chord[0]` here and nowhere
        # else, so DRIVE_CHORDS' thirds were never sounded at all: an FFT of bar 0 heard a
        # bare D-A fifth, which is neither major nor minor, and bar 3 had no E in it. The
        # progression existed only in the comments. Cello on all three voices, crossing the
        # bar so the harmony never gaps.
        for voice, degree in enumerate(chord):
            place(play(sf, GM_CELLO, TONIC_KEY + degree + 12, BAR * 1.05,
                       gain=0.17 - voice * 0.03, release=0.55), at)

        if section_full:
            # THE MELODY. Fast strings, which is the section that plays a tune in this genre,
            # doubled an octave up in the final section so the return is a lift and not a
            # repeat.
            octave = 12 if bar >= 32 else 0
            for step in range(8):
                degree = DRIVE_MELODY[((bar % 4) * 8 + step) % len(DRIVE_MELODY)]
                if degree is None:
                    continue
                place(play(sf, GM_FAST_STRINGS, TONIC_KEY + degree + 12 + octave,
                           BEAT * 0.55, gain=0.26, release=0.16),
                      at + BEAT * step * 0.5)
            # Strings underneath the tune, one note a bar, so the melody has something to
            # sit on that is not the bass.
            place(play(sf, GM_SLOW_STRINGS, root + 19, BAR * 1.1, gain=0.075, release=0.9), at)

        if breakdown:
            # THE BREAKDOWN. The guitar alone with the chord arpeggiated, which is what makes
            # the return at bar 32 land -- eighty-seven seconds of full arrangement is a wall.
            for voice, degree in enumerate(chord):
                place(play(sf, GM_NYLON_GUITAR, TONIC_KEY + degree + 24, BEAT * 2.2,
                           gain=0.40 - voice * 0.06),
                      at + BEAT * voice * 0.75)
            place(play(sf, GM_CHOIR, root + 24, BAR * 1.1, gain=0.05, release=1.0), at)

    # Less room than the ambient bed: a long reverb on a 132 BPM ostinato smears the pulse
    # into mud, and the pulse is the entire point of this arrangement.
    wet = reverb(dry, wet=0.22, size=0.85)
    return normalise(wrap_tail(decimate(wet), body), 0.86)


def ambient_bed():
    """
    Twenty-four seconds in D natural minor, played on real instruments.

    THE ARRANGEMENT IS THE POINT, AND SO IS THE INSTRUMENT. The dark-fantasy town theme
    everyone actually means is a SOLO ACOUSTIC GUITAR over a held low string — not an
    orchestra, and not a synthesiser pretending to be one. So: a nylon guitar carries the
    arpeggio, a cello holds the root, slow strings sit underneath, and a choir is barely
    there. Timpani marks the bar and is felt rather than heard.

    Eight bars of i-i-VI-VI-iv-iv-v-v at three seconds each. Slow enough to read as
    atmosphere rather than as a tune the player tires of on the fortieth round; long enough
    that the loop point is not something the ear can time.

    ONE FILE STILL SERVES ALL EIGHT WORLDS, re-pitched and low-passed per world at runtime
    from MusicMood — the same "one asset, themed" move the sky, the road and the scenery make.
    """
    sf = bank()
    if sf is None:
        return _synth_ambient_bed()

    bar = 3.0
    body = bar * len(AMBIENT_CHORDS)
    overhang = 5.0                       # the last chord's decay and the reverb tail
    rate = SR * OVERSAMPLE
    n = int((body + overhang) * rate)
    dry = np.zeros(n, dtype=np.float64)

    def place(signal, at_seconds, gain=1.0):
        at = int(at_seconds * rate)
        if at >= n:
            return
        k = min(len(signal), n - at)
        dry[at:at + k] += signal[:k] * gain

    for index, chord in enumerate(AMBIENT_CHORDS):
        at = index * bar

        # The guitar, arpeggiated across the bar and left to ring into the next chord. A block
        # chord on the downbeat is a hymn; this is a lute in an empty hall.
        for voice, degree in enumerate(chord):
            offset = 0.10 + voice * 0.42
            # THE GUITAR IS THE MELODY AND MUST SIT ON TOP. Measured on the first render, the
            # bed put 43% of its energy under 160 Hz and 8% in the guitar's own register — the
            # cello and the timpani were burying the only line that actually moves. Loud, and
            # an octave higher than the cello's root so the two are not competing for the same
            # place in the mix.
            place(play(sf, GM_NYLON_GUITAR, TONIC_KEY + degree + 24, bar * 1.7,
                       gain=1.15 - voice * 0.12),
                  at + offset)

        # The cello holds the root, crossing the bar line so the harmony never gaps.
        place(play(sf, GM_CELLO, TONIC_KEY + chord[0], bar * 1.15, gain=0.22, release=0.9), at)

        # Slow strings a fifth up, quieter still — this is the thing you notice only when it
        # stops, which is what a pad is for.
        place(play(sf, GM_SLOW_STRINGS, TONIC_KEY + chord[0] + 19, bar * 1.2,
                   gain=0.10, release=1.1), at + 0.25)

        # The choir is almost subliminal. Above about this level it stops being a room and
        # starts being a choir, and a choir every three seconds for forty rounds is a lot.
        place(play(sf, GM_CHOIR, TONIC_KEY + chord[0] + 24, bar * 1.1,
                   gain=0.055, release=1.2), at + 0.5)

        # Timpani on the bar and, softer, on the half. Felt rather than heard: it is what
        # stops twenty-four seconds of slow harmony from floating away entirely.
        place(play(sf, GM_TIMPANI, TONIC_KEY + chord[0], 1.2, gain=0.085), at)
        place(play(sf, GM_TIMPANI, TONIC_KEY + chord[0], 0.9, gain=0.038), at + bar * 0.5)

    wet = reverb(dry, wet=0.40, size=1.30)
    return normalise(wrap_tail(decimate(wet), body), 0.82)


def boss_bed():
    """
    The fight layer: same length, key and room, so the two cross-fade at any point without a
    key change — which would be more distracting than the fight itself.

    What changes is the harmony, the instrument and the pulse. The guitar gives way to a
    string section, the progression tightens to i-VI-iv-V, and that V is MAJOR: the raised
    third against a D minor tonic is the one interval in this key that genuinely wants to
    resolve, and it is why the ambient bed above deliberately never uses it.
    """
    sf = bank()
    if sf is None:
        return _synth_boss_bed()

    bar = 3.0
    chords = [
        (0, 3, 7, 12),    # Dm
        (-4, 0, 3, 8),    # Bb
        (-7, -4, 0, 5),   # Gm
        (-5, -1, 2, 7),   # A MAJOR — the C sharp is the whole point
    ]
    body = bar * len(chords) * 2
    overhang = 4.0
    rate = SR * OVERSAMPLE
    n = int((body + overhang) * rate)
    dry = np.zeros(n, dtype=np.float64)

    def place(signal, at_seconds, gain=1.0):
        at = int(at_seconds * rate)
        if at >= n:
            return
        k = min(len(signal), n - at)
        dry[at:at + k] += signal[:k] * gain

    for cycle in range(2):
        for index, chord in enumerate(chords):
            at = (cycle * len(chords) + index) * bar

            # Fast strings, all four voices together — a section attacking a chord, not an
            # arpeggio. The fight is not contemplative.
            for voice, degree in enumerate(chord):
                place(play(sf, GM_FAST_STRINGS, TONIC_KEY + degree + 24, bar * 1.1,
                           gain=0.52 - voice * 0.04, release=0.5), at + 0.02 * voice)

            place(play(sf, GM_DOUBLE_BASS, TONIC_KEY + chord[0], bar * 1.1,
                       gain=0.34, release=0.7), at)
            place(play(sf, GM_CHOIR, TONIC_KEY + chord[0] + 24, bar * 1.05,
                       gain=0.20, release=0.8), at)

    # Timpani at 80 bpm — four to the three-second bar. Fast enough to press, slow enough not
    # to turn a boss fight into a dance track.
    beat = 0.75
    for k in range(int(body / beat) + 5):
        strong = (k % 4 == 0)
        place(play(sf, GM_TIMPANI, TONIC_KEY + (0 if strong else -5), 0.8,
                   gain=0.16 if strong else 0.07), k * beat)

    wet = reverb(dry, wet=0.32, size=1.10)
    return normalise(wrap_tail(decimate(wet), body), 0.88)


# --- the synthesised beds, kept as the fallback ----------------------------
#
# These are what shipped before the SoundFont, and they are kept for exactly one reason: this
# script must still run on a machine that cannot reach the network. They are audibly worse and
# the fallback says so out loud rather than producing a quietly inferior file.

def _synth_ambient_bed():
    bar = 3.0
    body = bar * len(AMBIENT_CHORDS)
    n = int((body + 4.0) * SR)
    x = np.arange(n) / SR
    dry = np.zeros(n)
    for index, chord in enumerate(AMBIENT_CHORDS):
        at = int(index * bar * SR)
        for voice, degree in enumerate(chord):
            offset = at + int((0.12 + voice * 0.46) * SR)
            if offset >= n:
                continue
            v = pluck(note(degree + 12), min(bar * 1.8, (n - offset) / SR),
                      4000 + index * 8 + voice, damping=0.9965,
                      brightness=0.46 + voice * 0.03, gain=0.30 - voice * 0.035)
            dry[offset:offset + len(v)] += v
        root = note(chord[0])
        span = min(int(bar * 1.15 * SR), n - at)
        seg = np.arange(span) / SR
        swell = np.minimum(seg / 0.9, 1.0) * np.exp(-seg / (bar * 1.6))
        dry[at:at + span] += (np.sin(2 * np.pi * root * seg) * 0.22
                              + np.sin(2 * np.pi * root * 2.003 * seg) * 0.09
                              + np.sin(2 * np.pi * root * 3.0 * seg) * 0.035) * swell
    return normalise(wrap_tail(reverb(dry, wet=0.42, size=1.25), body), 0.80)


def _synth_boss_bed():
    bar = 3.0
    chords = [(0, 3, 7, 12), (-4, 0, 3, 8), (-7, -4, 0, 5), (-5, -1, 2, 7)]
    body = bar * len(chords) * 2
    n = int((body + 3.0) * SR)
    x = np.arange(n) / SR
    dry = np.zeros(n)
    for cycle in range(2):
        for index, chord in enumerate(chords):
            at = int((cycle * len(chords) + index) * bar * SR)
            for voice, degree in enumerate(chord):
                offset = at + int((0.05 + voice * 0.30) * SR)
                if offset >= n:
                    continue
                v = pluck(note(degree + 12), min(bar * 1.4, (n - offset) / SR),
                          7000 + cycle * 64 + index * 8 + voice,
                          damping=0.9955, brightness=0.52, gain=0.26 - voice * 0.03)
                dry[offset:offset + len(v)] += v
            root = note(chord[0])
            span = min(int(bar * 1.1 * SR), n - at)
            seg = np.arange(span) / SR
            dry[at:at + span] += (np.sin(2 * np.pi * root * seg) * 0.26
                                  + np.sin(2 * np.pi * root * 1.5 * seg) * 0.10) \
                                 * np.minimum(seg / 0.35, 1.0) * np.exp(-seg / (bar * 1.3))
    beat = 0.75
    for k in range(int(body / beat) + 4):
        at = int(k * beat * SR)
        if at >= n:
            continue
        span = min(int(0.45 * SR), n - at)
        seg = np.arange(span) / SR
        strong = (k % 4 == 0)
        dry[at:at + span] += sweep(126 if strong else 104, 44, span, 0.35) \
                             * np.exp(-seg * 15.0) * (0.42 if strong else 0.22)
    tremolo = 0.6 + 0.4 * np.sin(2 * np.pi * wrapping(0.28, body) * x)
    for f, g in ((note(12), 0.055), (note(19), 0.040)):
        dry += np.sin(2 * np.pi * wrapping(f, body) * x) * g * tremolo
    return normalise(wrap_tail(reverb(dry, wet=0.34, size=1.10), body), 0.86)


SOUNDS = [
    ("sfx_gate_add", gate_add),
    ("sfx_gate_add2", lambda: gate_add(1)),
    ("sfx_gate_add3", lambda: gate_add(2)),
    ("sfx_enemy_bite2", lambda: enemy_bite(1)),
    ("sfx_enemy_bite3", lambda: enemy_bite(2)),
    ("sfx_gate_multiply", gate_multiply),
    ("sfx_gate_subtract", gate_subtract),
    ("sfx_enemy_bite", enemy_bite),
    ("sfx_spell_cast", spell_cast),
    ("sfx_spell_hit", spell_hit),
    ("sfx_shield_raise", shield_raise),
    ("sfx_shield_block", shield_block),
    ("sfx_boss_telegraph", boss_telegraph),
    ("sfx_boss_blow", boss_blow),
    ("sfx_boss_hit", boss_hit),
    ("sfx_boss_death", boss_death),
    ("sfx_loot_reveal", loot_reveal),
    ("sfx_ui_tap", ui_tap),
    ("sfx_round_start", round_start),
    ("mus_bed", driving_bed),
    ("mus_boss", boss_bed),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", action="store_true", help="measure only, write nothing")
    args = ap.parse_args()

    written = []
    total = 0
    for name, make in SOUNDS:
        samples = make()
        if args.report:
            written.append((name, len(samples) / SR, len(samples) * 2 + 44))
        else:
            write(name, samples, written)
    for name, seconds, size in written:
        total += size
        print(f"  {name:22s} {seconds:5.2f}s  {size / 1024:7.1f} KB")
    print(f"{len(written)} clips, {total / 1024 / 1024:.2f} MB "
          f"({'measured only' if args.report else os.path.relpath(OUT, REPO)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
