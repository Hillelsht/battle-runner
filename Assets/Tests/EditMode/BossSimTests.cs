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
        private static RunResult Result(double force, float damage, double surplus = 1.0) => new RunResult
        {
            FinalForceCount = force,
            StartingForceCount = force / (surplus <= 0.0 ? 1.0 : surplus),
            HeroStats = StatSheet.Resolve(
                new Dictionary<string, float> { [StatIds.Damage] = damage, [StatIds.Health] = 100f },
                null),
            ReachedBoss = true
        };

        [Test]
        public void MoreForce_MeansMoreDps_WithDiminishingReturns()
        {
            float dps10 = BossSim.PlayerDps(Result(10, 10f));
            float dps1000 = BossSim.PlayerDps(Result(1_000, 10f));
            float dps100000 = BossSim.PlayerDps(Result(100_000, 10f));

            Assert.Greater(dps1000, dps10);
            Assert.Greater(dps100000, dps1000);
            Assert.Less(dps100000 / dps1000, dps1000 / dps10,
                "force contribution must diminish so gear stays the long-term lever");
        }

        [Test]
        public void DamageStat_ScalesDpsLinearly()
        {
            float low = BossSim.PlayerDps(Result(100, 10f));
            float high = BossSim.PlayerDps(Result(100, 20f));
            Assert.AreEqual(2f, high / low, 1e-3f);
        }

        [Test]
        public void SurplusBonus_IncreasesDps()
        {
            float plain = BossSim.PlayerDps(Result(100, 10f));
            float bonused = BossSim.PlayerDps(Result(100, 10f, surplus: 4.0));
            Assert.Greater(bonused, plain);
        }

        [Test]
        public void ZeroDamageHero_NeverKills()
        {
            Assert.AreEqual(float.PositiveInfinity,
                BossSim.TimeToKill(1000f, BossSim.PlayerDps(Result(100, 0f))));
        }

        /// <summary>Stat damage at an act, with the shipped balance numbers.</summary>
        private static float StatDamage(int act) =>
            BossSim.StatDamageAtAct(act, baseDamage: 10f, damagePerPoint: 2f, pointsPerBoss: 3);

        /// <summary>The army a par player is assumed to reach the act-N boss with.</summary>
        private static double Army(int act) => StandingArmy.Seed * System.Math.Pow(2.2, act);

        private static float Hp(float baseHp, float pressure, int act) =>
            BossSim.BossHp(baseHp, pressure, act, StatDamage(act), StatDamage(0), Army(act));

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
        public void TheFightLastsTheSameWhateverArmyWalksIntoIt()
        {
            // THIS REPLACES BossHpTracksTheArmyTheActExpectsRatherThanTheCalendar, which
            // pinned the boss to a MODEL of the army (60 * 2.2^act, capped). With the army
            // continuous no such model can be fair to everyone: simulated over sixty rounds
            // a player at lane-choice 0.80 ends on 1.1e17 men and one at 0.90 on 2.2e19, and
            // a boss priced for a ladder between them is a formality for one and a wall for
            // the other.
            //
            // So health is scaled by the SAME CrowdFactor the player's damage is multiplied
            // by, at the force that actually arrives — and the two cancel exactly. That is
            // the property, and it is much stronger than the one it replaces: the fight is a
            // test of the FIGHT, and the run decides whether you arrive, not whether you win.
            float damage = StatDamage(3);
            float hpSmall = BossSim.BossHp(1000f, 0.06f, 3, StatDamage(3), StatDamage(0), 400.0);
            float hpHuge = BossSim.BossHp(1000f, 0.06f, 3, StatDamage(3), StatDamage(0), 4e14);
            float ttkSmall = BossSim.TimeToKill(hpSmall, BossSim.PlayerDps(Result(400.0, damage)));
            float ttkHuge = BossSim.TimeToKill(hpHuge, BossSim.PlayerDps(Result(4e14, damage)));

            Assert.Greater(hpHuge, hpSmall * 100f, "a huge army must face a proportionally huge boss");
            Assert.AreEqual(ttkSmall, ttkHuge, ttkSmall * 0.02f,
                "a trillion-fold army must not change how long the fight takes");
        }

        [Test]
        public void GearIsStillThePlayersEdge()
        {
            // The corollary, and the reason the cancellation above is not a flat game: the
            // boss is priced against the army and the STAT POINTS the game hands out, and
            // against nothing else. Everything a player earns beyond that — gear, talents —
            // is theirs to keep, and shows up as a shorter fight.
            float bare = BossSim.TimeToKill(Hp(1000f, 0.06f, 3),
                BossSim.PlayerDps(Result(Army(3), StatDamage(3))));
            float geared = BossSim.TimeToKill(Hp(1000f, 0.06f, 3),
                BossSim.PlayerDps(Result(Army(3), StatDamage(3) * 1.6f)));
            Assert.Less(geared, bare * 0.7f, "sixty per cent more Might must be felt");
        }

        [Test]
        public void TimeToKill_SanityWindow_ForParPlayer()
        {
            // A par player at act 0 (base damage 10, modest force) kills the first boss in a
            // hybrid-casual window. The window is TIGHTENED from the old 5-60 s: the shipped
            // fight measured 9 s, the player called the game too easy, and 5 s was never a
            // sane lower bound for the beat a whole level builds to.
            // The roster was re-based when the boss stopped being priced against a modelled
            // army: with CrowdFactor cancelling on both sides, a base of 1150 landed the
            // first fight at 29 s and the act-8 Colossal at 76 s. Bases are 0.60x and the
            // pressures eased with them.
            float ttk = BossSim.TimeToKill(
                BossSim.BossHp(690f, 0.036f, 0, StatDamage(0), StatDamage(0), 150.0),
                BossSim.PlayerDps(Result(150, StatDamage(0)))) / SpellShare;
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
            float[] bases = { 690f, 798f, 870f, 792f, 834f, 906f };
            float[] pressures = { 0.036f, 0.040f, 0.044f, 0.042f, 0.047f, 0.051f };

            for (int act = 0; act < 16; act++)
            {
                double force = Army(act);
                // The stat points the game has handed out, and nothing else: gear and talents
                // are the player's edge and are deliberately not modelled, so a real player
                // beats this.
                // WITH THE AFFIX THAT ACTUALLY APPLIES. The window is meaningless if it only
                // holds for the plain boss: Colossal multiplies health by 1.40, and an affix
                // rotation that lands one on the hardest act would push a curve that passes
                // here straight through the ceiling in play.
                BossAffix affix = BossAffixes.For(act);
                float hp = BossAffixes.BossHp(bases[act % 6], pressures[act % 6], act,
                    StatDamage(act), StatDamage(0), force, affix);
                float dps = BossSim.PlayerDps(Result(force, StatDamage(act)));
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
            double unmitigated = BossSim.ApplyBossHit(1000, 0.4f, 0f, false);
            double mitigated = BossSim.ApplyBossHit(1000, 0.4f, 100f, false);
            Assert.AreEqual(600.0, unmitigated, 1e-4);
            Assert.AreEqual(800.0, mitigated, 1e-4, "100 health halves losses");
        }

        [Test]
        public void BossHit_TakesItsExactShare_AtAnyScale()
        {
            // WHAT THIS USED TO GUARD IS GONE, and that is the honest way to record it. The
            // old assertion was that a clean 40% of 1000 removes exactly 400 whole units —
            // a regression test for a Ceiling that turned float 0.4f (really 0.40000000596)
            // into 401 removed, with the error growing as the army did. There is no Ceiling
            // any more because there are no whole units, so the bug it guarded cannot recur;
            // what is left worth pinning is that the share is exact at every scale.
            Assert.AreEqual(600.0, BossSim.ApplyBossHit(1000, 0.4f, 0f, false), 1e-4);
            Assert.AreEqual(60_000.0, BossSim.ApplyBossHit(100_000, 0.4f, 0f, false), 1e-2);
            Assert.AreEqual(500.0, BossSim.ApplyBossHit(1000, 0.5f, 0f, false), 1e-9);
            Assert.AreEqual(900.0, BossSim.ApplyBossHit(1000, 0.1f, 0f, false), 1e-4);
        }

        [Test]
        public void BossHit_TakesAFractionOfASmallArmyToo()
        {
            // 7 * 0.4 = 2.8, so 4.2 is left. It used to be 4 exactly, rounded up so that a
            // blow always cost at least one man; nothing needs protecting from rounding now.
            Assert.AreEqual(4.2, BossSim.ApplyBossHit(7, 0.4f, 0f, false), 1e-4);
        }

        [Test]
        public void BossHit_ZeroFractionRemovesNothing()
        {
            Assert.AreEqual(1000.0, BossSim.ApplyBossHit(1000, 0f, 0f, false), 1e-9);
        }

        [Test]
        public void BossHit_NeverGoesNegative()
        {
            Assert.AreEqual(0.0, BossSim.ApplyBossHit(1, 1f, 0f, false), 1e-9);
            Assert.AreEqual(0.0, BossSim.ApplyBossHit(0, 0.5f, 0f, false), 1e-9);
        }

        [Test]
        public void TheMaulNeverTakesTheLastManHoweverLongTheFightRuns()
        {
            // THE PROPERTY THAT MATTERS. The boss now swats at the army on every volley beat,
            // roughly twice a second, for the whole fight. If that attrition could reach zero
            // then a player who answers every telegraph correctly still loses by standing
            // there — a fight with no answer, which is exactly what the shield exists to
            // prevent. The maul colours a fight; the blows decide it.
            double force = 500;
            for (int beat = 0; beat < 4000; beat++)
            {
                force -= BossSim.MaulBite(force, 0.0026f * 0.55f, 0f);
                Assert.Greater(force, 0.0, $"attrition alone wiped the army on beat {beat}");
            }
            Assert.Less(force, 500.0, "four thousand beats of attrition should still bite");
        }

        [Test]
        public void TheMaulAndARealBlowAreTheSameArithmetic()
        {
            // DELIBERATELY REWRITTEN. It used to be TheMaulRoundsDownWhereARealBlowRoundsUp,
            // pinning the one place the two differed: a blow ceilinged so it always cost at
            // least a man, the maul floored so a 0.14% bite could not quietly wipe a crowd
            // of five. Neither rule exists now — a continuous army has nothing to round — so
            // what is left is the property those two rounding rules were BOTH protecting:
            // Health mitigates identically in both, and the maul never takes the last man.
            const double force = 100000;
            const float fraction = 0.02f;
            foreach (float health in new[] { 0f, 25f, 100f, 400f })
            {
                double mauled = BossSim.MaulBite(force, fraction, health);
                double blown = force - BossSim.ApplyBossHit(force, fraction, health, false);
                Assert.AreEqual(mauled, blown, force * 1e-9,
                    $"at Health {health} the maul took {mauled} where a blow took {blown}");
            }
            Assert.Greater(BossSim.MaulBite(force, fraction, 0f),
                BossSim.MaulBite(force, fraction, 200f), "Health did not mitigate the maul");

            // And the clamp that IS a design decision rather than an arithmetic one.
            Assert.Less(BossSim.MaulBite(10.0, 1f, 0f), 10.0, "the last man is never taken");
        }

        [Test]
        public void HealthMitigatesTheMaulExactlyAsItMitigatesABlow()
        {
            // One stat, one meaning. If Health worked differently against the constant melee
            // than against a telegraphed blow, the talent tree's most-taken node would mean
            // two different things depending on which half of the fight was looked at.
            const double force = 100000;
            const float fraction = 0.02f;
            foreach (float health in new[] { 0f, 25f, 100f, 400f })
            {
                double mauled = BossSim.MaulBite(force, fraction, health);
                double blown = force - BossSim.ApplyBossHit(force, fraction, health, false);
                // The same maths. The two used to differ by a unit because one rounded up
                // and the other down; with a continuous army neither rounds at all.
                Assert.AreEqual(mauled, blown, force * 1e-9,
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
            double force = 1000;
            int beats = (int)(20f / 0.55f);
            for (int i = 0; i < beats; i++)
                force -= BossSim.MaulBite(force, perSecond * 0.55f, 0f);
            float lost = 1f - (float)(force / 1000.0);
            Assert.Greater(lost, 0.02f, "the maul is invisible and might as well not exist");
            Assert.Less(lost, 0.12f, "the maul is deciding fights the player cannot answer");
        }

    }
}
