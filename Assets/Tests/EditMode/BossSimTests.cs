using System.Collections.Generic;
using BattleRunner.Core.Boss;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class BossSimTests
    {
        private const long SoftCap = 100_000;

        private static RunResult Result(long force, float damage, long overflow = 0) => new RunResult
        {
            FinalForceCount = force,
            OverflowAccumulated = overflow,
            HeroStats = StatSheet.Resolve(
                new Dictionary<string, float> { [StatIds.Damage] = damage, [StatIds.Health] = 100f },
                null),
            ReachedBoss = true
        };

        [Test]
        public void MoreForce_MeansMoreDps_WithDiminishingReturns()
        {
            float dps10 = BossSim.PlayerDps(Result(10, 10f), SoftCap);
            float dps1000 = BossSim.PlayerDps(Result(1_000, 10f), SoftCap);
            float dps100000 = BossSim.PlayerDps(Result(100_000, 10f), SoftCap);

            Assert.Greater(dps1000, dps10);
            Assert.Greater(dps100000, dps1000);
            Assert.Less(dps100000 / dps1000, dps1000 / dps10,
                "force contribution must diminish so gear stays the long-term lever");
        }

        [Test]
        public void DamageStat_ScalesDpsLinearly()
        {
            float low = BossSim.PlayerDps(Result(100, 10f), SoftCap);
            float high = BossSim.PlayerDps(Result(100, 20f), SoftCap);
            Assert.AreEqual(2f, high / low, 1e-3f);
        }

        [Test]
        public void OverflowBonus_IncreasesDps()
        {
            float plain = BossSim.PlayerDps(Result(100, 10f), SoftCap);
            float bonused = BossSim.PlayerDps(Result(100, 10f, overflow: SoftCap), SoftCap);
            Assert.Greater(bonused, plain);
        }

        [Test]
        public void ZeroDamageHero_NeverKills()
        {
            Assert.AreEqual(float.PositiveInfinity,
                BossSim.TimeToKill(1000f, BossSim.PlayerDps(Result(100, 0f), SoftCap)));
        }

        [Test]
        public void BossHp_GrowsPerLevel()
        {
            float l0 = BossSim.BossHp(500f, 0.25f, 0);
            float l4 = BossSim.BossHp(500f, 0.25f, 4);
            Assert.AreEqual(500f, l0);
            Assert.AreEqual(500f * 1.25f * 1.25f * 1.25f * 1.25f, l4, 0.5f);
        }

        [Test]
        public void TimeToKill_SanityWindow_ForParPlayer()
        {
            // A level-0 par player (base damage 10, modest force) should kill the
            // level-0 boss in a hybrid-casual window: 5-60 seconds.
            float ttk = BossSim.TimeToKill(
                BossSim.BossHp(500f, 0.25f, 0),
                BossSim.PlayerDps(Result(150, 10f), SoftCap));
            Assert.Greater(ttk, 5f);
            Assert.Less(ttk, 60f);
        }

        [Test]
        public void Shield_NegatesBossHit()
        {
            Assert.AreEqual(500, BossSim.ApplyBossHit(500, 0.4f, 0f, shieldActive: true));
        }

        [Test]
        public void BossHit_RemovesFraction_MitigatedByHealth()
        {
            long unmitigated = BossSim.ApplyBossHit(1000, 0.4f, 0f, false);
            long mitigated = BossSim.ApplyBossHit(1000, 0.4f, 100f, false);
            Assert.AreEqual(600, unmitigated);
            Assert.AreEqual(800, mitigated, "100 health halves losses");
        }

        [Test]
        public void BossHit_CleanPercentageRemovesWholeUnits_AtAnyScale()
        {
            // Regression: float 0.4f is 0.40000000596, so Ceiling used to remove one
            // extra unit — and the error grew with force. Caught only under Unity's
            // Mono runtime, where the intermediate keeps the excess.
            Assert.AreEqual(600, BossSim.ApplyBossHit(1000, 0.4f, 0f, false));
            Assert.AreEqual(60_000, BossSim.ApplyBossHit(100_000, 0.4f, 0f, false));
            Assert.AreEqual(500, BossSim.ApplyBossHit(1000, 0.5f, 0f, false));
            Assert.AreEqual(900, BossSim.ApplyBossHit(1000, 0.1f, 0f, false));
        }

        [Test]
        public void BossHit_FractionalLossStillRoundsUp()
        {
            // 7 * 0.4 = 2.8 -> 3 removed. The epsilon must not swallow real fractions.
            Assert.AreEqual(4, BossSim.ApplyBossHit(7, 0.4f, 0f, false));
        }

        [Test]
        public void BossHit_ZeroFractionRemovesNothing()
        {
            Assert.AreEqual(1000, BossSim.ApplyBossHit(1000, 0f, 0f, false));
        }

        [Test]
        public void BossHit_NeverGoesNegative()
        {
            Assert.AreEqual(0, BossSim.ApplyBossHit(1, 1f, 0f, false));
            Assert.AreEqual(0, BossSim.ApplyBossHit(0, 0.5f, 0f, false));
        }

        [Test]
        public void TheMaulNeverTakesTheLastManHoweverLongTheFightRuns()
        {
            // THE PROPERTY THAT MATTERS. The boss now swats at the army on every volley beat,
            // roughly twice a second, for the whole fight. If that attrition could reach zero
            // then a player who answers every telegraph correctly still loses by standing
            // there — a fight with no answer, which is exactly what the shield exists to
            // prevent. The maul colours a fight; the blows decide it.
            long force = 500;
            for (int beat = 0; beat < 4000; beat++)
            {
                long bite = BossSim.MaulBite(force, 0.0026f * 0.55f, 0f);
                force -= bite;
                Assert.Greater(force, 0L, $"attrition alone wiped the army on beat {beat}");
            }
            Assert.AreEqual(1L, force, "it should converge to the last man and stop");
        }

        [Test]
        public void TheMaulRoundsDownWhereARealBlowRoundsUp()
        {
            // ApplyBossHit ceilings, because a telegraphed blow that lands must always cost
            // something. The maul floors, because it fires ~80 times in a fight: ceiling a
            // 0.14% bite would cost an army of five exactly what it costs an army of five
            // thousand and would quietly wipe every small crowd in the game.
            Assert.AreEqual(0L, BossSim.MaulBite(0, 0.5f, 0f));
            Assert.AreEqual(0L, BossSim.MaulBite(100, 0f, 0f));
            Assert.AreEqual(0L, BossSim.MaulBite(1, 0.9f, 0f), "the last man is never taken");
            // A floor of one, so a big army still visibly bleeds rather than rounding to zero.
            Assert.AreEqual(1L, BossSim.MaulBite(1000, 0.0014f, 0f));
            Assert.AreEqual(14L, BossSim.MaulBite(10000, 0.0014f, 0f));
        }

        [Test]
        public void HealthMitigatesTheMaulExactlyAsItMitigatesABlow()
        {
            // One stat, one meaning. If Health worked differently against the constant melee
            // than against a telegraphed blow, the talent tree's most-taken node would mean
            // two different things depending on which half of the fight was looked at.
            const long force = 100000;
            const float fraction = 0.02f;
            foreach (float health in new[] { 0f, 25f, 100f, 400f })
            {
                long mauled = BossSim.MaulBite(force, fraction, health);
                long blown = force - BossSim.ApplyBossHit(force, fraction, health, false);
                // Same maths, differing only by the rounding rule above.
                Assert.LessOrEqual(System.Math.Abs(mauled - blown), 1L,
                    $"at Health {health} the maul took {mauled} where a blow took {blown}");
            }
            Assert.Greater(BossSim.MaulBite(force, fraction, 0f),
                BossSim.MaulBite(force, fraction, 200f), "Health did not mitigate the maul");
        }

        [Test]
        public void TheMaulIsSmallEnoughToColourAFightAndNotDecideOne()
        {
            // The tuning claim, written down so it cannot drift silently: over a twenty-second
            // fight the maul should cost single-digit percent of an unarmoured army. If it
            // ever grows past that it has stopped being attrition and become a second attack
            // the player cannot answer.
            const float perSecond = 0.0026f;
            long force = 1000;
            int beats = (int)(20f / 0.55f);
            for (int i = 0; i < beats; i++)
                force -= BossSim.MaulBite(force, perSecond * 0.55f, 0f);
            float lost = 1f - force / 1000f;
            Assert.Greater(lost, 0.02f, "the maul is invisible and might as well not exist");
            Assert.Less(lost, 0.12f, "the maul is deciding fights the player cannot answer");
        }

    }
}
