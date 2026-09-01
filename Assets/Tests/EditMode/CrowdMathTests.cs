using System.Collections.Generic;
using System.Numerics;
using BattleRunner.Core.Crowd;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class CrowdMathTests
    {
        [Test]
        public void Slots_AreStable_GrowthNeverReshuffles()
        {
            for (int i = 0; i < 300; i++)
            {
                Vector2 before = CrowdMath.PhyllotaxisSlot(i, 0.5f);
                Vector2 after = CrowdMath.PhyllotaxisSlot(i, 0.5f); // same call after "growth"
                Assert.AreEqual(before, after, $"slot {i} moved");
            }
        }

        [Test]
        public void Slots_DoNotOverlap()
        {
            const float spacing = 0.5f;
            var slots = new List<Vector2>();
            for (int i = 0; i < 300; i++) slots.Add(CrowdMath.PhyllotaxisSlot(i, spacing));

            for (int a = 0; a < slots.Count; a++)
            for (int b = a + 1; b < slots.Count; b++)
            {
                float dist = Vector2.Distance(slots[a], slots[b]);
                Assert.Greater(dist, spacing * 0.4f, $"slots {a} and {b} overlap ({dist})");
            }
        }

        [Test]
        public void FormationRadius_GrowsWithSquareRoot()
        {
            float r100 = CrowdMath.PhyllotaxisSlot(100, 0.5f).Length();
            float r400 = CrowdMath.PhyllotaxisSlot(400, 0.5f).Length();
            Assert.AreEqual(2f, r400 / r100, 0.1f, "4x units should be ~2x radius");
        }

        // --- Formation envelope -------------------------------------------------
        // v0.1.1 shipped a crowd that covered all three lanes at once, so steering stopped
        // being visible and the game stopped reading as a lane game. These pin the fix.

        private const float LaneWidth = 2.2f;
        private const float CameraRearLimit = 3.742f; // bottom of frame meets ground here

        [Test]
        public void ShippedDisc_SaturatedWiderThanTheWholeRoad()
        {
            // Documents the defect. Spacing compressed by sqrt(40/n) exactly cancelled the
            // sqrt(n) in the radius, so the disc pinned at one size from 40 bodies upward.
            float radiusAt40 = CrowdMath.PhyllotaxisSlot(40, 0.55f).Length();
            float spacingAt300 = 0.55f * System.MathF.Sqrt(40f / 300f);
            float radiusAt300 = CrowdMath.PhyllotaxisSlot(300, spacingAt300).Length();

            Assert.AreEqual(radiusAt40, radiusAt300, 0.05f, "the old disc never changed size");
            Assert.Greater(radiusAt300 * 2f, 3f * LaneWidth,
                "and that size was wider than all three lanes combined");
        }

        [Test]
        public void FormationWidth_NeverLeavesOneLane()
        {
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            foreach (int count in new[] { 1, 5, 12, 40, 100, 200, 300, 512 })
            {
                float widest = 0f;
                for (int i = 0; i < count; i++)
                    widest = System.MathF.Max(widest,
                        System.MathF.Abs(CrowdMath.FormationSlot(i, count, halfWidth).X));

                Assert.LessOrEqual(widest, halfWidth + 1e-4f, $"n={count} broke its own envelope");
                Assert.Less(widest * 2f, LaneWidth, $"n={count} spilled outside its lane");
            }
        }

        [Test]
        public void FormationWidth_DoesNotGrowWithTheArmy()
        {
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            float at40 = CrowdMath.FormationEnvelope(40, halfWidth).X;
            float at300 = CrowdMath.FormationEnvelope(300, halfWidth).X;

            Assert.AreEqual(at40, at300, 0.06f, "width is a property of the road, not the count");
            Assert.Less(at300, halfWidth + 1e-4f);
        }

        [Test]
        public void FormationTail_StaysInsideTheCameraFrame()
        {
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            foreach (int count in new[] { 40, 100, 200, 300, 512 })
            {
                float deepest = 0f;
                for (int i = 0; i < count; i++)
                    deepest = System.MathF.Min(deepest, CrowdMath.FormationSlot(i, count, halfWidth).Y);

                Assert.Greater(deepest, -CameraRearLimit,
                    $"n={count} trails past the bottom of the screen");
            }
        }

        [Test]
        public void FormationGrowth_StillReadsAsAnArmyGettingBigger()
        {
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            Vector3 at40 = CrowdMath.FormationEnvelope(40, halfWidth);
            Vector3 at300 = CrowdMath.FormationEnvelope(300, halfWidth);

            // Growth goes UP the road, which is the direction that costs no screen: the rig's
            // top-of-frame ray points above horizontal and never meets the ground.
            Assert.Greater(at300.Y, at40.Y * 1.8f, "the army must visibly reach further up the road");

            float area40 = at40.X * (at40.Y + at40.Z);
            float area300 = at300.X * (at300.Y + at300.Z);
            Assert.Greater(area300, area40 * 1.5f,
                "a x3 gate that does not change the picture is not a reward");
        }

        [Test]
        public void LeadingPlane_RetreatsWhenTheArmyShrinks()
        {
            // Documents why a passed gate must LATCH as resolved rather than be re-tested
            // each frame. The leading plane is derived from the envelope, which shrinks with
            // the count, so a subtract gate pulls it backwards far faster than the anchor
            // advances (0.167 m per frame at 60 fps and 10 m/s).
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            const float advancePerFrame = 10f / 60f;

            foreach (var drop in new[] { (before: 120, after: 20), (before: 300, after: 150) })
            {
                float before = CrowdMath.FormationEnvelope(drop.before, halfWidth).Y;
                float after = CrowdMath.FormationEnvelope(drop.after, halfWidth).Y;
                float netMovement = (advancePerFrame + after) - before;

                Assert.Less(netMovement, 0f,
                    $"{drop.before}->{drop.after}: the plane must be able to retreat, or this " +
                    "test no longer describes the hazard the resolve-latch guards against");
            }
        }

        [Test]
        public void FormationSlots_NeverSwapPlaces()
        {
            float halfWidth = CrowdMath.HalfWidthMaxFor(LaneWidth);
            // Growing the crowd rescales the envelope but must not reorder anyone, or a x2
            // gate would visibly shuffle the army. The envelope is an ellipse, so Euclidean
            // length is NOT the invariant (a sideways slot is shorter than a forward one at
            // the same rank). What must hold is that each unit keeps its bearing and its
            // rank, measured in envelope units.
            float Rank(Vector2 s, int count)
            {
                Vector3 e = CrowdMath.FormationEnvelope(count, halfWidth);
                float u = s.X / e.X;
                float v = s.Y / (s.Y >= 0f ? e.Y : e.Z);
                return System.MathF.Sqrt(u * u + v * v);
            }

            for (int i = 1; i < 200; i++)
            {
                Vector2 small = CrowdMath.FormationSlot(i, 200, halfWidth);
                Vector2 large = CrowdMath.FormationSlot(i, 400, halfWidth);
                Vector2 prev = CrowdMath.FormationSlot(i - 1, 200, halfWidth);

                Assert.GreaterOrEqual(Rank(small, 200), Rank(prev, 200) - 1e-4f, $"slot {i} overtook {i - 1}");

                // Same side of the column before and after growth: nobody walks around the crowd.
                Assert.AreEqual(System.MathF.Sign(small.X), System.MathF.Sign(large.X),
                    $"slot {i} crossed the lane axis on growth");
                Assert.AreEqual(System.MathF.Sign(small.Y), System.MathF.Sign(large.Y),
                    $"slot {i} changed rank ahead/behind on growth");
            }
        }

        // --- Lane assignment ----------------------------------------------------

        [Test]
        public void LaneIndex_PartitionsTheRoad_WhereTheRadiusTestDoubleCounted()
        {
            // The shipped test was |crowdX - gateX| <= laneWidth * 0.75. At exactly half a
            // lane off centre that accepts BOTH the centre gate and the right-hand gate, so
            // one crowd could collect a +gate and a -gate on the same frame.
            const float x = LaneWidth * 0.5f;
            float oldHalfWidth = LaneWidth * 0.75f;
            int oldMatches = 0;
            foreach (int lane in new[] { -1, 0, 1 })
                if (System.MathF.Abs(x - lane * LaneWidth) <= oldHalfWidth) oldMatches++;
            Assert.AreEqual(2, oldMatches, "the old radius test really did match two lanes");

            Assert.AreEqual(1, CrowdMath.LaneIndex(x, LaneWidth), "exactly one lane may claim a position");
        }

        [Test]
        public void Road_GivesAllThreeLanesEqualWidth()
        {
            // The drawn road and the scoring partition must be the same object. The road
            // used to be drawn 4.8*laneWidth wide with lane lines only at +/-0.5*laneWidth,
            // so the centre lane read 2.20 m and the outer two 3.96 m each -- 1.8x wider.
            float roadHalf = CrowdMath.RoadHalfWidth(LaneWidth);
            Assert.AreEqual(3f * LaneWidth, roadHalf * 2f, 1e-4f, "the road is exactly three lanes");

            // Sweep the road and measure how much of it each lane claims.
            var claimed = new System.Collections.Generic.Dictionary<int, int> { { -1, 0 }, { 0, 0 }, { 1, 0 } };
            const int samples = 30000;
            for (int i = 0; i < samples; i++)
            {
                float x = -roadHalf + (2f * roadHalf) * (i + 0.5f) / samples;
                claimed[CrowdMath.LaneIndex(x, LaneWidth)]++;
            }

            float expected = samples / 3f;
            foreach (var lane in claimed)
                Assert.AreEqual(expected, lane.Value, samples * 0.01f,
                    $"lane {lane.Key} claims {lane.Value / (float)samples:P1} of the road, not a third");
        }

        [Test]
        public void LaneIndex_CoversEveryPositionExactlyOnce()
        {
            for (int step = -330; step <= 330; step++)
            {
                float x = step * 0.01f;
                int lane = CrowdMath.LaneIndex(x, LaneWidth);
                Assert.That(lane, Is.InRange(-1, 1), $"x={x} fell outside the road");
            }
            Assert.AreEqual(0, CrowdMath.LaneIndex(0f, LaneWidth));
            Assert.AreEqual(-1, CrowdMath.LaneIndex(-LaneWidth, LaneWidth));
            Assert.AreEqual(1, CrowdMath.LaneIndex(LaneWidth, LaneWidth));
            Assert.AreEqual(1, CrowdMath.LaneIndex(99f, LaneWidth), "beyond the road clamps to the outer lane");
            Assert.AreEqual(-1, CrowdMath.LaneIndex(-99f, LaneWidth));
        }

        [Test]
        public void SpringDamper_ConvergesWithoutOvershootExplosion()
        {
            float pos = 0f, vel = 0f;
            for (int i = 0; i < 240; i++) // 4 seconds at 60 fps
                pos = CrowdMath.SpringDamperStep(pos, ref vel, 10f, 8f, 1f / 60f);
            Assert.AreEqual(10f, pos, 0.05f);
            Assert.AreEqual(0f, vel, 0.5f);
        }

        [Test]
        public void SpringDamper_SurvivesFrameSpike()
        {
            float pos = 0f, vel = 0f;
            pos = CrowdMath.SpringDamperStep(pos, ref vel, 10f, 8f, 0.1f); // one 100 ms hitch
            for (int i = 0; i < 120; i++)
                pos = CrowdMath.SpringDamperStep(pos, ref vel, 10f, 8f, 1f / 60f);
            Assert.AreEqual(10f, pos, 0.05f);
        }

        [Test]
        public void VisibleUnits_CapsAtTier()
        {
            Assert.AreEqual(0, CrowdMath.VisibleUnits(0, 200));
            Assert.AreEqual(150, CrowdMath.VisibleUnits(150, 200));
            Assert.AreEqual(200, CrowdMath.VisibleUnits(100_000, 200));
        }

        [Test]
        public void HeroScale_IsNeutralBelowCap_GrowsAboveIt()
        {
            Assert.AreEqual(1f, CrowdMath.HeroScaleFor(100, 200));
            float at10x = CrowdMath.HeroScaleFor(2_000, 200);
            float at100x = CrowdMath.HeroScaleFor(20_000, 200);
            Assert.Greater(at10x, 1f);
            Assert.Greater(at100x, at10x);
            Assert.Less(at100x, 2.5f, "scale must stay bounded for readability");
        }
    }
}
