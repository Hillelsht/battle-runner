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

        /// <summary>Stat damage at an act, with the shipped balance numbers.</summary>
        private static float StatDamage(int act) =>
            BossSim.StatDamageAtAct(act, baseDamage: 10f, damagePerPoint: 2f, pointsPerBoss: 3);

        private static float Hp(float baseHp, float pressure, int act) =>
            BossSim.BossHp(baseHp, pressure, act, StatDamage(act), StatDamage(0), SoftCap);

        [Test]
        public void BossHp_GrowsPerAct()
        {
            // DELIBERATELY REWRITTEN, and the old test is worth stating: it asserted
            // `BossHp(500, 0.25, 4) == 500 * 1.25^4` and it passed, which is exactly how the
            // difficulty treadmill survived. The quantity was right and the INDEX was wrong —
            // it was a round index, and a boss is fought once per act, so the health
            // multiplied about 3.0x between consecutive fights while the player grew 1.2-1.7x.
            // What the test pinned was the bug, faithfully.
            Assert.AreEqual(500f, Hp(500f, 0.06f, 0), 0.01f, "act 0 is the base");

            float a1 = Hp(500f, 0.06f, 1);
            float a4 = Hp(500f, 0.06f, 4);
            Assert.Greater(a1, 500f, "the boss must get harder");
            Assert.Greater(a4, a1);

            // And it grows in the bounded way the fix is for: four acts on, against a player
            // whose army and stat points have both grown, not an unbounded exponential.
            Assert.Less(a4 / 500f, 30f, "four acts must not multiply the health thirtyfold");
        }

        [Test]
        public void BossHpTracksTheArmyTheActExpectsRatherThanTheCalendar()
        {
            // The load-bearing property of the fix. Health is scaled by the SAME crowd factor
            // the player's damage is multiplied by, at the force the act expects — so when the
            // soft cap flattens the army, it flattens the boss too, automatically and for the
            // same reason. Before, nothing connected the two and the gap compounded forever.
            long capped = BossSim.ExpectedForceAtAct(10, SoftCap);
            Assert.AreEqual(SoftCap, capped, "the army is expected to be at the cap by act 10");
            Assert.AreEqual(capped, BossSim.ExpectedForceAtAct(14, SoftCap),
                "and to stay there, which is why the late curve must flatten");

            // Past the cap the only growth left is the stat points and the pressure screw.
            float a9 = Hp(1000f, 0.06f, 9);
            float a14 = Hp(1000f, 0.06f, 14);
            Assert.Less(a14 / a9, 3f,
                "five acts past the force cap must not multiply the health more than a few fold");
        }

        [Test]
        public void TimeToKill_SanityWindow_ForParPlayer()
        {
            // A par player at act 0 (base damage 10, modest force) kills the first boss in a
            // hybrid-casual window. The window is TIGHTENED from the old 5-60 s: the shipped
            // fight measured 9 s, the player called the game too easy, and 5 s was never a
            // sane lower bound for the beat a whole level builds to.
            float ttk = BossSim.TimeToKill(
                Hp(1150f, 0.042f, 0),
                BossSim.PlayerDps(Result(150, 10f), SoftCap));
            Assert.Greater(ttk, 10f, "the first boss dies before the player has learned it");
            Assert.Less(ttk, 45f);
        }

        [Test]
        public void TheWholeCampaignStaysInsideAPlayableWindow()
        {
            // THE TEST THAT WOULD HAVE CAUGHT THE TREADMILL. Walk sixteen acts of the shipped
            // roster against the army and stat points each act expects, and require every
            // fight to be winnable in a sane time. Under the old per-round curve the act-10
            // boss needed roughly two and a half HOURS, and nothing in the suite noticed.
            float[] bases = { 1150f, 1330f, 1450f, 1320f, 1390f, 1510f };
            float[] pressures = { 0.042f, 0.047f, 0.052f, 0.050f, 0.057f, 0.062f };

            for (int act = 0; act < 16; act++)
            {
                long force = BossSim.ExpectedForceAtAct(act, SoftCap);
                // The stat points the game has handed out, and nothing else: gear and talents
                // are the player's edge and are deliberately not modelled, so a real player
                // beats this.
                // WITH THE AFFIX THAT ACTUALLY APPLIES. The window is meaningless if it only
                // holds for the plain boss: Colossal multiplies health by 1.40, and an affix
                // rotation that lands one on the hardest act would push a curve that passes
                // here straight through the ceiling in play.
                BossAffix affix = BossAffixes.For(act);
                float hp = BossAffixes.BossHp(bases[act % 6], pressures[act % 6], act,
                    StatDamage(act), StatDamage(0), SoftCap, affix);
                float dps = BossSim.PlayerDps(Result(force, StatDamage(act)), SoftCap);
                float ttk = BossSim.TimeToKill(hp, dps) / SpellShare;

                Assert.Greater(ttk, 5f, $"act {act} boss dies in {ttk:0.0}s");
                Assert.Less(ttk, 60f,
                    $"act {act} boss ({affix}) needs {ttk:0.0}s from a player with nothing "
                    + "but stat points");
            }
        }

        /// <summary>
        /// The spell roughly halves a fight's length at par, so the sustained figure is
        /// divided by this to get a real time-to-kill. Named rather than inline because two
        /// tests use it and it is an assumption, not a fact.
        /// </summary>
        private const float SpellShare = 1.5f;

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
