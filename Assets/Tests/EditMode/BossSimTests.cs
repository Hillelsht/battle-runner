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
    }
}
