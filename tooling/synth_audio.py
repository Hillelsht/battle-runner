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
import wave

import numpy as np

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


# --- the sounds ------------------------------------------------------------

def gate_add():
    """A bell. Blue gates ADD, and this is the sound the player hears most in a run, so it is
    short, bright and low enough in the mix to survive being heard four hundred times."""
    x = t(0.42)
    n = len(x)
    tone = (np.sin(2 * np.pi * 880 * x) * 0.60
            + np.sin(2 * np.pi * 1318.5 * x) * 0.28      # a fifth above
            + np.sin(2 * np.pi * 2640 * x) * 0.10)       # the shimmer
    return oneshot(tone * env(n, 0.002, 0.14), 0.85)


def gate_multiply():
    """A rising arpeggio. Multiplying is the best thing that happens in a run; it should go
    UP, and it should take longer than an add so the difference is legible at a glance."""
    n = int(0.72 * SR)
    out = np.zeros(n)
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]):
        start = int(i * 0.052 * SR)
        m = n - start
        x = np.arange(m) / SR
        out[start:] += (np.sin(2 * np.pi * f * x) * 0.55
                        + np.sin(2 * np.pi * f * 2 * x) * 0.12) * env(m, 0.003, 0.20)
    return oneshot(out, 0.90)


def gate_subtract():
    """A pitch-swept thud with a dirty edge. Red gates take, and the sound has to land as
    loss inside the quarter-second the player has to react."""
    n = int(0.38 * SR)
    body = sweep(165, 52, n, curve=0.55) * 0.85
    grit = lowpass(noise(n, 11), 2600, 500) * 0.55
    return oneshot((body + grit) * env(n, 0.001, 0.11), 0.88)


def enemy_bite():
    """Short, dry, percussive. It fires per pack and must not accumulate into mush."""
    n = int(0.22 * SR)
    crack = lowpass(noise(n, 23), 5200, 900) * 1.2
    thump = sweep(240, 80, n, 0.5) * 0.5
    return oneshot((crack + thump) * env(n, 0.0008, 0.055), 0.82)


def spell_cast():
    """An upward whoosh into a snap: the wind-up the player feels in their thumb."""
    n = int(0.55 * SR)
    air = lowpass(noise(n, 31), 400, 6500) * 1.6
    tone = sweep(180, 900, n, 1.6) * 0.35
    return oneshot((air + tone) * env(n, 0.06, 0.18), 0.86)


def spell_hit():
    """The detonation. Low body, bright transient, long-ish tail — the only run sound allowed
    to be genuinely loud, because it is the payoff for a cooldown."""
    n = int(0.95 * SR)
    body = sweep(320, 44, n, 0.42) * 0.9
    flash = lowpass(noise(n, 37), 9000, 1200) * 0.9 * env(n, 0.0005, 0.10)
    ring = np.sin(2 * np.pi * 138 * (np.arange(n) / SR)) * 0.35 * env(n, 0.01, 0.40)
    return oneshot(body * env(n, 0.001, 0.22) + flash + ring, 0.95)


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
    return oneshot(out, 0.66)


def shield_block():
    """Metal. This is the moment a shield earns its cooldown, so it is the brightest transient
    in the game and one of the loudest."""
    n = int(0.55 * SR)
    x = np.arange(n) / SR
    partials = sum(np.sin(2 * np.pi * f * x) * a
                   for f, a in [(1240, 0.5), (1867, 0.3), (2740, 0.22), (4310, 0.12)])
    strike = lowpass(noise(n, 47), 12000, 3000) * 1.1 * env(n, 0.0004, 0.035)
    return oneshot(partials * env(n, 0.001, 0.13) + strike, 0.95)


def boss_telegraph():
    """A rising drone under a heartbeat. It has to be readable as 'something is about to
    land' with the phone at arm's length and the music playing."""
    n = int(1.15 * SR)
    x = np.arange(n) / SR
    drone = sweep(70, 128, n, 1.3) * 0.6
    pulse = np.sin(2 * np.pi * 3.2 * x) * 0.5 + 0.5
    air = lowpass(noise(n, 53), 300, 1800) * 0.5
    return oneshot((drone * (0.55 + 0.45 * pulse) + air) * env(n, 0.10, 0.9, 2.0), 0.88)


def boss_blow():
    """The hit. Deliberately the loudest cue in the table and given the highest priority in
    the voice pool: a blow lost because the crowd was passing gates is a fairness problem."""
    n = int(0.8 * SR)
    body = sweep(190, 38, n, 0.4) * 1.0
    crack = lowpass(noise(n, 61), 8000, 700) * 1.0 * env(n, 0.0004, 0.07)
    return oneshot(body * env(n, 0.001, 0.20) + crack, 1.0)


def boss_hit():
    """The player's only audible confirmation that their army is doing damage. Small, dry,
    and pitched high enough to sit above the boss's own register."""
    n = int(0.2 * SR)
    return oneshot((lowpass(noise(n, 67), 7000, 2000) * 0.9
                      + sweep(600, 300, n, 0.6) * 0.4) * env(n, 0.0006, 0.05), 0.78)


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
    return oneshot(out + rubble, 1.0)


def loot_reveal():
    """A bright major third with a long tail. The one unambiguously GOOD sound in the game."""
    n = int(1.5 * SR)
    x = np.arange(n) / SR
    chord = sum(np.sin(2 * np.pi * f * x) * a
                for f, a in [(659.25, 0.45), (830.6, 0.32), (987.77, 0.26), (1318.5, 0.16)])
    shimmer = np.sin(2 * np.pi * 2637 * x) * 0.08 * env(n, 0.05, 0.5)
    return oneshot(chord * env(n, 0.01, 0.55) + shimmer, 0.85)


def ui_tap():
    """A soft wooden click. Quiet on purpose — it fires on every button in the game."""
    n = int(0.11 * SR)
    return oneshot((sweep(900, 380, n, 0.5) * 0.6
                      + lowpass(noise(n, 79), 4000, 1200) * 0.5) * env(n, 0.0005, 0.028), 0.60)


def round_start():
    """A low horn. It plays once as a round loads, which is the only moment the player is
    told, rather than shown, that somewhere new has begun."""
    n = int(1.6 * SR)
    x = np.arange(n) / SR
    horn = sum(np.sin(2 * np.pi * f * x + np.sin(2 * np.pi * 5.5 * x) * 0.08) * a
               for f, a in [(87.31, 0.55), (130.81, 0.35), (174.61, 0.22), (261.63, 0.10)])
    return oneshot(horn * env(n, 0.09, 0.7, 2.2), 0.88)


def ambient_bed():
    """
    Twenty seconds of dark ambience: a detuned drone stack, filtered-noise wind, and a slow
    swell, cross-faded head-to-tail so it loops without a seam.

    ONE FILE SERVES ALL EIGHT WORLDS. It is re-pitched and low-passed per world at runtime
    from MusicMood, which is the same 'one asset, themed' move the sky and the road already
    make. Eight beds would be more bytes than the entire rest of the project.
    """
    duration = 20.0
    x = t(duration)
    n = len(x)
    bed = np.zeros(n)
    # A minor-ish stack. The 55.4 against the 55 is the whole character: about 0.4 Hz of
    # beating, which is slow enough to read as unease rather than as an out-of-tune note.
    for f, g in [(55.0, 0.30), (55.4, 0.22), (82.5, 0.16), (110.0, 0.10),
                 (164.8, 0.05), (220.0, 0.03)]:
        bed += np.sin(2 * np.pi * f * x + np.sin(2 * np.pi * 0.07 * x) * 0.6) * g

    wind = lowpass(noise(n, 101), 140) * 6.0
    bed += wind * (0.26 + 0.18 * np.sin(2 * np.pi * 0.045 * x))
    bed *= 0.75 + 0.25 * np.sin(2 * np.pi * 0.033 * x)
    return normalise(loop(bed), 0.80)


def boss_bed():
    """
    The layer that fades in for a fight: a pulse, a fifth, and a hint of choir. Same length
    and same loop treatment as the ambient bed so the two can cross-fade at any point.
    """
    duration = 20.0
    x = t(duration)
    n = len(x)
    out = np.zeros(n)

    # A heartbeat at 84 bpm — fast enough to press, slow enough not to become a dance track.
    beat = 84.0 / 60.0
    phase = (x * beat) % 1.0
    kick = np.exp(-phase * 14.0)
    out += sweep(120, 45, n, 0.4) * kick * 0.55

    for f, g in [(65.4, 0.24), (98.0, 0.16), (130.8, 0.10)]:
        out += np.sin(2 * np.pi * f * x) * g
    # The "choir": three detuned saw-ish stacks well down in the mix.
    for f in (196.0, 233.1, 293.7):
        out += lowpass(np.sign(np.sin(2 * np.pi * f * x)) * 0.05, 900)
    out *= 0.8 + 0.2 * np.sin(2 * np.pi * 0.05 * x)
    return normalise(loop(out), 0.85)


SOUNDS = [
    ("sfx_gate_add", gate_add),
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
    ("mus_bed", ambient_bed),
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
