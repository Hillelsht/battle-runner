using System;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The champion that blocks a lane. Everything here is about the one property that makes
    /// it a fight rather than a toll: the player must be able to SEE a swing coming and answer
    /// it, while the road is still moving and gates are still arriving.
    /// </summary>
    [TestFixture]
    public class EliteTests
    {
        [Test]
        public void TheWarningArrivesBeforeTheBlowAndLastsLongEnoughToUse()
        {
            // The fairness property, and the reason ReadableWindow is a named constant rather
            // than an inline comparison. A telegraph that begins when the blow begins is not a
            // telegraph.
            Elite e = Elite.Begin(200.0, 1);
            Assert.IsFalse(e.IsTelegraphing(), "it must not be winding up on the frame it appears");

            // Walk up to the first swing and record when the warning starts.
            float warnedAt = -1f;
            for (float t = 0f; t < Elite.WindUpSeconds; t += 1f / 120f)
            {
                if (e.IsTelegraphing() && warnedAt < 0f) warnedAt = t;
                e.Grind(1f / 120f);
            }

            Assert.Greater(warnedAt, 0f, "the swing arrived with no warning at all");
            Assert.AreEqual(Elite.WindUpSeconds - Elite.ReadableWindow, warnedAt, 0.05f,
                "the warning must start exactly ReadableWindow before the blow");
        }

        [Test]
        public void TheTelegraphRunsFromZeroToOneAsTheBlowApproaches()
        {
            Elite e = Elite.Begin(200.0, 1);
            e.Grind(Elite.WindUpSeconds - Elite.ReadableWindow);
            Assert.AreEqual(0f, e.TelegraphPhase(), 0.05f, "it should be at the start of the warning");

            e.Grind(Elite.ReadableWindow * 0.5f);
            Assert.AreEqual(0.5f, e.TelegraphPhase(), 0.08f);

            e.Grind(Elite.ReadableWindow * 0.5f);
            Assert.GreaterOrEqual(e.TelegraphPhase(), 0.9f, "it should be at full heat as the blow lands");
        }

        [Test]
        public void ASwingIsTakenExactlyOncePerSwing()
        {
            // Closed form from Elapsed rather than a countdown that is reset, so a long frame
            // cannot silently skip a swing or fire one twice.
            Elite e = Elite.Begin(200.0, 1);
            int taken = 0;
            for (int i = 0; i < 400; i++)
            {
                e.Grind(1f / 60f);
                if (e.TakeSwingIfDue()) taken++;
                if (!e.Alive) break;
            }
            Assert.AreEqual(e.SwingsThrown, taken, "every swing must be consumed exactly once");
        }

        [Test]
        public void ALongFrameDoesNotSkipASwing()
        {
            Elite e = Elite.Begin(200.0, 1);
            e.Grind(Elite.WindUpSeconds + 0.01f);          // one enormous hitch
            Assert.IsTrue(e.TakeSwingIfDue(), "the swing that fell inside the hitch was lost");
        }

        [Test]
        public void ABlockedOrDodgedSwingCostsExactlyNothing()
        {
            // Both answers pay the same, and that is deliberate: an answer the player found
            // should feel like an answer, not like a discount. A barricade simply never
            // arrives here with dodged true — there is nowhere to dodge to.
            Assert.AreEqual(0.0, Elite.SwingCost(1000.0, 2, 3, blocked: true, dodged: false));
            Assert.AreEqual(0.0, Elite.SwingCost(1000.0, 2, 3, blocked: false, dodged: true));
            Assert.Greater(Elite.SwingCost(1000.0, 2, 3, blocked: false, dodged: false), 0.0);
        }

        [Test]
        public void AWholeBarricadeCostsTheSameHoweverManySwingsItGetsIn()
        {
            // THE PROPERTY THE RE-PRICING EXISTS FOR. The shipped share was charged per swing
            // against the LIVE army, so the bill compounded with the swing count — and the
            // fight is longest against the smallest army, because FightLength shrinks with its
            // square root. An unavoidable weight-4 champion took 98.3% of a hundred-man army
            // and 68.6% of a million. Splitting one whole-fight share across the swings makes
            // the number of them irrelevant, which is what lets the same wall be fair to both.
            for (int w = 1; w <= 4; w++)
            {
                double whole = Elite.WholeFightShare(w);
                for (int swings = 1; swings <= 3; swings++)
                {
                    double army = 10_000.0;
                    for (int i = 0; i < swings; i++)
                        army -= Elite.SwingCost(army, w, swings, false, false);
                    Assert.AreEqual(10_000.0 * (1.0 - whole), army, 0.5,
                        $"weight {w} over {swings} swings did not land on its whole-fight share");
                }
            }
        }

        [Test]
        public void NoBarricadeCanEndARunOnItsOwn()
        {
            // The bound that the old numbers did not have. Whatever the weight and whatever the
            // army, tanking every swing of one barricade leaves most of the army standing —
            // and the smallest army must not be the worst case, which is where the old curve
            // was upside down.
            foreach (double army in new[] { 100.0, 2_000.0, 1e6, 1e9 })
                for (int w = 1; w <= 4; w++)
                {
                    Elite e = Elite.Begin(army, w);
                    double left = army;
                    for (int i = 0; i < e.SwingsExpected; i++)
                        left -= Elite.SwingCost(left, w, e.SwingsExpected, false, false);
                    Assert.Greater(left, army * 0.7,
                        $"a weight-{w} barricade took more than 30% of an army of {army:N0}, "
                        + "which it cannot be steered around to avoid");
                }
        }

        [Test]
        public void TheFightHasRoomForAtMostThreeSwings()
        {
            // Not taste: the swing count is the divisor the per-swing share is derived from,
            // and it is also how partial an answer the shield is. Three swings means blocking
            // one saves a third. Seven — which the old 10.8 s ceiling allowed against a small
            // army — means the shield magazine decides the fight rather than the player.
            foreach (double army in new[] { 1.0, 100.0, 2_000.0, 1e9 })
                for (int w = 1; w <= 4; w++)
                {
                    Elite e = Elite.Begin(army, w);
                    Assert.GreaterOrEqual(e.SwingsExpected, 1);
                    Assert.LessOrEqual(e.SwingsExpected, 3,
                        $"weight {w} against {army:N0} men has room for {e.SwingsExpected} swings");
                }
            Assert.LessOrEqual(Elite.FightLength(1.0, 9), Elite.MaxFightSeconds);
        }

        [Test]
        public void TheSwingsExpectedAtTheStartAreTheSwingsThatActuallyLand()
        {
            // The derived share is only the right share if the count it was derived from is
            // the count that happens. Run the real loop — grind first, swing only on a frame
            // the champion survived — and compare.
            foreach (double army in new[] { 60.0, 800.0, 5e4, 1e7 })
                for (int w = 1; w <= 4; w++)
                {
                    Elite e = Elite.Begin(army, w);
                    int landed = 0;
                    for (int i = 0; i < 4000 && e.Alive; i++)
                    {
                        bool died = e.Grind(1f / 240f);
                        if (!died && e.TakeSwingIfDue()) landed++;
                    }
                    Assert.AreEqual(e.SwingsExpected, landed,
                        $"weight {w} against {army:N0} men budgeted {e.SwingsExpected} swings "
                        + $"and landed {landed}");
                }
        }

        [Test]
        public void ASwingNeverWipesTheArmyAndNeverCostsNothing()
        {
            // Both clamps matter, at opposite ends of the campaign.
            Assert.AreEqual(1.0, Elite.SwingCost(3.0, 3, 3, false, false), 1e-9,
                "at three men the floor is what keeps the swing visible");
            for (int w = 1; w <= 4; w++)
                Assert.Less(Elite.SwingCost(1000.0, w, 1, false, false), 1000.0,
                    $"weight {w} wiped the army in one swing");
        }

        [Test]
        public void ABiggerArmyKillsItFasterButNeverInstantly()
        {
            float small = Elite.FightLength(50.0, 1);
            float big = Elite.FightLength(50_000.0, 1);
            float huge = Elite.FightLength(50e12, 1);

            Assert.Less(big, small, "a bigger army must grind it down faster");
            Assert.GreaterOrEqual(huge, Elite.MinFightSeconds,
                "no army should make an elite free");
            Assert.Greater(Elite.FightLength(200.0, 3), Elite.FightLength(200.0, 1),
                "a heavier champion must take longer");
        }

        [Test]
        public void ItDiesExactlyOnceSoTheBountyIsPaidOnce()
        {
            Elite e = Elite.Begin(200.0, 1);
            int deaths = 0;
            for (int i = 0; i < 2000; i++)
                if (e.Grind(1f / 60f)) deaths++;
            Assert.AreEqual(1, deaths, "Grind must report the death on exactly one frame");
            Assert.IsFalse(e.Alive);
        }

        [Test]
        public void HealthFallsMonotonicallyFromOneToZero()
        {
            Elite e = Elite.Begin(200.0, 2);
            float previous = e.Health;
            Assert.AreEqual(1f, previous, 1e-6f);
            for (int i = 0; i < 2000 && e.Alive; i++)
            {
                e.Grind(1f / 60f);
                Assert.LessOrEqual(e.Health, previous + 1e-6f, "health went back up");
                Assert.GreaterOrEqual(e.Health, 0f);
                previous = e.Health;
            }
            Assert.AreEqual(0f, e.Health, 1e-6f);
        }

        [Test]
        public void ShieldingTheWholeFightIsWorthFarMoreThanTankingIt()
        {
            // THE SKILL GRADIENT, and it is the reason a mandatory obstacle is still a
            // decision. Tank every swing of a barricade and the bounty nearly makes you whole;
            // shield every swing and you leave a fifth larger than you arrived. The gap between
            // the two is what the player is playing for, and it has to be a gap rather than a
            // cliff — the old numbers made tanking a weight-4 champion cost 68.6% of a million
            // men, which is not a decision, it is a punishment for owning a shield magazine.
            for (int w = 2; w <= 4; w++)
            {
                double tanked = Elite.UnshieldedFactor(w);
                double shielded = 1.0 + Elite.BountyShare * w;
                Assert.Less(tanked, 1.0, $"weight {w}: tanking a barricade came out ahead");
                Assert.Greater(tanked, 0.88, $"weight {w}: tanking a barricade is a spiral");
                // The gap has to GROW with the weight, or a heavy barricade is no more worth
                // shielding than a light one and the shield magazine has nothing to spend
                // itself on. It comes to 13.2, 20.7 and 28.8 points at weights 2, 3 and 4.
                Assert.Greater(shielded - tanked, Elite.BarricadeShare * w,
                    $"weight {w}: shielding is worth only {(shielded - tanked) * 100:0.0} "
                    + "points more than standing there, which is not a decision");
            }
        }

        [Test]
        public void KillingOnePaysSomethingWorthHaving()
        {
            double bounty = Elite.Bounty(1000.0, 1);
            double whole = 1000.0 * Elite.WholeFightShare(1);
            Assert.Greater(bounty, whole * 0.5,
                "the reward for beating it should be comparable to the cost of failing it");
            Assert.GreaterOrEqual(Elite.Bounty(1.0, 1), 1.0, "it must always pay at least a man");
        }

        // ===================================================================
        // The generator
        // ===================================================================

        [Test]
        public void TheChampionShapeIsActuallyGenerated()
        {
            // THE FAILURE THIS GUARDS IS SILENT. SequenceFor picked with a bare `% 8u`; a
            // ninth shape would have been authored, wired and tested and then never appear in
            // a single round, with every existing test still green.
            bool seen = false;
            for (int round = 0; round < 200 && !seen; round++)
                foreach (ChunkShape shape in ChunkLayouts.SequenceFor(RoundPlan.For(round), 16))
                    if (shape == ChunkShape.Champion) { seen = true; break; }

            Assert.IsTrue(seen, "no round in two hundred contained a champion");
        }

        [Test]
        public void EveryChampionTheGeneratorAuthorsBlocksTheWholeRoad()
        {
            // *"mini bosses I fight only if they are on my lane, and I want them to be on 3
            // lanes, mandatory to fight."* The flag is per-pack rather than implied by Elite,
            // so a champion authored without it would be dodgeable again and nothing else
            // would complain. And its centre is always lane 0: a wall spanning the road with
            // its champion drawn off to one side reads as if the other two thirds were open.
            int champions = 0;
            for (int round = 0; round < 60; round++)
                foreach (ChunkLayout layout in ChunkLayouts.BuildRound(RoundPlan.For(round)))
                    foreach (PlannedPack pack in layout.Packs)
                    {
                        if (!pack.Elite) { Assert.IsFalse(pack.BlocksAllLanes,
                            "an ordinary squad blocked the road; dodging one is still the game"); continue; }
                        champions++;
                        Assert.IsTrue(pack.BlocksAllLanes, "a champion the player can steer around");
                        Assert.AreEqual(0, pack.Lane, "a barricade is centred, or it reads as a gap");
                    }
            Assert.Greater(champions, 20, "too few champions generated to conclude anything");
        }

        [Test]
        public void ParChargesABarricadeInEveryLaneAndAtItsOwnPrice()
        {
            // TWO WAYS TO GET THIS WRONG AND THE OLD CODE HAD BOTH. Charging a barricade in
            // one lane lets par treat the other two as escapes that no longer exist; charging
            // it as an ambush gate of the same weight prices a weight-4 champion at 62% of the
            // army when it actually takes 24% and pays 20% back. Measured on the shape itself:
            // par for a champion chunk must land near the recruit gate's gain times the
            // barricade's own factor, and nowhere near either mistake.
            var champion = new ChunkLayout(ChunkShape.Champion);
            champion.Packs.Add(new PlannedPack(3, 0, 26f, elite: true, blocksAllLanes: true));
            double par = ChunkLayouts.EstimateParForce(new[] { champion }, 10_000.0);

            Assert.AreEqual(10_000.0 * Elite.UnshieldedFactor(3), par, 1.0,
                "par did not charge the barricade at its own price in every lane");
            Assert.Less(par, 10_000.0, "a barricade that par counts as free is not an obstacle");
            Assert.Greater(par, 10_000.0 * (1.0 - GateMath.AmbushShare * 3),
                "par is still charging the barricade like an ambush gate of the same weight");
        }

        [Test]
        public void EveryShapeInTheEnumCanBeGenerated()
        {
            // The general form of the bug above, so the next shape added cannot repeat it.
            var seen = new System.Collections.Generic.HashSet<ChunkShape>();
            for (int round = 0; round < 400; round++)
                foreach (ChunkShape shape in ChunkLayouts.SequenceFor(RoundPlan.For(round), 16))
                    seen.Add(shape);

            Assert.AreEqual(ChunkShapes.Count, seen.Count,
                "some shape in the enum is never picked: " + string.Join(", ", seen));
        }

        [Test]
        public void AChampionNeverOpensARound()
        {
            // It is in IsHarsh, so it cannot land in the first three chunks.
            for (int round = 0; round < 200; round++)
            {
                ChunkShape[] shapes = ChunkLayouts.SequenceFor(RoundPlan.For(round), 16);
                for (int i = 0; i < 3; i++)
                    Assert.AreNotEqual(ChunkShape.Champion, shapes[i],
                        $"round {round} opened with a champion at chunk {i}");
            }
        }

        [Test]
        public void AChampionChunkHoldsOneEliteAndNothingCrowdingIt()
        {
            uint rng = 12345u;
            ChunkLayout layout = ChunkLayouts.Build(ChunkShape.Champion, 5, 4, ref rng);

            int elites = 0;
            foreach (PlannedPack pack in layout.Packs) if (pack.Elite) elites++;
            Assert.AreEqual(1, elites, "a champion chunk must hold exactly one champion");

            // It is the one event in the round that needs the player's whole attention, so
            // nothing may sit inside the reaction gap on either side of it.
            float at = 0f;
            foreach (PlannedPack pack in layout.Packs) if (pack.Elite) at = pack.Position;
            foreach (PlannedGate gate in layout.Gates)
                Assert.GreaterOrEqual(Math.Abs(gate.Position - at), ChunkLayouts.MinDecisionGap,
                    "something is crowding the champion");
        }

        [Test]
        public void OnlyAChampionChunkPlacesAnElite()
        {
            foreach (ChunkShape shape in Enum.GetValues(typeof(ChunkShape)))
            {
                if (shape == ChunkShape.Champion) continue;
                uint rng = 999u;
                ChunkLayout layout = ChunkLayouts.Build(shape, 7, 3, ref rng);
                foreach (PlannedPack pack in layout.Packs)
                    Assert.IsFalse(pack.Elite, $"{shape} placed an elite");
            }
        }

        [Test]
        public void AChampionChunkStaysInsideThePoolPrewarm()
        {
            // ObjectPool.Get silently instantiates on an empty pool, which doc 04 bans mid-run,
            // and the prewarm is derived from these two constants.
            uint rng = 7u;
            ChunkLayout layout = ChunkLayouts.Build(ChunkShape.Champion, 9, 5, ref rng);
            Assert.LessOrEqual(layout.Packs.Count, ChunkLayouts.MaxPacksPerChunk);
            Assert.LessOrEqual(layout.Gates.Count, ChunkLayouts.MaxGatesPerChunk);
        }
    }
}
