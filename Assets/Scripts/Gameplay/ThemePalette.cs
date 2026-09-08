using BattleRunner.Core.World;
using UnityEngine;

namespace BattleRunner.Gameplay
{
    /// <summary>
    /// Turns Core's engine-free palette into Unity colours, and rotates a world's hue for the
    /// per-round variation.
    ///
    /// The rotation goes through YIQ rather than through Color.RGBToHSV. HSV round-trips clamp
    /// to [0,1], and several of these values are deliberately HDR — an accent at 1.5 exists to
    /// clear the 0.85 bloom threshold, and squashing it to 1.0 would quietly switch the glow
    /// off. A YIQ rotation is a linear map: it moves the hue and leaves the magnitude alone,
    /// so an emissive colour stays emissive.
    /// </summary>
    public static class ThemePalette
    {
        public static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        /// <summary>A world's colour, rotated by the round's hue shift (in turns, not degrees).</summary>
        public static Color Shifted(Rgb c, float hueTurns) => HueRotate(ToColor(c), hueTurns);

        /// <summary>
        /// Rotate a colour's hue about the luma axis. <paramref name="turns"/> is a fraction of
        /// the full wheel, so 0.5 is the opposite hue and 0 is the identity.
        /// </summary>
        public static Color HueRotate(Color c, float turns)
        {
            if (Mathf.Approximately(turns, 0f)) return c;

            float angle = turns * 2f * Mathf.PI;
            float u = Mathf.Cos(angle);
            float w = Mathf.Sin(angle);

            float y = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float i = 0.596f * c.r - 0.274f * c.g - 0.322f * c.b;
            float q = 0.211f * c.r - 0.523f * c.g + 0.312f * c.b;

            float i2 = i * u - q * w;
            float q2 = i * w + q * u;

            // Negatives are possible at the edge of the gamut and mean nothing to a shader;
            // clamping the floor is enough, and the ceiling is left open on purpose so HDR
            // survives the trip.
            return new Color(
                Mathf.Max(0f, y + 0.956f * i2 + 0.621f * q2),
                Mathf.Max(0f, y - 0.272f * i2 - 0.647f * q2),
                Mathf.Max(0f, y - 1.106f * i2 + 1.703f * q2),
                c.a);
        }
    }
}
