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
            // should feel like an answer, not like a discount.
            Assert.AreEqual(0.0, Elite.SwingCost(1000.0, 2, blocked: true, dodged: false));
            Assert.AreEqual(0.0, Elite.SwingCost(1000.0, 2, blocked: false, dodged: true));
            Assert.Greater(Elite.SwingCost(1000.0, 2, blocked: false, dodged: false), 0.0);
        }

        [Test]
        public void ASwingHurtsFarMoreThanAnAmbushGate()
        {
            // It has two answers where an ambush gate has one, so it must be worth answering.
            double swing = Elite.SwingCost(1000.0, 1, false, false);
            double ambush = 1000.0 - GateMath.ApplyGate(1000.0, GateOp.Subtract, 1, 0);
            Assert.Greater(swing, ambush * 0.7,
                "an elite's swing that costs less than a gate is a chore, not a threat");
        }

        [Test]
        public void ASwingNeverWipesTheArmyAndNeverCostsNothing()
        {
            // Both clamps matter, at opposite ends of the campaign.
            Assert.AreEqual(1.0, Elite.SwingCost(3.0, 3, false, false), 1e-9,
                "at three men the floor is what keeps the swing visible");
            for (int w = 1; w <= 4; w++)
                Assert.Less(Elite.SwingCost(1000.0, w, false, false), 1000.0,
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
        public void KillingOnePaysSomethingWorthHaving()
        {
            double bounty = Elite.Bounty(1000.0, 1);
            double swing = Elite.SwingCost(1000.0, 1, false, false);
            Assert.Greater(bounty, swing * 0.8,
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
