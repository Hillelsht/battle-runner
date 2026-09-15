using System;

namespace BattleRunner.Core.World
{
    /// <summary>Which shape crosses over the road in a world. None means nothing does.</summary>
    public enum ArchStyle
    {
        None = 0,
        /// <summary>A pointed span with crosses on it. Graveyard gothic.</summary>
        Gothic = 1,
        /// <summary>A timbered gateway under a pitched roof, with banners. Storybook.</summary>
        Timber = 2,
        /// <summary>A rib: two curved shafts meeting overhead, no lintel at all.</summary>
        Rib = 3,
        /// <summary>A span that has already fallen, leaving one leg and a stub.</summary>
        Broken = 4,
        /// <summary>A thin frozen bridge, flat and far overhead.</summary>
        Frozen = 5
    }

    /// <summary>
    /// The numbers an arch is built from, and the reason they live in Core.
    ///
    /// AN ARCH IS THE ONE PIECE OF SCENERY THAT CROSSES THE LANE THE PLAYER RUNS IN. Every
    /// other thing the world places is held off the road by a clearance rule; this is placed
    /// ON the centreline on purpose. So the constraint that stops it becoming "a screenshot of
    /// a wall across the track" is arithmetic — the legs must stand outside the road and the
    /// span must clear the army — and arithmetic belongs where a test can reach it.
    ///
    /// The pack cannot supply these. `SceneryPieces` bakes a height per piece and nothing
    /// else, so building an arch out of `ca_gate` would mean guessing how wide a gate is, and
    /// a guess that is wrong by a metre puts a pillar in the middle lane.
    /// </summary>
    public readonly struct ArchShape
    {
        /// <summary>Distance from the centreline to the INNER face of a leg.</summary>
        public readonly float LegInner;
        /// <summary>And to its outer face. The difference is how heavy the arch reads.</summary>
        public readonly float LegOuter;
        /// <summary>Height of the underside of the span.</summary>
        public readonly float Clearance;
        /// <summary>Total height, including whatever rides on top.</summary>
        public readonly float Crown;
        /// <summary>Thickness along the road. A thin arch flickers past; a thick one is a tunnel.</summary>
        public readonly float Depth;

        public ArchShape(float legInner, float legOuter, float clearance, float crown, float depth)
        {
            LegInner = legInner;
            LegOuter = legOuter;
            Clearance = clearance;
            Crown = crown;
            Depth = depth;
        }
    }

    public static class RoadArches
    {
        /// <summary>
        /// Half the road, at the shipped 2.2 m lane. Not read from CrowdMath because that
        /// takes a lane width and this is a constant a test compares against; a test asserts
        /// the two agree, so the duplicate cannot drift.
        /// </summary>
        public const float RoadHalfWidth = 3.30f;

        /// <summary>
        /// How much daylight a leg must leave beside the road. The rails already stand at
        /// 3.76, so anything under that would be an arch growing out of a railing.
        /// </summary>
        public const float LegMargin = 0.75f;

        /// <summary>
        /// The lowest a span may pass over the road.
        ///
        /// A soldier is about 1.5 m and the hero is drawn at 1.35x, so the army itself needs
        /// barely two. This is 4.5 because the CAMERA sits 5.5 m up and looks down the road:
        /// a span at two metres would cut across the middle of the frame and hide the gates
        /// the player is reading. An arch has to change the skyline without occluding the game.
        /// </summary>
        public const float MinClearance = 4.5f;

        private static readonly ArchShape[] Table =
        {
            // None — a world with nothing overhead. Sized anyway so nothing has to special-case it.
            new ArchShape(4.2f, 5.0f, 5.0f, 5.5f, 0.6f),
            // Gothic: narrow legs, high pointed crown. The tallest of the five, because a
            // graveyard arch is meant to be the thing on the skyline.
            new ArchShape(4.15f, 5.10f, 5.20f, 8.40f, 0.70f),
            // Timber: wide, low and heavy under a pitched roof — a gateway you go THROUGH
            // rather than under. The lowest crown, which is what makes it feel domestic.
            new ArchShape(4.30f, 5.60f, 4.70f, 6.60f, 1.40f),
            // Rib: the thinnest legs in the set and no lintel, so the shape is two curves
            // meeting. Deliberately the least architectural of the five.
            //
            // Clearance 4.80 and crown 8.40, WIDER APART than the 5.60/7.20 first authored,
            // and the gap is the point: a rib is the one style whose shafts lean in over the
            // road, so the only place it can do that is above the clearance line. With 1.6 m
            // to work in the lean was so abrupt it read as a break; with 3.6 m it is a curve.
            new ArchShape(4.10f, 4.70f, 4.80f, 8.40f, 0.50f),
            // Broken: one leg standing and a stub opposite. Asymmetric on purpose — a
            // symmetric ruin reads as a design rather than as damage.
            new ArchShape(4.20f, 5.40f, 5.00f, 7.60f, 0.90f),
            // Frozen: a flat slab far overhead on slender columns. Highest clearance, lowest
            // mass, so it reads as something that could come down.
            new ArchShape(4.05f, 4.80f, 6.40f, 7.10f, 1.10f)
        };

        public const int Count = 6;

        public static ArchShape For(ArchStyle style)
        {
            int i = (int)style;
            return i < 0 || i >= Table.Length ? Table[0] : Table[i];
        }

        /// <summary>
        /// Where the arches of a round stand, given the round's length and the world's
        /// spacing. Returned as a count and a stride rather than a list so Core stays
        /// allocation-free on a path the field walks every time it dresses.
        /// </summary>
        public static int CountOver(float roadLength, float spacing)
        {
            if (spacing <= 0f || roadLength <= 0f) return 0;
            return (int)(roadLength / spacing);
        }

        /// <summary>
        /// The z of the n-th arch. Offset by half a stride so an arch never lands exactly on
        /// the start line, where it would be the first thing the player sees and would read as
        /// a loading screen rather than as something they run under.
        /// </summary>
        public static float PositionOf(int index, float spacing) =>
            (index + 0.5f) * spacing;
    }
}
