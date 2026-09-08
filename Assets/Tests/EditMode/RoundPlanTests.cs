using System.Collections.Generic;
using BattleRunner.Core.Progression;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The act structure. This is the file that decides how often a player meets a boss, how
    /// long a round runs and how long it takes before a world and a boss come round together
    /// again — all of which used to be "every round, always the same, immediately".
    /// </summary>
    [TestFixture]
    public class RoundPlanTests
    {
        private const int Roster = 6;   // the shipped boss roster

        [Test]
        public void TheFirstActIsShortSoTheFirstSessionMeetsABoss()
        {
            // The tutorial teaches the shield on a boss telegraph. A long opening act would
            // leave a new player minutes with a verb they have never been shown.
            Assert.IsFalse(RoundPlan.For(0).IsBossRound);
            Assert.IsTrue(RoundPlan.For(1).IsBossRound, "the second round ends in a fight");
            Assert.AreEqual(RoundPlan.OpeningActLength, RoundPlan.For(0).ActLength);
        }

        [Test]
        public void ActsSettleIntoThreeToFiveRounds()
        {
            for (int round = 0; round < 400; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                Assert.GreaterOrEqual(plan.ActLength, 2, $"round {round}");
                Assert.LessOrEqual(plan.ActLength, 5, $"round {round}");
                if (plan.ActIndex > 0)
                    Assert.GreaterOrEqual(plan.ActLength, 3,
                        $"only the OPENING act may be short — a 2 inside the repeating cycle " +
                        $"would bring two-round boss gaps back around forever (round {round})");
            }
        }

        [Test]
        public void RoundsWalkThroughAnActAndThenStartTheNextOne()
        {
            int expectedAct = 0;
            int expectedInAct = 0;
            for (int round = 0; round < 200; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                Assert.AreEqual(expectedAct, plan.ActIndex, $"round {round} act");
                Assert.AreEqual(expectedInAct, plan.RoundInAct, $"round {round} position");

                expectedInAct++;
                if (expectedInAct >= plan.ActLength)
                {
                    expectedInAct = 0;
                    expectedAct++;
                }
            }
        }

        [Test]
        public void ExactlyOneRoundPerActIsABossRound()
        {
            var perAct = new Dictionary<int, int>();
            for (int round = 0; round < 400; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                if (!plan.IsBossRound) continue;
                perAct.TryGetValue(plan.ActIndex, out int seen);
                perAct[plan.ActIndex] = seen + 1;
            }

            foreach (KeyValuePair<int, int> pair in perAct)
                Assert.AreEqual(1, pair.Value, $"act {pair.Key} has more than one boss round");
            Assert.Greater(perAct.Count, 90, "400 rounds should cover a great many acts");
        }

        [Test]
        public void ABossIsFoughtEveryThreeToFiveRoundsOnceUnderway()
        {
            int last = -1;
            var gaps = new List<int>();
            for (int round = 0; round < 400; round++)
            {
                if (!RoundPlan.For(round).IsBossRound) continue;
                if (last >= 0) gaps.Add(round - last);
                last = round;
            }

            foreach (int gap in gaps)
            {
                Assert.GreaterOrEqual(gap, 3);
                Assert.LessOrEqual(gap, 5);
            }
            // And they are not all the same gap, or the rhythm would be a metronome.
            Assert.Greater(new HashSet<int>(gaps).Count, 1);
        }

        [Test]
        public void NegativeAndZeroRoundsAreTheFirstRound()
        {
            Assert.AreEqual(0, RoundPlan.For(-5).RoundIndex);
            Assert.AreEqual(0, RoundPlan.For(-5).ActIndex);
            Assert.AreEqual(0, RoundPlan.For(0).RoundIndex);
        }

        [Test]
        public void TheClosedFormSurvivesRoundsNobodyWillEverReach()
        {
            // The whole point of deriving the act from arithmetic rather than from saved
            // state is that round 100,000 costs the same as round 3.
            RoundPlan deep = RoundPlan.For(100_000);
            Assert.AreEqual(100_000, deep.RoundIndex);
            Assert.GreaterOrEqual(deep.ActIndex, 20_000);
            Assert.Less(deep.RoundInAct, deep.ActLength);
        }

        // --- worlds and bosses ------------------------------------------------

        [Test]
        public void ActsWalkTheWorldsForwardAndTheBossesBackward()
        {
            Assert.AreEqual(0, RoundPlan.For(0).ThemeSlot);
            var bosses = new List<int>();
            for (int act = 0; act < Roster; act++) bosses.Add(RoundPlan.BossSlot(act, Roster));
            CollectionAssert.AreEqual(new[] { 0, 5, 4, 3, 2, 1 }, bosses);
        }

        [Test]
        public void EveryBossInTheRosterIsReached()
        {
            var seen = new HashSet<int>();
            for (int act = 0; act < Roster; act++) seen.Add(RoundPlan.BossSlot(act, Roster));
            Assert.AreEqual(Roster, seen.Count, "stepping backward visits every slot");
        }

        [Test]
        public void AWorldAndABossPairingLastsTwentyFourActs()
        {
            // The bug this replaces: six levels and six bosses both indexed by the same
            // number, so level three drew the Grave Warden every single time. That is a
            // pairing period of ONE.
            var pairs = new HashSet<(int, int)>();
            int period = RoundPlan.PairingPeriod(Roster);
            for (int act = 0; act < period; act++)
                Assert.IsTrue(
                    pairs.Add((RoundPlan.ThemeSlotFor(act), RoundPlan.BossSlot(act, Roster))),
                    $"pairing repeated at act {act}, before the period was up");

            Assert.AreEqual(24, period);
            Assert.AreEqual(period, pairs.Count);
        }

        [Test]
        public void APairingPeriodIsSaneForAnyRosterSize()
        {
            Assert.AreEqual(RoundPlan.ThemeCount, RoundPlan.PairingPeriod(0), "no roster, no pairing");
            Assert.AreEqual(8, RoundPlan.PairingPeriod(1));
            Assert.AreEqual(40, RoundPlan.PairingPeriod(10));
            Assert.AreEqual(0, RoundPlan.BossSlot(7, 0), "an empty roster cannot be indexed");
        }

        // --- length -----------------------------------------------------------

        [Test]
        public void RoundsGrowWithTheActAndAgainForABoss()
        {
            RoundPlan first = RoundPlan.For(0);
            Assert.AreEqual(RoundPlan.BaseChunks, first.ChunkCount);

            RoundPlan firstBoss = RoundPlan.For(1);
            Assert.AreEqual(RoundPlan.BaseChunks + RoundPlan.BossRoundChunkBonus, firstBoss.ChunkCount);
        }

        [Test]
        public void RoundLengthRisesButIsBounded()
        {
            int max = 0;
            for (int round = 0; round < 500; round++)
            {
                int chunks = RoundPlan.For(round).ChunkCount;
                Assert.GreaterOrEqual(chunks, RoundPlan.BaseChunks);
                if (chunks > max) max = chunks;
            }

            // Bounded, or a round at hour ten would be a marathon nobody finishes.
            Assert.AreEqual(RoundPlan.BaseChunks + RoundPlan.GrowthActs + RoundPlan.BossRoundChunkBonus, max);
            Assert.LessOrEqual(max, 22);
        }

        [Test]
        public void ABossRoundIsAlwaysTheLongestRoadInItsAct()
        {
            for (int round = 0; round < 200; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                if (!plan.IsBossRound) continue;
                RoundPlan before = RoundPlan.For(round - 1);
                if (before.ActIndex != plan.ActIndex) continue;
                Assert.Greater(plan.ChunkCount, before.ChunkCount, $"round {round}");
            }
        }

        [Test]
        public void DifficultyTracksTheRoundSoExistingScalingIsUnchanged()
        {
            // Gate values and pack costs are authored against the round index today. Renaming
            // it must not silently re-tune the whole game.
            for (int round = 0; round < 50; round++)
                Assert.AreEqual(round, RoundPlan.For(round).Difficulty);
        }
    }

    /// <summary>
    /// The per-round drift inside a world. Eight worlds over endless rounds is still a loop
    /// unless the rounds inside an act differ, and these are the numbers that make them.
    /// </summary>
    [TestFixture]
    public class ThemeVariantTests
    {
        [Test]
        public void TheIdentityChangesNothing()
        {
            ThemeVariant none = ThemeVariant.None;
            Assert.AreEqual(0f, none.HueShift);
            Assert.AreEqual(1f, none.FogScale);
            Assert.AreEqual(1f, none.StarScale);
            Assert.AreEqual(0f, none.LightAzimuth);
            Assert.AreEqual(1f, none.PropDensity);
            Assert.AreEqual(0f, none.WetnessShift);
        }

        [Test]
        public void ARoundLooksTheSameEveryTimeItIsPlayed()
        {
            for (int round = 0; round < 40; round++)
            {
                ThemeVariant a = ThemeVariant.For(RoundPlan.For(round));
                ThemeVariant b = ThemeVariant.For(RoundPlan.For(round));
                Assert.AreEqual(a.HueShift, b.HueShift, $"round {round}");
                Assert.AreEqual(a.FogScale, b.FogScale, $"round {round}");
                Assert.AreEqual(a.LightAzimuth, b.LightAzimuth, $"round {round}");
            }
        }

        [Test]
        public void TheHashIsPinnedSoAMonoDivergenceFailsCi()
        {
            // Exact values, not a range. Unsigned integer maths is bit-identical across
            // runtimes and this asserts it stays that way — the same class of bug that made
            // Talents.Executes pass under .NET and fail under Mono.
            ThemeVariant v = ThemeVariant.For(RoundPlan.For(7));
            Assert.AreEqual(0.02947079f, v.HueShift, 1e-6f);
            Assert.AreEqual(0.94995729f, v.FogScale, 1e-6f);
            Assert.AreEqual(-14.9011416f, v.LightAzimuth, 1e-5f);
            Assert.AreEqual(1.15820272f, v.PropDensity, 1e-6f);
        }

        [Test]
        public void AWorldIsIntroducedBeforeItIsBent()
        {
            // Act 3 runs five rounds (14 rounds in, index 9..13). The first should sit far
            // closer to the authored world than the last, or the player never sees what the
            // world actually looks like.
            float opening = Deviation(ThemeVariant.For(RoundPlan.For(9)));
            float closing = Deviation(ThemeVariant.For(RoundPlan.For(13)));
            Assert.AreEqual(0, RoundPlan.For(9).RoundInAct, "test is anchored to the act's first round");
            Assert.AreEqual(4, RoundPlan.For(13).RoundInAct, "and to its last");
            Assert.Less(opening, closing);
        }

        [Test]
        public void EveryValueStaysInsideItsStatedRange()
        {
            for (int round = 0; round < 500; round++)
            {
                ThemeVariant v = ThemeVariant.For(RoundPlan.For(round));
                Assert.LessOrEqual(System.Math.Abs(v.HueShift), 0.055f, $"round {round} hue");
                Assert.GreaterOrEqual(v.FogScale, 0.80f, $"round {round} fog");
                Assert.LessOrEqual(v.FogScale, 1.20f, $"round {round} fog");
                Assert.GreaterOrEqual(v.StarScale, 0.55f, $"round {round} stars");
                Assert.LessOrEqual(v.StarScale, 1.45f, $"round {round} stars");
                Assert.LessOrEqual(System.Math.Abs(v.LightAzimuth), 26f, $"round {round} azimuth");
                Assert.GreaterOrEqual(v.PropDensity, 0.70f, $"round {round} props");
                Assert.LessOrEqual(v.PropDensity, 1.30f, $"round {round} props");
                Assert.LessOrEqual(System.Math.Abs(v.WetnessShift), 0.18f, $"round {round} wetness");
            }
        }

        [Test]
        public void ConsecutiveRoundsDoNotLookAlike()
        {
            // The whole point. If the hash collapsed, every round in an act would drift the
            // same way and this would be eight worlds instead of dozens of rounds.
            int samey = 0;
            for (int round = 2; round < 200; round++)
            {
                ThemeVariant a = ThemeVariant.For(RoundPlan.For(round - 1));
                ThemeVariant b = ThemeVariant.For(RoundPlan.For(round));
                if (System.Math.Abs(a.LightAzimuth - b.LightAzimuth) < 2f) samey++;
            }
            Assert.Less(samey, 30, "too many neighbouring rounds share a light angle");
        }

        private static float Deviation(ThemeVariant v) =>
            System.Math.Abs(v.HueShift) / 0.055f
            + System.Math.Abs(v.FogScale - 1f) / 0.20f
            + System.Math.Abs(v.LightAzimuth) / 26f;
    }
}
