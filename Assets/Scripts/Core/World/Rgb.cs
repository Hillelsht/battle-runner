using System;

namespace BattleRunner.Core.World
{
    /// <summary>
    /// A colour, without an engine.
    ///
    /// The theme table has to live in Core: the test assembly references Core and nothing
    /// else, on purpose, so that every test runs identically under headless `dotnet test` and
    /// under Unity's runner. A palette expressed in UnityEngine.Color would be untestable
    /// here, and the relationship between a world's sky and its fog is exactly the kind of
    /// thing that should be pinned rather than eyeballed.
    ///
    /// Values are LINEAR-INTENT sRGB, matching what the .mat files and the existing material
    /// constants already carry; the Gameplay adapter hands them to Unity unchanged.
    /// </summary>
    public readonly struct Rgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Rgb Scaled(float factor) => new Rgb(R * factor, G * factor, B * factor);

        /// <summary>The brightest channel. Zero only for pure black.</summary>
        public float Peak => Math.Max(R, Math.Max(G, B));

        /// <summary>How far from grey, 0..1. A signature colour scores high; ash scores low.</summary>
        public float Chroma
        {
            get
            {
                float peak = Peak;
                return peak <= 1e-4f ? 0f : (peak - Math.Min(R, Math.Min(G, B))) / peak;
            }
        }

        /// <summary>
        /// The same hue at unit brightness. Theme colours are authored HDR — an accent can
        /// sit at 1.5 — so anything that wants the HUE of a colour without its intensity has
        /// to divide the intensity out first rather than clamp it away.
        /// </summary>
        public Rgb Normalized
        {
            get
            {
                float peak = Peak;
                return peak <= 1e-4f ? new Rgb(1f, 1f, 1f) : Scaled(1f / peak);
            }
        }

        /// <summary>Pulled toward white. 0 keeps the colour, 1 discards it.</summary>
        public Rgb TowardWhite(float t) =>
            new Rgb(R + (1f - R) * t, G + (1f - G) * t, B + (1f - B) * t);

        /// <summary>
        /// Scaled so the three channels average 1. Grading multipliers work this way: the
        /// tint is the RATIO between channels, and letting the average drift would change
        /// the exposure as a side effect of changing the hue.
        /// </summary>
        public Rgb MeanNormalized
        {
            get
            {
                float mean = (R + G + B) / 3f;
                return mean <= 1e-4f ? new Rgb(1f, 1f, 1f) : Scaled(1f / mean);
            }
        }

        /// <summary>Rec. 709 relative luminance — what the eye reads as "how bright".</summary>
        public float Luma => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        /// <summary>
        /// The same colour at no less than a given luminance, scaled rather than lifted
        /// toward white so the HUE SURVIVES.
        ///
        /// Adding a constant to all three channels would raise the luminance and desaturate
        /// at the same time, which turns a world's signature colour into grey — the opposite
        /// of what a floor on darkness is for. Scaling keeps the ratios, so an ochre stays
        /// ochre and simply stops being a hole in the frame.
        /// </summary>
        public Rgb AtLeastLuma(float minimum)
        {
            float luma = Luma;
            if (luma >= minimum || luma <= 1e-5f) return this;
            return Scaled(minimum / luma);
        }

        public static Rgb Lerp(Rgb a, Rgb b, float t) =>
            new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        public override string ToString() => $"({R:0.###}, {G:0.###}, {B:0.###})";
    }
}
