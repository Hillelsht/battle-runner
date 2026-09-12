using System.Collections.Generic;
using BattleRunner.Core.Boss;
using BattleRunner.Core.Progression;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// Champion affixes. The cheap answer to "six archetypes is six fights": the same
    /// creature, modified, announced by a prefix and an aura.
    /// </summary>
    [TestFixture]
    public class BossAffixTests
    {
        private const int Roster = 6;

        private static readonly BossArchetype[] Archetypes =
        {
            BossArchetype.Slam, BossArchetype.Volley, BossArchetype.Warded,
            BossArchetype.Drain, BossArchetype.Summoner, BossArchetype.Enrage
        };

        private static readonly BossAffix[] AllAffixes =
        {
            BossAffix.None, BossAffix.Frenzied, BossAffix.Armoured,
            BossAffix.Vampiric, BossAffix.Haunted, BossAffix.Colossal
        };

        // --- selection ---------------------------------------------------------

        [Test]
        public void TheFirstTwoActsTeachTheBaseFights()
        {
            // Modifying a creature the player has not learned yet is noise, not difficulty.
            Assert.AreEqual(BossAffix.None, BossAffixes.For(0));
            Assert.AreEqual(BossAffix.None, BossAffixes.For(1));
            Assert.AreNotEqual(BossAffix.None, BossAffixes.For(BossAffixes.FirstAffixAct));
        }

        [Test]
        public void EveryAffixIsReachable()
        {
            var seen = new HashSet<BossAffix>();
            for (int act = 0; act < 40; act++) seen.Add(BossAffixes.For(act));
            foreach (BossAffix affix in AllAffixes)
                Assert.IsTrue(seen.Contains(affix), $"{affix} is never selected");
        }

        [Test]
        public void SomeChampionActsAreDeliberatelyOrdinary()
        {
            // A roster where every boss is a champion has no contrast: the affix has to be
            // able to be absent for its presence to mean anything.
            int plain = 0;
            for (int act = BossAffixes.FirstAffixAct;
                 act < BossAffixes.FirstAffixAct + BossAffixes.CycleLength; act++)
                if (BossAffixes.For(act) == BossAffix.None) plain++;

            Assert.Greater(plain, 0, "no ordinary bosses at all past the teaching acts");
            Assert.Less(plain, BossAffixes.CycleLength / 2, "too many, and the affixes stop landing");
        }

        [Test]
        public void ThirtyDistinctChampionEncountersBeforeOneRepeats()
        {
            // The whole promise of affixes: six archetypes x five affixes = thirty fights for
            // a fraction of the cost of five new creatures. Measured, not assumed.
            //
            // CHAMPION pairings only, and that qualifier is real. None appears twice in the
            // cycle so it is not injective, and an ordinary boss recurs after eighteen acts —
            // which says nothing, because None is the absence of an affix. Every named affix
            // appears exactly once per turn, so those pair with the roster on lcm(7,6) = 42.
            var champions = new HashSet<(int, BossAffix)>();
            int period = BossAffixes.PairingPeriod(Roster);
            Assert.AreEqual(42, period);

            for (int i = 0; i < period; i++)
            {
                int act = BossAffixes.FirstAffixAct + i;
                BossAffix affix = BossAffixes.For(act);
                if (affix == BossAffix.None) continue;
                Assert.IsTrue(champions.Add((RoundPlan.BossSlot(act, Roster), affix)),
                    $"champion pairing repeated at act {act}, before the period was up");
            }

            Assert.AreEqual(30, champions.Count, "six bosses times five affixes");

            // And it does come round again immediately after, which is what makes 42 the
            // period rather than merely a lower bound.
            int wrapped = BossAffixes.FirstAffixAct + period;
            Assert.AreNotEqual(BossAffix.None, BossAffixes.For(wrapped));
            Assert.IsFalse(
                champions.Add((RoundPlan.BossSlot(wrapped, Roster), BossAffixes.For(wrapped))));
        }

        [Test]
        public void EveryNamedAffixAppearsExactlyOncePerTurnOfTheCycle()
        {
            // This is what makes the 42 hold. If a named affix were duplicated the way None
            // is, champion pairings would collide early too.
            var counts = new Dictionary<BossAffix, int>();
            for (int i = 0; i < BossAffixes.CycleLength; i++)
            {
                BossAffix affix = BossAffixes.For(BossAffixes.FirstAffixAct + i);
                counts.TryGetValue(affix, out int seen);
                counts[affix] = seen + 1;
            }

            foreach (KeyValuePair<BossAffix, int> pair in counts)
            {
                if (pair.Key == BossAffix.None) continue;
                Assert.AreEqual(1, pair.Value, $"{pair.Key} appears {pair.Value} times per cycle");
            }
        }

        [Test]
        public void SelectionSurvivesNonsenseActIndices()
        {
            Assert.AreEqual(BossAffix.None, BossAffixes.For(-9));
            Assert.DoesNotThrow(() => BossAffixes.For(int.MaxValue));
            Assert.AreEqual(BossAffixes.CycleLength, BossAffixes.PairingPeriod(0));
        }

        // --- naming ------------------------------------------------------------

        [Test]
        public void AnOrdinaryBossKeepsItsOwnName()
        {
            Assert.AreEqual("Bone Colossus", BossAffixes.Decorate(BossAffix.None, "Bone Colossus"));
            Assert.IsEmpty(BossAffixes.Prefix(BossAffix.None));
        }

        [Test]
        public void AChampionIsAnnouncedInItsName()
        {
            Assert.AreEqual("Frenzied Gore Hound",
                BossAffixes.Decorate(BossAffix.Frenzied, "Gore Hound"));
            Assert.AreEqual("Colossal Grave Warden",
                BossAffixes.Decorate(BossAffix.Colossal, "Grave Warden"));
        }

        [Test]
        public void NamingNeverProducesAStraySpaceOrANull()
        {
            foreach (BossAffix affix in AllAffixes)
            {
                Assert.IsNotNull(BossAffixes.Decorate(affix, null));
                Assert.IsFalse(BossAffixes.Decorate(affix, "X").StartsWith(" "));
                Assert.IsFalse(BossAffixes.Decorate(affix, "X").EndsWith(" "));
            }
        }

        [Test]
        public void EveryChampionHasAnAuraAndAnOrdinaryBossHasNone()
        {
            foreach (BossAffix affix in AllAffixes)
            {
                var tint = BossAffixes.Tint(affix);
                float sum = tint.R + tint.G + tint.B;
                if (affix == BossAffix.None) Assert.AreEqual(0f, sum, 1e-5f);
                else Assert.Greater(sum, 1f, $"{affix} has no readable aura");
            }
        }

        // --- modulation --------------------------------------------------------

        [Test]
        public void NoneIsTheIdentityForEveryModifier()
        {
            // The single most important property: an unaffixed boss must fight exactly as it
            // did before affixes existed.
            Assert.AreEqual(1f, BossAffixes.HpScale(BossAffix.None));
            Assert.AreEqual(1f, BossAffixes.IntervalScale(BossAffix.None));
            Assert.AreEqual(1f, BossAffixes.BlowScale(BossAffix.None));
            Assert.AreEqual(0f, BossAffixes.ExtraWardFraction(BossAffix.None));
            Assert.AreEqual(0, BossAffixes.ExtraAdds(BossAffix.None));
            Assert.AreEqual(0f, BossAffixes.LifeSteal(BossAffix.None));
            Assert.AreEqual(1f, BossAffixes.ScaleBoost(BossAffix.None));

            foreach (BossArchetype archetype in Archetypes)
            {
                Assert.AreEqual(BossSim.BlowFraction(archetype, 0.3f),
                    BossAffixes.BlowFraction(archetype, BossAffix.None, 0.3f), 1e-6f);
                Assert.AreEqual(BossSim.NextInterval(archetype, 4f, 0.5f),
                    BossAffixes.NextInterval(archetype, BossAffix.None, 4f, 0.5f), 1e-6f);
                Assert.AreEqual(BossSim.WardPool(archetype, 1000f),
                    BossAffixes.WardPool(archetype, BossAffix.None, 1000f), 1e-4f);
                Assert.AreEqual(BossSim.AddsPerCycle(archetype),
                    BossAffixes.AddsPerCycle(archetype, BossAffix.None));
            }
        }

        [Test]
        public void ABlowCanNeverBeComposedOutsideTheRangeApplyBossHitAccepts()
        {
            // ApplyBossHit throws outside [0,1]. A Colossal multiplier on an archetype
            // authored near the top of that range is exactly how a boss fight would end in an
            // exception instead of a death.
            foreach (BossArchetype archetype in Archetypes)
            foreach (BossAffix affix in AllAffixes)
            foreach (float printed in new[] { 0f, 0.3f, 0.85f, 1f })
            {
                float blow = BossAffixes.BlowFraction(archetype, affix, printed);
                Assert.GreaterOrEqual(blow, 0f, $"{archetype}/{affix}@{printed}");
                Assert.LessOrEqual(blow, 1f, $"{archetype}/{affix}@{printed}");
                Assert.DoesNotThrow(() => BossSim.ApplyBossHit(1000L, blow, 0f, false),
                    $"{archetype}/{affix}@{printed}");
            }
        }

        [Test]
        public void FrenziedSwingsFasterAndColossalSwingsSlower()
        {
            float plain = BossAffixes.NextInterval(BossArchetype.Slam, BossAffix.None, 4f, 1f);
            float fast = BossAffixes.NextInterval(BossArchetype.Slam, BossAffix.Frenzied, 4f, 1f);
            float slow = BossAffixes.NextInterval(BossArchetype.Slam, BossAffix.Colossal, 4f, 1f);

            Assert.Less(fast, plain);
            Assert.Greater(slow, plain);
        }

        [Test]
        public void ColossalTradesSpeedForWeight()
        {
            // Harder AND faster would not be a trade, it would just be worse.
            Assert.Greater(BossAffixes.HpScale(BossAffix.Colossal), 1f);
            Assert.Greater(BossAffixes.BlowScale(BossAffix.Colossal), 1f);
            Assert.Greater(BossAffixes.IntervalScale(BossAffix.Colossal), 1f);
            Assert.Greater(BossAffixes.ScaleBoost(BossAffix.Colossal), 1f, "it has to LOOK colossal");
        }

        [Test]
        public void AnIntervalCanNeverBeCompressedToNothing()
        {
            // Frenzied on an Enrage boss at death's door is the fastest the game can get.
            float fastest = BossAffixes.NextInterval(BossArchetype.Enrage, BossAffix.Frenzied, 3.6f, 0f);
            Assert.GreaterOrEqual(fastest, 0.35f);
            Assert.Greater(fastest, 0.9f, "still leaves time to see a telegraph and react");
        }

        [Test]
        public void ArmouredGivesAWardToBossesThatHaveNone()
        {
            Assert.AreEqual(0f, BossSim.WardPool(BossArchetype.Slam, 1000f), 1e-4f);
            Assert.Greater(BossAffixes.WardPool(BossArchetype.Slam, BossAffix.Armoured, 1000f), 0f);

            // And stacks on one that already has one, rather than replacing it.
            Assert.Greater(BossAffixes.WardPool(BossArchetype.Warded, BossAffix.Armoured, 1000f),
                BossSim.WardPool(BossArchetype.Warded, 1000f));
        }

        [Test]
        public void HauntedCallsAddsFromAnyArchetype()
        {
            Assert.AreEqual(0, BossSim.AddsPerCycle(BossArchetype.Slam));
            Assert.AreEqual(1, BossAffixes.AddsPerCycle(BossArchetype.Slam, BossAffix.Haunted));
            Assert.AreEqual(BossSim.AddsPerCycle(BossArchetype.Summoner) + 1,
                BossAffixes.AddsPerCycle(BossArchetype.Summoner, BossAffix.Haunted));
        }

        [Test]
        public void OnlyAVampiricBossHealsAndOnlyOnABlowThatLanded()
        {
            Assert.Greater(BossAffixes.HealOnHit(BossAffix.Vampiric, 1000f, blocked: false), 0f);
            Assert.AreEqual(0f, BossAffixes.HealOnHit(BossAffix.Vampiric, 1000f, blocked: true),
                "blocking is the counter-play — that is the whole point of the affix");
            Assert.AreEqual(0f, BossAffixes.HealOnHit(BossAffix.Frenzied, 1000f, blocked: false));
            Assert.AreEqual(0f, BossAffixes.HealOnHit(BossAffix.Vampiric, 0f, blocked: false));
        }

        [Test]
        public void AVampiricBossCannotOutHealTheFightItIsIn()
        {
            // It regains 4.5% of its maximum per landed blow. If that ever exceeded what a
            // player removes between blows the fight would be unwinnable rather than hard.
            float healPerBlow = BossAffixes.HealOnHit(BossAffix.Vampiric, 1000f, false);
            Assert.Less(healPerBlow, 1000f * 0.06f);
        }

        [Test]
        public void ColossalHealthScalesTheArchetypeCurveRatherThanReplacingIt()
        {
            // The signature moved from a ROUND index to an ACT index with the player's own
            // curve folded in (see BossSim.BossHp); what this test is about — that the affix
            // SCALES the curve rather than replacing it — is unchanged.
            const long softCap = 100_000L;
            float d10 = BossSim.StatDamageAtAct(10, 10f, 2f, 3);
            float d0 = BossSim.StatDamageAtAct(0, 10f, 2f, 3);
            float plain = BossSim.BossHp(500f, 0.06f, 10, d10, d0, softCap);
            float huge = BossAffixes.BossHp(500f, 0.06f, 10, d10, d0, softCap, BossAffix.Colossal);
            Assert.AreEqual(plain * BossAffixes.HpScale(BossAffix.Colossal), huge, 1e-2f);
            Assert.Greater(huge, plain);
        }
    }
}
