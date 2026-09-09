using System;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// Whether the scenery actually clusters, and whether the roadside is still a hole.
    ///
    /// "Clustered" with no empty ground between the clusters is even scatter with an extra
    /// step, and it fails invisibly: the code looks like it groups things, and the frame
    /// looks exactly as it did. So the property being asserted is the NEGATIVE SPACE, which
    /// is the thing a village actually has and an evenly-dressed field does not.
    /// </summary>
    [TestFixture]
    public class SettlementTests
    {
        private const float LevelLength = 400f;
        private const float Radius = 24f;

        /// <summary>The layout SceneryField builds, without the engine.</summary>
        private static int Plan(Settlement[] into, float fromZ, float toZ, float spacing,
            float radius, int firstSide = -1)
        {
            int n = 0;
            float z = fromZ + spacing * 0.5f;
            int side = firstSide;
            while (z < toZ && n < into.Length)
            {
                into[n++] = new Settlement(z, 26f, side, radius, 0f);
                z += spacing;
                side = -side;
            }
            return n;
        }

        [Test]
        public void SpacingLeavesRealOpenGroundBetweenHamlets()
        {
            // The gap is the point. Two rims a metre apart is one long village, which is the
            // even scatter this replaces with more steps.
            float spacing = Settlements.Spacing(Radius);
            float gap = spacing - 2f * Radius;
            Assert.Greater(gap, Radius,
                $"settlements {spacing:F1} m apart with a {Radius} m radius leave only "
                + $"{gap:F1} m of open ground");
            Assert.AreEqual(Radius * Settlements.GapRadii, gap, 1e-3f);
        }

        [Test]
        public void MostOfALevelIsOpenCountryRatherThanVillage()
        {
            // Sampled down the middle of the settlement band, the majority of a level must be
            // outside every settlement's reach. A village you are always inside is a corridor.
            var plan = new Settlement[32];
            int n = Plan(plan, 0f, LevelLength, Settlements.Spacing(Radius), Radius);
            Assert.Greater(n, 2, "a 400 m level should hold several settlements");

            float open = Settlements.OpenGroundFraction(plan, n, 0f, LevelLength, -26f);
            // Simulated against the real per-world spacings, SceneryField lands at 40-47%.
            // Below about a third and the road is a continuous village with gaps in it; above
            // about three quarters and the hamlets are too rare to be the thing you notice.
            Assert.Greater(open, 0.33f, $"only {open:P0} of the level is open country");
            Assert.Less(open, 0.80f, $"{open:P0} of the level is empty — the hamlets vanished");
        }

        [Test]
        public void AWorldsAuthoredSpacingIsHonouredExactlyRatherThanFlooredAway()
        {
            // The obvious way round — floor the spacing at Spacing(someFixedRadius) — pinned
            // seven of the eight worlds to the same 110 m and silently discarded the spacing
            // each of them authors. Deriving the radius FROM the spacing keeps both the
            // authored number and the guaranteed gap, because the gap is a multiple of the
            // radius rather than a number of metres.
            foreach (WorldTheme t in WorldThemes.All)
            {
                float authored = t.Scenery.LandmarkSpacing;
                float radius = Settlements.RadiusFor(authored);
                Assert.AreEqual(authored, Settlements.Spacing(radius), 0.01f,
                    $"{t.DisplayName} authors {authored} m and would get "
                    + $"{Settlements.Spacing(radius)} m");
                Assert.GreaterOrEqual(radius, Settlements.MinRadius, t.DisplayName);
                Assert.LessOrEqual(radius, Settlements.MaxRadius, t.DisplayName);
            }

            // Two worlds that author different spacings must not end up the same size.
            Assert.AreNotEqual(Settlements.RadiusFor(82f), Settlements.RadiusFor(120f));

            // And the clamps must still hold for a world that authors something absurd.
            Assert.AreEqual(Settlements.MinRadius, Settlements.RadiusFor(1f), 1e-4f);
            Assert.AreEqual(Settlements.MaxRadius, Settlements.RadiusFor(10000f), 1e-4f);
        }

        [Test]
        public void DensityIsHighAtTheCentreAndFallsToTheBackgroundOutside()
        {
            var plan = new[] { new Settlement(100f, 26f, -1, Radius, 0f) };

            float centre = Settlements.DensityAt(plan, 1, -26f, 100f);
            float rim = Settlements.DensityAt(plan, 1, -26f, 100f + Radius * 0.98f);
            float away = Settlements.DensityAt(plan, 1, -26f, 100f + Radius * 3f);

            Assert.AreEqual(1f, centre, 1e-3f, "the middle of a hamlet is not dense");
            Assert.Less(rim, 0.45f, "the density has not fallen off by the rim");
            Assert.AreEqual(Settlements.BackgroundDensity, away, 1e-3f);
            Assert.Greater(away, 0f,
                "open country is empty, not sparse — a road with nothing beside it between "
                + "villages reads as the scenery being switched off");
        }

        [Test]
        public void TheFalloffIsAreaWeightedRatherThanLinear()
        {
            // A linear falloff puts most of its AREA in the outer ring, because area grows
            // with the square of the radius, so the cluster comes out as a ring of buildings
            // around an empty middle. Halfway out by radius must still be well over half.
            var s = new Settlement(0f, 0f, 1, Radius, 0f);
            Assert.Greater(s.Weight(Radius * 0.5f, 0f), 0.6f,
                "halfway out the density has already collapsed; the hamlet will be a ring");
            Assert.AreEqual(0f, s.Weight(Radius * 1.01f, 0f), 1e-6f);
            Assert.AreEqual(1f, s.Weight(0f, 0f), 1e-6f);
        }

        [Test]
        public void ASettlementOnOneSideDoesNotDressTheOther()
        {
            // Side is part of the position, not a label. A hamlet on the left verge weighting
            // the right one would put half of every village across the road from itself.
            var left = new Settlement(50f, 26f, -1, Radius, 0f);
            Assert.Greater(left.Weight(-26f, 50f), 0.99f);
            Assert.AreEqual(0f, left.Weight(26f, 50f), 1e-6f);
        }

        [Test]
        public void ADegenerateSettlementDoesNotDivideByZero()
        {
            // A world authoring a zero radius must produce a point with no reach, not a NaN
            // that propagates into every matrix in the round.
            var s = new Settlement(0f, 0f, 1, 0f, 0f);
            float w = s.Weight(1f, 1f);
            Assert.IsFalse(float.IsNaN(w));
            Assert.AreEqual(0f, w, 1e-6f);
            // And a round that planned no settlements at all still dresses its verge, at the
            // background density — not at zero, which would be a level with the scenery off.
            Assert.AreEqual(Settlements.BackgroundDensity,
                Settlements.DensityAt(Array.Empty<Settlement>(), 0, 0f, 0f), 1e-6f);
        }

        [Test]
        public void NoWorldPaintsItsRoadsideIntoAHole()
        {
            // 45-70% of a game frame sat below 12% luminance against 36-43% for the photoreal
            // references, and the verge is a large share of that: the procedural props are
            // painted PropStone outright and the imported verge is dragged 60-78% toward it.
            // Three worlds authored a PropStone below 0.17 luma. The road already carries
            // this exact invariant; the roadside did not.
            foreach (WorldTheme t in WorldThemes.All)
                Assert.GreaterOrEqual(t.VergeStone.Luma, WorldTheme.MinVergeLuma - 1e-4f,
                    $"{t.DisplayName}: the verge is painted {t.VergeStone}");
        }

        [Test]
        public void TheDarknessFloorKeepsTheWorldsHueRatherThanGreyingIt()
        {
            // Lifting toward white would raise the luminance and desaturate at the same time,
            // which turns a world's signature colour into grey — the opposite of the point.
            // Blood Marsh's prop stone is red; it must still be red after the floor.
            foreach (WorldTheme t in WorldThemes.All)
            {
                Rgb before = t.PropStone;
                Rgb after = t.VergeStone;
                if (before.Luma >= WorldTheme.MinVergeLuma)
                {
                    Assert.AreEqual(before.R, after.R, 1e-6f, t.DisplayName);
                    continue;
                }
                // Same ratios, so the same hue and the same saturation.
                float k = after.Luma / before.Luma;
                Assert.AreEqual(before.R * k, after.R, 1e-4f, t.DisplayName);
                Assert.AreEqual(before.G * k, after.G, 1e-4f, t.DisplayName);
                Assert.AreEqual(before.B * k, after.B, 1e-4f, t.DisplayName);
            }
        }

        [Test]
        public void EveryWorldGradesAndGlossesDifferentlyFromEveryOther()
        {
            // Terrain._Gloss existed and nothing ever wrote it, and WhiteBalance was not in
            // the post stack at all, so ice and dry ash caught the key light identically and
            // no world had a white point of its own. Both are free — one uniform and one LUT.
            var seenGloss = new System.Collections.Generic.HashSet<float>();
            var seenTemp = new System.Collections.Generic.HashSet<float>();
            foreach (WorldTheme t in WorldThemes.All)
            {
                Assert.Greater(t.GroundGloss, 1f, t.DisplayName);
                Assert.LessOrEqual(t.GroundGloss, 64f, $"{t.DisplayName}: a mirror, not ground");
                Assert.GreaterOrEqual(t.GradeTemperature, -100f, t.DisplayName);
                Assert.LessOrEqual(t.GradeTemperature, 100f, t.DisplayName);
                seenGloss.Add(t.GroundGloss);
                seenTemp.Add(t.GradeTemperature);
            }
            Assert.GreaterOrEqual(seenGloss.Count, 7, "the land shines the same in every world");
            Assert.GreaterOrEqual(seenTemp.Count, 7, "every world has the same white point");

            // And the derivation must actually separate the extremes it exists for.
            Assert.Less(Find("The Frozen Reach").GradeTemperature,
                Find("Ember Fields").GradeTemperature,
                "a frozen world is graded warmer than a burning one");
            Assert.Greater(Find("The Frozen Reach").GroundGloss,
                Find("The Bone Wastes").GroundGloss,
                "ice is duller than dry bone sand");
        }

        private static WorldTheme Find(string displayName)
        {
            foreach (WorldTheme t in WorldThemes.All)
                if (t.DisplayName == displayName) return t;
            throw new AssertionException($"no world called {displayName}");
        }
    }
}
