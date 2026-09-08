using System;

namespace BattleRunner.Core.Audio
{
    /// <summary>
    /// Every sound the game makes. The enum is the contract: a cue that has no clip is a
    /// silent bug, so the table below and `tooling/synth_audio.py` are checked against each
    /// other by a test rather than kept in step by hand.
    /// </summary>
    public enum AudioCue
    {
        GateAdd = 0,
        GateMultiply,
        GateSubtract,
        EnemyBite,
        SpellCast,
        SpellHit,
        ShieldRaise,
        ShieldBlock,
        BossTelegraph,
        BossBlow,
        BossHit,
        BossDeath,
        LootReveal,
        UiTap,
        RoundStart
    }

    /// <summary>How a cue is mixed. Volume is linear; pitch multiplies playback rate.</summary>
    public readonly struct CueMix
    {
        public readonly string Clip;
        public readonly float Volume;
        /// <summary>Random pitch spread, +/- this fraction, so repeats do not machine-gun.</summary>
        public readonly float PitchJitter;
        /// <summary>
        /// Seconds before the same cue may retrigger. Gate chimes fire in bursts when a
        /// player weaves a Ladder, and a dozen identical bells inside 200 ms is not twelve
        /// times as satisfying — it is a buzz.
        /// </summary>
        public readonly float MinInterval;
        /// <summary>
        /// Louder cues that must never be lost. The voice pool evicts the quietest first,
        /// and a boss blow being dropped because the crowd is passing gates is a fairness
        /// problem, not an audio one.
        /// </summary>
        public readonly int Priority;

        public CueMix(string clip, float volume, float pitchJitter, float minInterval, int priority)
        {
            Clip = clip;
            Volume = volume;
            PitchJitter = pitchJitter;
            MinInterval = minInterval;
            Priority = priority;
        }
    }

    /// <summary>
    /// The mix, in Core so it can be reasoned about and tested without an engine — the same
    /// reason the world palette and the mesh format live here. Nothing in this file touches
    /// UnityEngine; AudioDirector reads it.
    /// </summary>
    public static class AudioCues
    {
        public const int Count = (int)AudioCue.RoundStart + 1;

        /// <summary>Directory inside Resources. Clips load by NAME so any file can be swapped.</summary>
        public const string ResourceFolder = "Audio/";

        private static readonly CueMix[] Table =
        {
            //                clip              vol   jitter  gap    priority
            new CueMix("sfx_gate_add",        0.55f, 0.06f, 0.045f, 1),
            new CueMix("sfx_gate_multiply",   0.70f, 0.04f, 0.090f, 2),
            new CueMix("sfx_gate_subtract",   0.62f, 0.07f, 0.060f, 2),
            new CueMix("sfx_enemy_bite",      0.50f, 0.12f, 0.055f, 1),
            new CueMix("sfx_spell_cast",      0.68f, 0.05f, 0.100f, 3),
            new CueMix("sfx_spell_hit",       0.80f, 0.06f, 0.100f, 3),
            new CueMix("sfx_shield_raise",    0.60f, 0.03f, 0.150f, 3),
            new CueMix("sfx_shield_block",    0.85f, 0.05f, 0.070f, 4),
            new CueMix("sfx_boss_telegraph",  0.70f, 0.02f, 0.300f, 4),
            new CueMix("sfx_boss_blow",       0.95f, 0.04f, 0.120f, 5),
            new CueMix("sfx_boss_hit",        0.55f, 0.10f, 0.050f, 2),
            new CueMix("sfx_boss_death",      1.00f, 0.00f, 1.000f, 5),
            new CueMix("sfx_loot_reveal",     0.75f, 0.02f, 0.300f, 4),
            new CueMix("sfx_ui_tap",          0.45f, 0.04f, 0.040f, 1),
            new CueMix("sfx_round_start",     0.80f, 0.00f, 0.500f, 4)
        };

        /// <summary>The looping beds. Not cues: they are cross-faded, not triggered.</summary>
        public const string AmbientBed = "mus_bed";
        public const string BossBed = "mus_boss";

        public static CueMix For(AudioCue cue)
        {
            int i = (int)cue;
            if (i < 0 || i >= Table.Length)
                throw new ArgumentOutOfRangeException(nameof(cue), cue, "no mix for this cue");
            return Table[i];
        }

        /// <summary>Every clip name the game will try to load, beds included.</summary>
        public static string[] AllClipNames()
        {
            var names = new string[Table.Length + 2];
            for (int i = 0; i < Table.Length; i++) names[i] = Table[i].Clip;
            names[Table.Length] = AmbientBed;
            names[Table.Length + 1] = BossBed;
            return names;
        }
    }
}
