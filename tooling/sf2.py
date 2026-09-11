#!/usr/bin/env python3
"""A minimal SoundFont 2 reader and renderer — enough to play real instruments, in numpy.

WHY THIS EXISTS. The music was synthesised, and synthesis has a ceiling: Karplus-Strong is a
plucked string, a summed sine stack is a pad, and neither of them is a cello. Told plainly that
the result was still bad, the honest move is not a better oscillator — it is to stop
synthesising and play recordings of instruments.

WHY IT IS A BUILD-TIME TOOL AND NOT A RUNTIME ONE. Unity has no SoundFont synthesiser. Playing
an .sf2 in-app means bundling FluidSynth or TinySoundFont as a native .so per ABI, plus 30 MB
resident and 32 MB of install size, on a hybrid-casual Android title where install size is the
single most conversion-sensitive number there is. That trade is indefensible.

Rendering OFFLINE costs none of it. The bank is downloaded into tooling/.cache (git-ignored,
never committed), the beds are rendered to the same 22 kHz mono WAV the project already ships,
and the device sees exactly what it saw before — two audio files. This is the same move the
meshes, the sky, the road surfaces and the UI sprites all already make: generate offline, commit
the output, ship no tool.

THE LICENCE COVERS EXACTLY THIS. GeneralUser GS v2.0.3, documentation/LICENSE.txt: "You may use
GeneralUser GS without restriction for your own music creation, private or commercial", and the
samples "allow full use in music production, including the ability to make profit from musical
recordings created with GeneralUser GS". A rendered bed IS a musical recording created with it.
No share-alike, no attribution requirement, no non-commercial clause.

WHAT IS IMPLEMENTED, AND WHAT IS DELIBERATELY NOT. Preset and instrument zone resolution, key
ranges, the sample loop, tuning, and the volume envelope — the things that decide whether a note
sounds like the instrument. NOT implemented: modulators, the low-pass filter, LFOs, velocity
layers, stereo pan. The output is mono and the project's whole mix is mono, so pan is free to
drop; the rest would buy detail that a 22 kHz bed under a runner cannot carry anyway.
"""
import os
import struct

import numpy as np

# --- generator operators, by number. Only the ones that change the note. ---
GEN_START_OFF = 0
GEN_END_OFF = 1
GEN_STARTLOOP_OFF = 2
GEN_ENDLOOP_OFF = 3
GEN_START_COARSE = 4
GEN_END_COARSE = 12
GEN_PAN = 17
GEN_DELAY_VOL = 33
GEN_ATTACK_VOL = 34
GEN_HOLD_VOL = 35
GEN_DECAY_VOL = 36
GEN_SUSTAIN_VOL = 37
GEN_RELEASE_VOL = 38
GEN_KEY_RANGE = 43
GEN_VEL_RANGE = 44
GEN_STARTLOOP_COARSE = 45
GEN_INITIAL_ATTENUATION = 48
GEN_ENDLOOP_COARSE = 50
GEN_COARSE_TUNE = 51
GEN_FINE_TUNE = 52
GEN_SAMPLE_ID = 53
GEN_SAMPLE_MODES = 54
GEN_ROOT_KEY = 58
GEN_INSTRUMENT = 41

# Generators that are an INDEX or a RANGE rather than a value. A preset-level zone ADDS its
# generators to the instrument's; adding two sample indices together would be nonsense, so
# these are the ones that must be overridden instead of summed.
NON_ADDITIVE = {GEN_INSTRUMENT, GEN_SAMPLE_ID, GEN_KEY_RANGE, GEN_VEL_RANGE, GEN_SAMPLE_MODES}


def _timecents(value, default):
    """SoundFont durations are in timecents: 1200 per octave of doubling, from one second."""
    if value is None:
        return default
    if value <= -32000:
        return 0.0
    return float(2.0 ** (value / 1200.0))


class SoundFont:
    def __init__(self, path):
        with open(path, "rb") as f:
            data = f.read()
        if data[:4] != b"RIFF" or data[8:12] != b"sfbk":
            raise ValueError(f"{path} is not a SoundFont 2 bank")

        chunks = {}
        self._walk(data, 12, len(data), chunks)

        # The sample pool: one long 16-bit signed stream that every shdr indexes into.
        raw = chunks["smpl"]
        self.samples = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0

        self.phdr = self._records(chunks["phdr"], 38)
        self.pbag = self._records(chunks["pbag"], 4)
        self.pgen = self._records(chunks["pgen"], 4)
        self.inst = self._records(chunks["inst"], 22)
        self.ibag = self._records(chunks["ibag"], 4)
        self.igen = self._records(chunks["igen"], 4)
        self.shdr = self._records(chunks["shdr"], 46)

    @staticmethod
    def _walk(data, at, end, out):
        """Flatten the RIFF tree. Nested LISTs are transparent; leaf chunks are what matter."""
        while at + 8 <= end:
            tag = data[at:at + 4]
            size = struct.unpack("<I", data[at + 4:at + 8])[0]
            body = at + 8
            if tag == b"LIST":
                SoundFont._walk(data, body + 4, min(body + size, end), out)
            else:
                out[tag.decode("ascii", "replace")] = data[body:body + size]
            at = body + size + (size & 1)          # chunks are word-aligned

    @staticmethod
    def _records(blob, width):
        return [blob[i:i + width] for i in range(0, len(blob) - width + 1, width)]

    # --- zone walking ------------------------------------------------------

    @staticmethod
    def _gens(gen_records, first, last):
        out = {}
        for i in range(first, min(last, len(gen_records))):
            oper, amount = struct.unpack("<HH", gen_records[i])
            out[oper] = amount
        return out

    def _preset_index(self, bank, program):
        for i, rec in enumerate(self.phdr[:-1]):      # last phdr is the terminal record
            preset, bnk = struct.unpack("<HH", rec[20:24])
            if bnk == bank and preset == program:
                return i
        raise KeyError(f"no preset for bank {bank} program {program}")

    def zone_for(self, bank, program, key):
        """
        Resolve one (bank, program, key) down to a sample and a complete generator set.

        Two levels, and the order matters: the INSTRUMENT zone supplies the sample and its
        defaults, and the PRESET zone then adds its own generators on top. Getting that
        backwards, or summing an index, produces a note that plays the wrong sample at the
        wrong pitch — which sounds like a bug in the music rather than a bug in the reader.
        """
        pi = self._preset_index(bank, program)
        pbag_first = struct.unpack("<H", self.phdr[pi][24:26])[0]
        pbag_last = struct.unpack("<H", self.phdr[pi + 1][24:26])[0]

        preset_global = {}
        chosen = None
        for b in range(pbag_first, pbag_last):
            gen_first = struct.unpack("<H", self.pbag[b][0:2])[0]
            gen_last = struct.unpack("<H", self.pbag[b + 1][0:2])[0]
            gens = self._gens(self.pgen, gen_first, gen_last)
            if GEN_INSTRUMENT not in gens:
                preset_global = gens                 # a zone with no instrument is the global one
                continue
            if GEN_KEY_RANGE in gens:
                lo, hi = gens[GEN_KEY_RANGE] & 0xFF, gens[GEN_KEY_RANGE] >> 8
                if not (lo <= key <= hi):
                    continue
            chosen = gens
            break
        if chosen is None:
            return None

        merged_preset = dict(preset_global)
        merged_preset.update(chosen)

        ii = merged_preset[GEN_INSTRUMENT]
        ibag_first = struct.unpack("<H", self.inst[ii][20:22])[0]
        ibag_last = struct.unpack("<H", self.inst[ii + 1][20:22])[0]

        inst_global = {}
        zone = None
        for b in range(ibag_first, ibag_last):
            gen_first = struct.unpack("<H", self.ibag[b][0:2])[0]
            gen_last = struct.unpack("<H", self.ibag[b + 1][0:2])[0]
            gens = self._gens(self.igen, gen_first, gen_last)
            if GEN_SAMPLE_ID not in gens:
                inst_global = gens
                continue
            if GEN_KEY_RANGE in gens:
                lo, hi = gens[GEN_KEY_RANGE] & 0xFF, gens[GEN_KEY_RANGE] >> 8
                if not (lo <= key <= hi):
                    continue
            zone = gens
            break
        if zone is None:
            return None

        final = dict(inst_global)
        final.update(zone)
        # The preset layer ADDS, except for the indices and ranges.
        for oper, amount in merged_preset.items():
            if oper in NON_ADDITIVE:
                continue
            signed = amount - 65536 if amount > 32767 else amount
            base = final.get(oper, 0)
            base = base - 65536 if base > 32767 else base
            final[oper] = base + signed
        return final

    # --- rendering ---------------------------------------------------------

    def note(self, bank, program, key, seconds, sample_rate, gain=1.0, release=0.35):
        """One note, mono, at `sample_rate`. Returns float32, or silence if unresolvable."""
        total = int((seconds + release) * sample_rate)
        gens = self.zone_for(bank, program, key)
        if gens is None:
            return np.zeros(total, dtype=np.float32)

        def g(oper, default=None):
            if oper not in gens:
                return default
            v = gens[oper]
            return v - 65536 if v > 32767 else v

        sid = gens[GEN_SAMPLE_ID]
        shdr = self.shdr[sid]
        start, end, loop_start, loop_end, rate = struct.unpack("<IIIII", shdr[20:40])
        root, correction = struct.unpack("<Bb", shdr[40:42])

        start += (g(GEN_START_OFF, 0) or 0) + 32768 * (g(GEN_START_COARSE, 0) or 0)
        end += (g(GEN_END_OFF, 0) or 0) + 32768 * (g(GEN_END_COARSE, 0) or 0)
        loop_start += (g(GEN_STARTLOOP_OFF, 0) or 0) + 32768 * (g(GEN_STARTLOOP_COARSE, 0) or 0)
        loop_end += (g(GEN_ENDLOOP_OFF, 0) or 0) + 32768 * (g(GEN_ENDLOOP_COARSE, 0) or 0)
        if end <= start or end > len(self.samples):
            return np.zeros(total, dtype=np.float32)

        override = g(GEN_ROOT_KEY, -1)
        if override is not None and 0 <= override <= 127:
            root = override
        cents = (key - root) * 100 + (g(GEN_COARSE_TUNE, 0) or 0) * 100 \
                + (g(GEN_FINE_TUNE, 0) or 0) + correction
        step = (2.0 ** (cents / 1200.0)) * (rate / float(sample_rate))
        if not np.isfinite(step) or step <= 0:
            return np.zeros(total, dtype=np.float32)

        looping = (g(GEN_SAMPLE_MODES, 0) or 0) & 1
        loop_len = loop_end - loop_start
        if loop_len < 32:
            looping = 0

        # Walk the sample at `step` per output sample, wrapping inside the loop while held.
        # A linear read past the loop is the difference between a sustained note and a note
        # that stops dead a third of a second in — which is what a pad made of one-shots is.
        pos = np.arange(total, dtype=np.float64) * step
        if looping:
            rel = pos - (loop_start - start)
            inside = rel >= 0
            wrapped = np.where(inside, (rel % (loop_end - loop_start)) + (loop_start - start), pos)
            idx = wrapped + start
        else:
            idx = np.minimum(pos + start, end - 2)

        i0 = np.floor(idx).astype(np.int64)
        frac = (idx - i0).astype(np.float32)
        i0 = np.clip(i0, 0, len(self.samples) - 2)
        out = self.samples[i0] * (1.0 - frac) + self.samples[i0 + 1] * frac

        # Volume envelope, in the SoundFont's own timecent units.
        attack = _timecents(g(GEN_ATTACK_VOL), 0.001)
        hold = _timecents(g(GEN_HOLD_VOL), 0.0)
        decay = _timecents(g(GEN_DECAY_VOL), 0.0)
        sustain_cb = g(GEN_SUSTAIN_VOL, 0) or 0
        sustain = float(np.clip(10.0 ** (-sustain_cb / 200.0), 0.0, 1.0))

        t = np.arange(total, dtype=np.float32) / sample_rate
        envelope = np.ones(total, dtype=np.float32)
        if attack > 0:
            envelope = np.minimum(t / attack, 1.0).astype(np.float32)
        after = np.maximum(t - attack - hold, 0.0)
        if decay > 0:
            envelope *= (sustain + (1.0 - sustain) * np.exp(-after / (decay / 3.0))).astype(np.float32)

        # A hard stop at the end of a note is a click; ramp the tail into the release window.
        tail = int(release * sample_rate)
        if 0 < tail < total:
            envelope[total - tail:] *= np.linspace(1.0, 0.0, tail, dtype=np.float32) ** 1.5

        attenuation = 10.0 ** (-(g(GEN_INITIAL_ATTENUATION, 0) or 0) / 200.0)
        return (out * envelope * attenuation * gain).astype(np.float32)


_LOADED = {}


def load(path):
    """Cached, because a bank is 32 MB and a bed asks it for a hundred notes."""
    if path not in _LOADED:
        _LOADED[path] = SoundFont(path)
    return _LOADED[path]
