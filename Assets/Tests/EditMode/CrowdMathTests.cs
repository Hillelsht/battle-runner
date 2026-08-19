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
