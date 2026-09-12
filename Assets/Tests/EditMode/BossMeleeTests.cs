using System.Collections.Generic;
using BattleRunner.Core.Boss;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The army's half of a boss fight. Two of these tests exist because the change they
    /// guard is a PRESENTATION change that touches the damage path, and a presentation change
    /// that quietly alters the balance is a balance change wearing a disguise.
    /// </summary>
    public sealed class BossMeleeTests
    {
        [Test]
        public void VolleysDeliverExactlyWhatTheContinuousGrindDelivered()
        {
            // THE LOAD-BEARING TEST. The army's damage moves from `dps * dt` every frame to a
            // discrete beat every SwingSeconds. Same total, rearranged in time. If this drifts
            // then every tuned boss HP value in the roster silently means something else.
            const float dps = 37.5f;
            for (float fight = 1f; fight <= 60f; fight += 0.37f)
            {
                int swings = BossMelee.SwingsBy(fight);
                float dealt = BossMelee.DamageForSwings(0, swings, dps);
                float continuous = dps * fight;
                // The only permitted difference is the beat currently in flight.
                Assert.LessOrEqual(dealt, continuous + 1e-3f,
                    $"at {fight:0.00}s the volleys have run AHEAD of the grind");
                Assert.GreaterOrEqual(dealt, continuous - dps * BossMelee.SwingSeconds - 1e-3f,
                    $"at {fight:0.00}s the volleys have fallen more than one beat behind");
            }
        }

        [Test]
        public void DamageAccumulatesTheSameWhateverTheFrameRate()
        {
            // The encounter applies the DIFFERENCE in completed swings each frame, so a 30 Hz
            // device and a 120 Hz one must arrive at the same total. Stepping the swing count
            // rather than the clock is what makes that true, and it is worth pinning because
            // the obvious implementation — a timer that resets — accumulates float error.
            const float dps = 12f;
            foreach (float step in new[] { 1f / 120f, 1f / 60f, 1f / 30f, 0.1f })
            {
                float t = 0f;
                int last = 0;
                float total = 0f;
                while (t < 30f)
                {
                    t += step;
                    int now = BossMelee.SwingsBy(t);
                    total += BossMelee.DamageForSwings(last, now, dps);
                    last = now;
                }
                Assert.AreEqual(BossMelee.DamageForSwings(0, BossMelee.SwingsBy(t), dps), total, 1e-2f,
                    $"a {1f / step:0} Hz frame rate arrived at a different total");
            }
        }

        [Test]
        public void TheLineIsNeverInUnison()
        {
            // The exact bug that made every instanced squad in the game look wrong: one phase
            // shared by every body reads as a marching band, not as men fighting. Here the
            // phases are spread deliberately, so it is worth proving they stay spread at every
            // count the fight can produce.
            for (int count = 2; count <= BossMelee.MaxFighters; count++)
            {
                for (float t = 0f; t < 6f; t += 0.41f)
                {
                    var seen = new HashSet<int>();
                    for (int i = 0; i < count; i++)
                        seen.Add((int)(BossMelee.Phase(i, t) * 40f));
                    // A generous bucket: the claim is "spread", not "all distinct".
                    Assert.GreaterOrEqual(seen.Count, System.Math.Min(count, 4),
                        $"{count} fighters at t={t:0.00} occupy only {seen.Count} phase buckets");
                }
            }
        }

        [Test]
        public void TheSpreadIsAVolleyAndNotAUniformShimmer()
        {
            // The failure mode at the OTHER end. Spread the phases across the whole cycle and
            // every part of it is occupied at every instant: statistically busy, visually
            // static. A volley means the line clumps — most of it doing the same thing at once,
            // with stragglers — so at any moment one stage should dominate.
            for (float t = 0f; t < BossMelee.CycleSeconds * 2f; t += 0.23f)
            {
                int closing = 0, trading = 0, back = 0;
                for (int i = 0; i < BossMelee.MaxFighters; i++)
                {
                    float p = BossMelee.Phase(i, t);
                    if (p < 0.30f) closing++;
                    else if (p < 0.75f) trading++;
                    else back++;
                }
                int most = System.Math.Max(closing, System.Math.Max(trading, back));
                Assert.GreaterOrEqual(most, BossMelee.MaxFighters / 3,
                    $"at t={t:0.00} the line is evenly smeared ({closing}/{trading}/{back}) "
                    + "and would read as a shimmer rather than a volley");
            }
        }

        [Test]
        public void AFighterNeverInvertsOrLeavesTheArena()
        {
            // Toward is 0 at the army's line and 1 at the boss. A pose that goes negative puts
            // a soldier behind his own army mid-swing; one past 1 puts him inside the boss.
            for (int i = 0; i < BossMelee.MaxFighters; i++)
            for (float t = 0f; t < 12f; t += 0.07f)
            {
                SkirmishPose p = BossMelee.Pose(i, BossMelee.MaxFighters, t, -1f);
                Assert.GreaterOrEqual(p.Toward, -1e-4f, $"fighter {i} at t={t:0.00} ran backwards");
                Assert.LessOrEqual(p.Toward, 1f, $"fighter {i} at t={t:0.00} ran through the boss");
                Assert.LessOrEqual(System.Math.Abs(p.Across), 2f, $"fighter {i} left the road");
                Assert.AreEqual(1f, p.Standing, 1e-6f, "nobody was killed in this case");
            }
        }

        [Test]
        public void AFighterTheBossKilledActuallyFallsAndStaysDown()
        {
            // "He smacks me back" needs a victim, not a number. A blow that lands has to take
            // somebody off the board, and they must not get up again inside the fight.
            SkirmishPose standing = BossMelee.Pose(3, 12, 4f, -1f);
            Assert.AreEqual(1f, standing.Standing, 1e-6f);

            SkirmishPose mid = BossMelee.Pose(3, 12, 4.25f, 4f);
            Assert.Greater(mid.Standing, 0f);
            Assert.Less(mid.Standing, 1f, "the fall is instant rather than a fall");
            Assert.Less(mid.Lean, standing.Lean, "a falling body did not tip over");

            for (float t = 4.5f; t < 30f; t += 0.5f)
                Assert.AreEqual(0f, BossMelee.Pose(3, 12, t, 4f).Standing, 1e-6f,
                    $"a dead fighter was back on his feet at t={t:0.0}");
        }

        [Test]
        public void TheDetachmentGrowsWithTheArmyButNeverDissolvesTheFormation()
        {
            Assert.AreEqual(0, BossMelee.Fighters(0));
            Assert.AreEqual(1, BossMelee.Fighters(1));

            long previous = 0;
            foreach (long force in new long[] { 1, 5, 20, 100, 500, 5000, 1000000 })
            {
                int n = BossMelee.Fighters(force);
                Assert.GreaterOrEqual(n, 1, $"an army of {force} sent nobody");
                Assert.LessOrEqual(n, BossMelee.MaxFighters, $"an army of {force} sent {n}");
                Assert.LessOrEqual(n, force, $"an army of {force} sent more men than it has");
                Assert.GreaterOrEqual(n, previous, "the detachment shrank as the army grew");
                previous = n;
            }
            // Sub-linear: a hundredfold army does not send a hundredfold detachment.
            Assert.Less(BossMelee.Fighters(10000), BossMelee.Fighters(100) * 4);
        }
    }
}
