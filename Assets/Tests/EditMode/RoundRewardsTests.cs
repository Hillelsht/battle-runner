using BattleRunner.Core.Progression;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The reward curve, and specifically the one thing that could break silently: making
    /// bosses rare without touching rewards would have cut talent income roughly fourfold,
    /// and the tree was sized against per-round income.
    /// </summary>
    [TestFixture]
    public class RoundRewardsTests
    {
        private const int PerBoss = 3;   // BalanceSettings.StatPointsPerBossKill

        [Test]
        public void NoActPaysLessThanTheSameRoundsUsedTo()
        {
            // The regression this file exists for. Walk every act across a long game and
            // compare against the old curve for exactly those rounds.
            int round = 0;
            for (int act = 0; act < 60; act++)
            {
                RoundPlan first = RoundPlan.For(round);
                int now = RoundRewards.ActIncome(round, PerBoss);
                int before = RoundRewards.LegacyIncome(round, first.ActLength, PerBoss);
                Assert.GreaterOrEqual(now, before,
                    $"act {act} (rounds {round}..{round + first.ActLength - 1}) pays {now}, " +
                    $"where every-round-a-boss paid {before}");
                round += first.ActLength;
            }
        }

        [Test]
        public void ABossRoundIsTheWindfall()
        {
            // Three rounds of small change and then a payday. Four equal payments would read
            // as a salary and give nobody a reason to want the fight.
            for (int r = 0; r < 120; r++)
            {
                RoundPlan plan = RoundPlan.For(r);
                if (!plan.IsBossRound) continue;
                RoundPlan before = RoundPlan.For(r - 1);
                if (before.ActIndex != plan.ActIndex) continue;

                int bossPay = RoundRewards.PointsFor(plan, PerBoss);
                int normalPay = RoundRewards.PointsFor(before, PerBoss);
                Assert.Greater(bossPay, normalPay * 2, $"round {r}: {bossPay} vs {normalPay}");
            }
        }

        [Test]
        public void EveryRoundPaysSomething()
        {
            // The alternative considered was paying only on boss rounds. Three unrewarded
            // rounds in a row is a long time on a phone.
            for (int r = 0; r < 200; r++)
                Assert.GreaterOrEqual(RoundRewards.PointsFor(RoundPlan.For(r), PerBoss), 1,
                    $"round {r}");
        }

        [Test]
        public void ANormalRoundNeverPaysAsingleTokenPoint()
        {
            // perBossKill/2 rounds to 1 at the shipped value of 3, and one point against a
            // tree of 200 spends does not read as a reward at all.
            Assert.GreaterOrEqual(RoundRewards.PointsFor(RoundPlan.For(0), PerBoss), 2);
        }

        [Test]
        public void IncomeRisesWithDepth()
        {
            int early = RoundRewards.ActIncome(0, PerBoss);
            int mid = RoundRewards.ActIncome(30, PerBoss);
            int late = RoundRewards.ActIncome(120, PerBoss);
            Assert.Less(early, mid);
            Assert.Less(mid, late);
        }

        [Test]
        public void ABossRoundRollsAtFullLuckAndAThreatRoundDoesNot()
        {
            Assert.AreEqual(1f, RoundRewards.LootLuck(RoundPlan.For(1)), 1e-5f);
            Assert.AreEqual(RoundRewards.ThreatRoundLuck, RoundRewards.LootLuck(RoundPlan.For(0)), 1e-5f);
            Assert.Less(RoundRewards.ThreatRoundLuck, 1f);
            Assert.Greater(RoundRewards.ThreatRoundLuck, 0f);
        }

        [Test]
        public void OnlyAFightGuaranteesAnUpgrade()
        {
            Assert.IsTrue(RoundRewards.GuaranteesUpgrade(RoundPlan.For(1)));
            Assert.IsFalse(RoundRewards.GuaranteesUpgrade(RoundPlan.For(0)));
        }

        [Test]
        public void ADegenerateBalanceValueCannotZeroOutRewards()
        {
            foreach (int perBoss in new[] { 0, -5, 1 })
            {
                Assert.GreaterOrEqual(RoundRewards.PointsFor(RoundPlan.For(0), perBoss), 1);
                Assert.GreaterOrEqual(RoundRewards.PointsFor(RoundPlan.For(1), perBoss), 1);
            }
        }

        [Test]
        public void TheLegacyYardstickIsTheCurveItClaimsToBe()
        {
            // If this drifts, the regression test above silently stops meaning anything.
            Assert.AreEqual(PerBoss + 0 / 2, RoundRewards.LegacyIncome(0, 1, PerBoss));
            Assert.AreEqual(PerBoss + 10 / 2, RoundRewards.LegacyIncome(10, 1, PerBoss));
            Assert.AreEqual(0, RoundRewards.LegacyIncome(5, 0, PerBoss));
        }
    }
}
