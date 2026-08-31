using System;
using System.Numerics;

namespace BattleRunner.Core.Crowd
{
    /// <summary>
    /// Formation and steering math for the crowd (doc 04). Golden-angle packing grows the
    /// formation from the centre without reshuffling, bounded by a road-shaped envelope:
    /// width saturates at roughly one lane so the crowd never covers lanes it is not in,
    /// and count is carried by depth and density instead.
    /// </summary>
    public static class CrowdMath
    {
        /// <summary>Golden angle in radians.</summary>
        public const float GoldenAngle = 2.39996323f;

        /// <summary>Radius one body claims in an unconstrained disc; sets how fast the envelope fills.</summary>
        public const float NaturalRadius = 0.55f;

        /// <summary>Fraction of a lane the crowd may occupy, leaving road visible either side.</summary>
        public const float LaneFillFraction = 0.355f;

        /// <summary>
        /// How far the army may reach UP the road. Forward growth is nearly free: the rig is
        /// pitched 11.31 deg down with a 30 deg half-FOV, so the top-of-frame ray points
        /// 18.69 deg ABOVE horizontal and never meets the ground. Growth goes here.
        /// </summary>
        public const float FrontDepthMax = 7.00f;

        /// <summary>
        /// How far the army may trail BEHIND the anchor. This one is hard-limited: the
        /// bottom-of-frame ray meets the ground just 3.742 m back, and anything beyond that
        /// is off-screen however large the army gets.
        /// </summary>
        public const float RearDepthMax = 2.60f;

        /// <summary>The crowd's maximum half-width on a road of the given lane width.</summary>
        public static float HalfWidthMaxFor(float laneWidth)
        {
            if (laneWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(laneWidth));
            return laneWidth * LaneFillFraction;
        }

        /// <summary>Local-space (x, z) offset of an UNBOUNDED disc slot. Kept for reference and tests;
        /// gameplay uses <see cref="FormationSlot"/>, which bounds the disc to the road.</summary>
        public static Vector2 PhyllotaxisSlot(int index, float spacing)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == 0) return Vector2.Zero;
            float radius = spacing * MathF.Sqrt(index);
            float angle = index * GoldenAngle;
            return new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }

        /// <summary>
        /// Half-extents (x = half-width, z = half-depth) of the formation envelope.
        ///
        /// The envelope grows like an unconstrained disc while the crowd is small, then
        /// saturates smoothly on each axis independently. Saturation is what the shipped
        /// disc got wrong: compressing spacing by sqrt(40/n) exactly cancelled the sqrt(n)
        /// in the radius, so the radius pinned at 0.55*sqrt(40) = 3.48 m -- a 6.96 m blob,
        /// wider than all three 2.2 m lanes combined, from 40 bodies upward. The crowd
        /// covered every lane at once and steering stopped being visible.
        ///
        /// Width therefore belongs to the ROAD (it must fit a lane and pass through a gate)
        /// and count is carried by DEPTH instead — asymmetrically, because the two directions
        /// cost different amounts of screen. Returns (halfWidth, frontDepth, rearDepth).
        /// </summary>
        public static Vector3 FormationEnvelope(int count, float halfWidthMax)
        {
            if (halfWidthMax <= 0f) throw new ArgumentOutOfRangeException(nameof(halfWidthMax));
            if (count <= 0) return Vector3.Zero;

            float natural = NaturalRadius * MathF.Sqrt(count);
            return new Vector3(
                halfWidthMax * (1f - MathF.Exp(-natural / halfWidthMax)),
                FrontDepthMax * (1f - MathF.Exp(-natural / FrontDepthMax)),
                RearDepthMax * (1f - MathF.Exp(-natural / RearDepthMax)));
        }

        /// <summary>
        /// Local-space (x, z) offset of formation slot <paramref name="index"/> within a crowd
        /// of <paramref name="count"/> bodies, bounded by the envelope and shifted back by
        /// <paramref name="backBias"/> so the leader is genuinely at the front.
        ///
        /// Ordering is stable under growth: slot i keeps its angle (i * goldenAngle) and its
        /// radial rank (monotonic in i) for the crowd's whole lifetime, so a x2 gate rescales
        /// the formation without any two bodies ever swapping places.
        /// </summary>
        public static Vector2 FormationSlot(int index, int count, float halfWidthMax)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (index >= count) throw new ArgumentOutOfRangeException(nameof(index));

            Vector3 envelope = FormationEnvelope(count, halfWidthMax);
            float rank = MathF.Sqrt(index / (float)count); // equal-area fill of the unit disc
            float angle = index * GoldenAngle;
            float forward = rank * MathF.Sin(angle);
            return new Vector2(
                rank * MathF.Cos(angle) * envelope.X,
                forward * (forward >= 0f ? envelope.Y : envelope.Z));
        }

        /// <summary>
        /// Critically-damped spring step toward a target; returns the new position and
        /// updates velocity. Stable for dt spikes up to ~0.1 s at the default stiffness.
        /// </summary>
        public static float SpringDamperStep(float current, ref float velocity, float target, float stiffness, float dt)
        {
            if (stiffness <= 0f) throw new ArgumentOutOfRangeException(nameof(stiffness));
            float omega = stiffness;
            float x = current - target;
            float exp = 1f / (1f + omega * dt + 0.48f * omega * omega * dt * dt + 0.235f * omega * omega * omega * dt * dt * dt);
            float change = (velocity + omega * x * dt) * dt;
            velocity = (velocity - omega * omega * change * dt) * exp * exp;
            return target + (x + change) * exp * exp;
        }

        /// <summary>
        /// Half the drivable road: the outer edge of the outer lanes. Three lanes of
        /// laneWidth each, so the road spans [-1.5w, +1.5w] and its four edges sit at
        /// -1.5w, -0.5w, +0.5w, +1.5w.
        ///
        /// The road USED to be drawn 4.8w wide with lane lines only at +/-0.5w, so the
        /// centre lane read as 2.2 m while the outer two ran all the way to the rails at
        /// 2.3w and read as 3.96 m -- 1.8x the centre. Whatever draws the road must derive
        /// its edges from here, so what the player sees is the partition that scores.
        /// </summary>
        public static float RoadHalfWidth(float laneWidth)
        {
            if (laneWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(laneWidth));
            return laneWidth * 1.5f;
        }

        /// <summary>
        /// Which of the three lanes a world X sits in: -1, 0 or +1.
        ///
        /// A radius test cannot do this job. With lane centres one laneWidth apart, the
        /// shipped acceptance half-width of laneWidth*0.75 made adjacent windows overlap by
        /// laneWidth/2 each, so a crowd parked half a lane off centre satisfied two lanes at
        /// once and could collect a +gate and a -gate on the same frame. Rounding partitions
        /// the road into exactly three windows: no overlap, no gap.
        /// </summary>
        public static int LaneIndex(float worldX, float laneWidth)
        {
            if (laneWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(laneWidth));
            int lane = (int)MathF.Round(worldX / laneWidth, MidpointRounding.AwayFromZero);
            if (lane < -1) return -1;
            return lane > 1 ? 1 : lane;
        }

        /// <summary>How many units to actually render for a logical force count, per device tier cap (doc 01, R2).</summary>
        public static int VisibleUnits(long forceCount, int tierCap)
        {
            if (tierCap <= 0) throw new ArgumentOutOfRangeException(nameof(tierCap));
            if (forceCount <= 0) return 0;
            return forceCount > tierCap ? tierCap : (int)forceCount;
        }

        /// <summary>Hero scale bump expressing over-cap growth the bodies can't (1.0 at or below cap).</summary>
        public static float HeroScaleFor(long forceCount, int tierCap)
        {
            if (forceCount <= tierCap || tierCap <= 0) return 1f;
            double ratio = (double)forceCount / tierCap;
            return 1f + 0.35f * (float)Math.Log10(ratio + 1.0);
        }
    }
}
