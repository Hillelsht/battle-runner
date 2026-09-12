using BattleRunner.Core.Boss;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The six archetype patterns.
    ///
    /// The first thing every case here checks is that SLAM is untouched: it is the fight
    /// the game already had and the one the tutorial teaches, so every pattern added around
    /// it must be the identity for it. A boss roster that quietly re-tunes the first boss is
    /// a regression wearing a feature's clothes.
    /// </summary>
    [TestFixture]
    public class BossRosterTests
    {
        private static readonly BossArchetype[] All =
        {
            BossArchetype.Slam, BossArchetype.Volley, BossArchetype.Warded,
            BossArchetype.Drain, BossArchetype.Summoner, BossArchetype.Enrage
        };

        [Test]
        public void OnlyTheVolleySwingsMoreThanOnce()
        {
            foreach (BossArchetype a in All)
                Assert.AreEqual(a == BossArchetype.Volley ? BossSim.VolleyBlows : 1,
                    BossSim.BlowsPerCycle(a), a.ToString());
        }

        [Test]
        public void AVolleyIsWorseIgnoredAndBetterBlocked()
        {
            const float printed = 0.30f;
            float slam = BossSim.BlowFraction(BossArchetype.Slam, printed);
            float volley = BossSim.BlowFraction(BossArchetype.Volley, printed);

            Assert.AreEqual(printed, slam, 1e-6f, "slam is unchanged");
            Assert.Less(volley, slam, "each blow lands softer");
            Assert.Greater(volley * BossSim.VolleyBlows, slam,
                "but ignoring the whole volley costs more than eating a slam");
        }

        [Test]
        public void AVolleyGapFitsInsideAShieldWindowAndNeverCollapses()
        {
            Assert.AreEqual(0.242f, BossSim.VolleyGapSeconds(1.10f), 1e-4f);
            Assert.AreEqual(0.30f, BossSim.VolleyGapSeconds(9f), 1e-4f, "clamped so a volley is not a machine gun");
            Assert.AreEqual(0.08f, BossSim.VolleyGapSeconds(0f), 1e-4f, "and never zero");
        }

        [Test]
        public void OnlyTheEnrageChangesItsOwnClock()
        {
            foreach (BossArchetype a in All)
            {
                if (a == BossArchetype.Enrage) continue;
                Assert.AreEqual(4f, BossSim.NextInterval(a, 4f, 0.1f), 1e-6f, a.ToString());
            }
        }

        [Test]
        public void AnEnrageSpeedsUpMonotonicallyAsItDies()
        {
            float full = BossSim.NextInterval(BossArchetype.Enrage, 4f, 1f);
            float half = BossSim.NextInterval(BossArchetype.Enrage, 4f, 0.5f);
            float dying = BossSim.NextInterval(BossArchetype.Enrage, 4f, 0f);

            Assert.AreEqual(4f, full, 1e-5f, "at full health it is an ordinary boss");
            Assert.Less(half, full);
            Assert.Less(dying, half);
            Assert.AreEqual(4f * 0.45f, dying, 1e-5f);
            // Out-of-range HP must not invert the curve — a boss healed above max or
            // reported below zero still has to produce a sane interval.
            Assert.AreEqual(full, BossSim.NextInterval(BossArchetype.Enrage, 4f, 4f), 1e-5f);
            Assert.AreEqual(dying, BossSim.NextInterval(BossArchetype.Enrage, 4f, -3f), 1e-5f);
        }

        [Test]
        public void OnlyTheDrainBleeds()
        {
            foreach (BossArchetype a in All)
            {
                if (a == BossArchetype.Drain) continue;
                Assert.AreEqual(0L, BossSim.DrainTick(a, 1000L, 0.3f, 1f, false), a.ToString());
            }
        }

        [Test]
        public void ARaisedShieldStopsTheDrainCompletely()
        {
            Assert.AreEqual(0L, BossSim.DrainTick(BossArchetype.Drain, 1000L, 0.3f, 1f, true));
            Assert.Greater(BossSim.DrainTick(BossArchetype.Drain, 1000L, 0.3f, 1f, false), 0L);
        }

        [Test]
        public void TheDrainScalesWithTheCrowdAndWithTime()
        {
            double oneSecond = BossSim.DrainTick(BossArchetype.Drain, 1000.0, 0.3f, 1f, false);
            double half = BossSim.DrainTick(BossArchetype.Drain, 1000.0, 0.3f, 0.5f, false);
            double small = BossSim.DrainTick(BossArchetype.Drain, 100.0, 0.3f, 1f, false);

            // Tolerances are relative, not absolute: hitFraction is a float, so 0.3f is
            // 0.300000011920929 and the exact answer is 60.0000023. Demanding 1e-6 of a
            // number derived from a float is demanding more precision than the input has.
            Assert.AreEqual(60.0, oneSecond, 1e-4, "20% of the printed hit, per second, of current force");
            Assert.AreEqual(30.0, half, 1e-4);
            Assert.AreEqual(6.0, small, 1e-5);
            Assert.AreEqual(0.0, BossSim.DrainTick(BossArchetype.Drain, 0.0, 0.3f, 1f, false));
        }

        [Test]
        public void ASingleFrameOfDrainCostsExactlyItsShare()
        {
            // At 60 fps a 20-man crowd loses 0.02 of a man per frame. That used to be
            // rounded UP to a whole unit, because an integer army showed a drain of zero as
            // nothing happening at all — a boss that reads as broken rather than as gentle.
            // A continuous army needs no such lie: the frame costs exactly what it costs,
            // and sixty of them still come to 1.2.
            Assert.AreEqual(0.02, BossSim.DrainTick(BossArchetype.Drain, 20.0, 0.3f, 1f / 60f, false), 1e-6);
            Assert.Greater(BossSim.DrainTick(BossArchetype.Drain, 20.0, 0.3f, 1f / 60f, false), 0.0);
        }

        [Test]
        public void OnlyTheWardedBossCarriesAWard()
        {
            foreach (BossArchetype a in All)
                Assert.AreEqual(a == BossArchetype.Warded ? 1000f * BossSim.WardFraction : 0f,
                    BossSim.WardPool(a, 1000f), 1e-4f, a.ToString());
        }

        [Test]
        public void WithNoWardDamagePassesStraightThrough()
        {
            Assert.AreEqual(120f, BossSim.ThroughWard(120f, 0f, 3f, out float left), 1e-4f);
            Assert.AreEqual(0f, left);
        }

        [Test]
        public void AWardSoaksEverythingUntilItBreaks()
        {
            float toHealth = BossSim.ThroughWard(10f, 220f, 1f, out float left);
            Assert.AreEqual(0f, toHealth, 1e-4f, "the grind wears it down, it does not break it");
            Assert.AreEqual(210f, left, 1e-4f);
        }

        [Test]
        public void ASpellStripsAWardSeveralTimesFaster()
        {
            BossSim.ThroughWard(10f, 220f, 3f, out float spellLeft);
            BossSim.ThroughWard(10f, 220f, 1f, out float grindLeft);
            Assert.Less(spellLeft, grindLeft);
            Assert.AreEqual(190f, spellLeft, 1e-4f);
        }

        [Test]
        public void OverkillCrossesTheBrokenWardAtTheOrdinaryRate()
        {
            // 100 damage at 3x is 300 against a 150 ward. The ward cost 50 of the raw
            // damage to break, so 50 reaches the health — NOT 150, which is what letting
            // the multiplier apply to the remainder as well would have given.
            float toHealth = BossSim.ThroughWard(100f, 150f, 3f, out float left);
            Assert.AreEqual(0f, left);
            Assert.AreEqual(50f, toHealth, 1e-4f);
        }

        [Test]
        public void AWardExactlyEmptiedLeavesNothingBehind()
        {
            float toHealth = BossSim.ThroughWard(50f, 150f, 3f, out float left);
            Assert.AreEqual(0f, left);
            Assert.AreEqual(0f, toHealth, 1e-4f);
        }

        [Test]
        public void OnlyTheSummonerSummons()
        {
            foreach (BossArchetype a in All)
                Assert.AreEqual(a == BossArchetype.Summoner ? BossSim.SummonCount : 0,
                    BossSim.AddsPerCycle(a), a.ToString());
        }

        [Test]
        public void AddsCostNothingWhenThereAreNone()
        {
            Assert.AreEqual(0L, BossSim.AddBite(0, 500L));
            Assert.AreEqual(0L, BossSim.AddBite(3, 0L));
        }

        [Test]
        public void AddsCostAFixedShareOfTheCrowdAtEveryScale()
        {
            // The "at least one each" floor is GONE, deliberately: it existed because an
            // integer army rounded a 6% share of five men down to zero, and a continuous
            // army has nothing to round. The proportionality it was protecting is the real
            // property and is now exact at every scale.
            Assert.AreEqual(60.0, BossSim.AddBite(2, 500.0), 1e-9, "6% of the crowd per add");
            Assert.AreEqual(0.6, BossSim.AddBite(2, 5.0), 1e-9, "still exactly 6% each at five men");
            Assert.AreEqual(0.96, BossSim.AddBite(4, 4.0), 1e-9, "four adds at 6% each");
            Assert.AreEqual(20.0, BossSim.AddBite(40, 20.0), 1e-9,
                "capped at wiping the crowd, never past it");
        }

        [Test]
        public void EveryArchetypeStillDrivesTheOriginalHitMath()
        {
            // BlowFraction feeds ApplyBossHit, which throws outside [0,1]. A volley
            // multiplies the printed fraction, so this is the guard that a future boss
            // authored at a high HitFraction cannot crash the encounter.
            foreach (BossArchetype a in All)
            {
                float f = BossSim.BlowFraction(a, 1f);
                Assert.GreaterOrEqual(f, 0f, a.ToString());
                Assert.LessOrEqual(f, 1f, a.ToString());
            }
        }
    }
}
