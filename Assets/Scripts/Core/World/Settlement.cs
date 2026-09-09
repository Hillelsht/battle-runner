namespace BattleRunner.Core.World
{
    /// <summary>
    /// One cluster of buildings beside the road — a hamlet, a camp, a ruin — with an origin,
    /// an extent, and a falloff.
    ///
    /// WHY CLUSTERING IS THE WHOLE POINT. Scenery was scattered EVENLY: a piece every so many
    /// metres, jittered, on both sides, for the length of the level. Even scatter is what
    /// texture looks like, not what a place looks like. A village is buildings touching each
    /// other with empty ground between clusters, and the negative space is what makes the
    /// cluster read as a settlement rather than as density. Held against the two photoreal
    /// references, everything past about thirty metres collapsed into one flat silhouette,
    /// and that is why.
    ///
    /// This is deliberately in Core with no engine types in it: whether a layout actually
    /// leaves empty ground between its settlements is arithmetic, and arithmetic can be
    /// tested. The placement itself stays in SceneryField, which owns the random stream.
    /// </summary>
    public readonly struct Settlement
    {
        /// <summary>Centre, along the road.</summary>
        public readonly float Z;
        /// <summary>Centre, across it. Always positive; Side says which verge.</summary>
        public readonly float X;
        /// <summary>-1 or +1.</summary>
        public readonly int Side;
        /// <summary>Where the density has fallen to zero.</summary>
        public readonly float Radius;
        /// <summary>
        /// Which way the settlement faces, in degrees. Buildings in a hamlet share an
        /// orientation because they share a street; individually random yaws are the other
        /// half of why a cluster reads as scatter.
        /// </summary>
        public readonly float Yaw;

        public Settlement(float z, float x, int side, float radius, float yaw)
        {
            Z = z;
            X = x;
            Side = side;
            Radius = radius > 0.01f ? radius : 0.01f;
            Yaw = yaw;
        }

        /// <summary>
        /// How dense the settlement is at a point, 1 at the centre falling to 0 at the rim.
        ///
        /// Smoothstep rather than linear, and squared toward the centre: a linear falloff
        /// puts most of its area in the outer ring, because area grows with the square of the
        /// radius, so the cluster comes out as a ring of buildings around an empty middle.
        /// </summary>
        public float Weight(float x, float z)
        {
            float dx = x - X * Side;
            float dz = z - Z;
            float d = (dx * dx + dz * dz) / (Radius * Radius);
            if (d >= 1f) return 0f;
            float t = 1f - d;
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// How settlements are spaced, and how much of the road is left empty between them.
    /// </summary>
    public static class Settlements
    {
        /// <summary>
        /// Empty ground between two rims, as a multiple of the radius. Below about 1 the
        /// clusters merge back into the even scatter this replaces; far above it the level is
        /// a long walk between two villages.
        ///
        /// SIMULATED, NOT GUESSED. At 1.35 a 400 m level came out with 9-14 hamlets and only
        /// 31% of the road as open country, which is not clustering, it is a continuous
        /// village with gaps in it. At 2.6 it is 9-11 hamlets and 43-52% open — half the road
        /// is empty ground, which is what makes the other half read as a place.
        /// </summary>
        public const float GapRadii = 2.6f;

        /// <summary>
        /// Away from every settlement the field does not go empty — it goes SPARSE. A road
        /// with nothing at all beside it between villages reads as a corridor with the
        /// scenery switched off, which is a worse artefact than even scatter. This is the
        /// fraction of the old uniform density that survives out there.
        /// </summary>
        public const float BackgroundDensity = 0.22f;

        /// <summary>Centre-to-centre spacing that guarantees GapRadii of empty ground.</summary>
        public static float Spacing(float radius) => radius * (2f + GapRadii);

        /// <summary>Smallest hamlet worth being one, and the largest that still leaves a gap.</summary>
        public const float MinRadius = 14f;
        public const float MaxRadius = 30f;

        /// <summary>
        /// The radius that turns an authored spacing into that spacing exactly.
        ///
        /// THE GAP RATIO IS THE INVARIANT AND THE WORLD CHOOSES THE SCALE. Flooring the
        /// spacing at Spacing(fixedRadius) instead — the obvious way round — pinned seven of
        /// the eight worlds to the same 110 m, which silently threw away the per-world
        /// spacing they each author. Deriving the radius from the spacing honours the
        /// authored number exactly and still guarantees the empty ground, because the gap is
        /// expressed as a multiple of the radius rather than as metres.
        /// </summary>
        public static float RadiusFor(float spacing)
        {
            float r = spacing / (2f + GapRadii);
            if (r < MinRadius) return MinRadius;
            if (r > MaxRadius) return MaxRadius;
            return r;
        }

        /// <summary>
        /// The density multiplier at a point: 1 inside a settlement's core, BackgroundDensity
        /// out in open country. Takes the strongest settlement rather than the sum, so two
        /// that happen to land near each other do not stack into an impossible density.
        /// </summary>
        public static float DensityAt(Settlement[] settlements, int count, float x, float z)
        {
            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = settlements[i].Weight(x, z);
                if (w > best) best = w;
            }
            return BackgroundDensity + (1f - BackgroundDensity) * best;
        }

        /// <summary>
        /// What fraction of a span is open country — outside every settlement's reach.
        ///
        /// This exists to be asserted rather than to be called at runtime. "Clustered" with
        /// no empty ground between the clusters is just scatter with extra steps, and that
        /// failure is invisible in code and obvious on screen.
        /// </summary>
        public static float OpenGroundFraction(Settlement[] settlements, int count,
            float fromZ, float toZ, float x, int samples = 512)
        {
            if (samples < 2 || toZ <= fromZ) return 1f;
            int open = 0;
            for (int i = 0; i < samples; i++)
            {
                float z = fromZ + (toZ - fromZ) * i / (samples - 1f);
                float best = 0f;
                for (int s = 0; s < count; s++)
                {
                    float w = settlements[s].Weight(x, z);
                    if (w > best) best = w;
                }
                if (best <= 0.001f) open++;
            }
            return open / (float)samples;
        }
    }
}
