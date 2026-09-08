using System;

namespace BattleRunner.Core.Progression
{
    /// <summary>
    /// How one round differs from the others wearing the same world.
    ///
    /// EIGHT WORLDS DIVIDED BY ROUNDS-FOREVER IS STILL A LOOP. An act shares a world so that
    /// arriving somewhere new feels like an event, but three to five identical rounds inside
    /// it would reproduce the complaint at a smaller scale. So each round shifts the world's
    /// palette, its fog depth, its star field, the angle of its light and how thickly the
    /// roadside is dressed — enough that no two rounds look alike, never so much that the
    /// world stops being recognisable.
    ///
    /// THE AMPLITUDE RAMPS ACROSS THE ACT. The first round of an act is close to the authored
    /// world, and each round after it drifts further. That is not decoration: it reads as
    /// going DEEPER into somewhere, and it means the player sees what the world actually looks
    /// like before it starts bending.
    ///
    /// Every value is a plain float derived from the round index by hash, so a round looks the
    /// same every time it is played, on every device, and the whole thing is testable without
    /// an engine.
    /// </summary>
    public readonly struct ThemeVariant
    {
        /// <summary>Fraction of the colour wheel to rotate the world's palette, +/-.</summary>
        public float HueShift { get; }

        /// <summary>Multiplies fog start and end together — smaller is thicker weather.</summary>
        public float FogScale { get; }

        /// <summary>Multiplies the sky's star strength.</summary>
        public float StarScale { get; }

        /// <summary>Degrees to swing the key light's azimuth, +/-. Moves every shadow on the road.</summary>
        public float LightAzimuth { get; }

        /// <summary>Multiplies roadside prop density.</summary>
        public float PropDensity { get; }

        /// <summary>Added to the road's wetness, +/-. Cheapest change that reads as weather.</summary>
        public float WetnessShift { get; }

        private ThemeVariant(float hue, float fog, float star, float azimuth, float props, float wet)
        {
            HueShift = hue;
            FogScale = fog;
            StarScale = star;
            LightAzimuth = azimuth;
            PropDensity = props;
            WetnessShift = wet;
        }

        /// <summary>The identity: the authored world, unmodified. What act round one nearly is.</summary>
        public static ThemeVariant None => new ThemeVariant(0f, 1f, 1f, 0f, 1f, 0f);

        /// <summary>
        /// The variation for a round. Amplitude ramps from a third at the top of an act to
        /// full at its end, so a world is introduced before it is bent.
        /// </summary>
        public static ThemeVariant For(RoundPlan plan)
        {
            int span = Math.Max(1, plan.ActLength - 1);
            float depth = 0.34f + 0.66f * (plan.RoundInAct / (float)span);
            int seed = plan.RoundIndex;

            return new ThemeVariant(
                hue: Signed(seed, 1) * 0.055f * depth,
                fog: 1f + Signed(seed, 2) * 0.20f * depth,
                star: 1f + Signed(seed, 3) * 0.45f * depth,
                azimuth: Signed(seed, 4) * 26f * depth,
                props: 1f + Signed(seed, 5) * 0.30f * depth,
                wet: Signed(seed, 6) * 0.18f * depth);
        }

        /// <summary>A stable value in [0,1) for one round and one axis.</summary>
        private static float Unit(int index, int salt)
        {
            // Knuth's multiplicative constant plus an xorshift, the same mixer CrowdRenderer
            // uses to give a soldier a stable archetype. All unchecked uint maths, so it is
            // bit-identical on Mono and .NET rather than merely close.
            unchecked
            {
                uint h = (uint)index * 2654435761u + (uint)salt * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        /// <summary>A stable value in [-1,1) for one round and one axis.</summary>
        private static float Signed(int index, int salt) => Unit(index, salt) * 2f - 1f;
    }
}
