using System;

namespace BattleRunner.Core.World
{
    /// <summary>
    /// What a world puts beside its road, in three bands.
    ///
    /// This replaces the old `PropKind[] Props` — an array of ten procedural meshes shared by
    /// every world, drawn in one grey, in one pair of bands, at one scale range. Two worlds
    /// could differ only by which three of the ten they drew from, which is exactly what the
    /// player saw and called "minor colors only".
    ///
    /// THE THREE BANDS ARE NOT DECORATION, THEY ARE A BUDGET. The verge is close enough to
    /// read detail and close enough to distract, so it stays small and gets pushed toward the
    /// world's own stone. The field is the middle distance where trees, fences and carts read
    /// as a place being lived in. Landmarks are structures too large to be props at all — a
    /// castle, a mill, a mausoleum — and they are the only things the player will describe
    /// afterwards.
    ///
    /// Pieces are named rather than enumerated because the pack is generated: a name can be
    /// checked against the generated `SceneryPieces.Names` by a test, whereas a stale enum
    /// value would compile and then draw nothing.
    /// </summary>
    public sealed class SceneryPalette
    {
        public string[] Verge = Array.Empty<string>();
        public string[] Field = Array.Empty<string>();
        public string[] Landmarks = Array.Empty<string>();

        /// <summary>Roughly how many verge pieces per 100 m of one side.</summary>
        public float VergeDensity = 14f;
        /// <summary>Roughly how many field pieces per 100 m of one side.</summary>
        public float FieldDensity = 9f;
        /// <summary>
        /// Metres between SETTLEMENTS along the road. Landmarks stand inside those rather
        /// than walking a spacing of their own, which is what let a castle and the carts and
        /// fences around it agree about nothing. Floored by Settlements.Spacing, so a world
        /// authoring a short value gets denser hamlets rather than hamlets that merge back
        /// into an even scatter.
        /// </summary>
        public float LandmarkSpacing = 110f;

        // Kenney's models are authored at roughly one unit per module. Measured against the game's own scale rather than guessed: the road is 6.6 m
        // across and a soldier is about 1.5 m, so a 1.05-unit gravestone at 2.0 stands
        // 2.1 m — waist-high on the verge — a 1.93-unit pine at 2.8 stands 5.4 m, and the
        // castle keep, whose tallest part tops out at 7.67 units, stands 26 m. That is a
        // castle. At the 5.0 first tried it was 38 m and filled the sky.
        public float VergeScale = 2.0f;
        public float FieldScale = 2.8f;
        public float LandmarkScale = 3.4f;

        /// <summary>
        /// How far the verge is dragged toward the world's PropStone, 0..1.
        ///
        /// This is the "bright landmarks, dark verge" decision as a number. Kenney's palette
        /// is cheerful and saturated; the road the player actually looks down has to stay
        /// grim or the game stops being dark fantasy. Landmarks and the field keep their own
        /// colour, because contrast between a grim verge and a lit castle on the horizon is
        /// worth more than either alone.
        /// </summary>
        public float VergeTint = 0.72f;
        /// <summary>The same, gentler, for the middle distance.</summary>
        public float FieldTint = 0.28f;
        /// <summary>And barely at all for landmarks — they are the colour in the frame.</summary>
        public float LandmarkTint = 0.10f;

        public bool IsComplete =>
            Verge.Length > 0 && Field.Length > 0 && Landmarks.Length > 0;
    }
}
