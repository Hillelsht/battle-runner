using System;

namespace BattleRunner.Core.Audio
{
    /// <summary>
    /// How the one ambient bed is bent to sound like a particular world.
    ///
    /// EIGHT WORLDS DO NOT GET EIGHT MUSIC FILES. A 20-second stereo bed is most of a
    /// megabyte in the repo, and eight of them would be more bytes than the entire rest of
    /// the project. Instead one bed is re-pitched and filtered per world, which is the same
    /// "one asset, themed" move the sky, the road and the scenery pack already make — and it
    /// means a world costs two numbers rather than a recording session.
    ///
    /// Pitch does double duty on a drone: it shifts the fundamental AND stretches the loop,
    /// so two worlds a fifth apart do not phase against each other on a level transition.
    /// </summary>
    public readonly struct MusicMood
    {
        public readonly float Pitch;
        /// <summary>Low-pass cutoff in Hz. Low is muffled and underwater; high is open and cold.</summary>
        public readonly float Cutoff;
        public readonly float Volume;

        public MusicMood(float pitch, float cutoff, float volume)
        {
            Pitch = pitch;
            Cutoff = cutoff;
            Volume = volume;
        }

        /// <summary>Below this the filter is doing nothing audible; above it, it is a wall.</summary>
        public const float MinCutoff = 240f;
        public const float MaxCutoff = 22000f;
        public const float MinPitch = 0.62f;
        public const float MaxPitch = 1.45f;

        /// <summary>
        /// A world's mood, derived from what it already declares rather than authored twice.
        ///
        /// Two signals, both of which a player can already see:
        ///   - HOW FAR YOU CAN SEE. Fog is the strongest cue of enclosure a world has. A
        ///     world that fogs out at 105 m is inside something and should sound muffled; the
        ///     Bone Wastes at 185 m is open sky and should sound open.
        ///   - HOW HIGH THE SKY SITS. A cold, blue-white zenith pitches up; a heavy warm one
        ///     pitches down. Using the zenith's blue-to-red balance rather than its brightness
        ///     keeps this independent of how dark a world is.
        ///
        /// Deriving rather than authoring means a ninth world cannot ship silent-by-default
        /// or, worse, sounding exactly like the first.
        /// </summary>
        public static MusicMood For(float fogEnd, float zenithRed, float zenithBlue, float accentChroma)
        {
            // 35..185 is the authored range of FogEnd across the roster.
            float openness = Clamp01((fogEnd - 35f) / 150f);
            float cutoff = MinCutoff + (float)Math.Pow(openness, 0.7) * (5200f - MinCutoff);

            float warmth = zenithRed + zenithBlue <= 1e-5f
                ? 0.5f
                : Clamp01(zenithBlue / (zenithRed + zenithBlue));
            // Cold skies ride up, warm ones sit down; a fifth of a range either side of unity
            // is enough to hear and small enough that the loop does not audibly stretch.
            float pitch = 0.86f + warmth * 0.34f;

            // A chromatic world gets a touch more presence, so the music agrees with the
            // grade rather than contradicting it.
            float volume = 0.52f + 0.16f * Clamp01(accentChroma);

            return new MusicMood(
                Clamp(pitch, MinPitch, MaxPitch),
                Clamp(cutoff, MinCutoff, MaxCutoff),
                Clamp(volume, 0f, 1f));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
