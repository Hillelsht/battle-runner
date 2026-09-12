using System.Collections.Generic;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The eight things a stretch of road can ask. The generator this replaces had exactly
    /// three outcomes and cycled them forever, which is the literal reason the report was
    /// "every round the doors are the same".
    /// </summary>
    [TestFixture]
    public class ChunkLayoutTests
    {
        private static readonly ChunkShape[] AllShapes =
        {
            ChunkShape.Ladder, ChunkShape.Fork, ChunkShape.Gauntlet, ChunkShape.Minefield,
            ChunkShape.Toll, ChunkShape.Vault, ChunkShape.Breather, ChunkShape.Crossfire
        };

        [Test]
        public void NoShapeCrowdsTwoDecisionsTooCloseToRead()
        {
            // The comment that survives from the original generator: at 10 m/s, decisions
            // 0.2 s apart are unreadable. Gates sharing a Z are ONE decision (pick a lane);
            // gates at different Z are two.
            foreach (ChunkShape shape in AllShapes)
            for (int difficulty = 0; difficulty < 40; difficulty += 7)
            for (int step = 0; step < 20; step += 5)
            {
                uint rng = 12345u;
                ChunkLayout layout = ChunkLayouts.Build(shape, difficulty, step, ref rng);

                var stops = new SortedSet<float>();
                foreach (PlannedGate g in layout.Gates) stops.Add(g.Position);
                foreach (PlannedPack p in layout.Packs) stops.Add(p.Position);

                float previous = float.NegativeInfinity;
                foreach (float z in stops)
                {
                    if (previous > float.NegativeInfinity)
                        Assert.GreaterOrEqual(z - previous, ChunkLayouts.MinDecisionGap,
                            $"{shape}: decisions at {previous} and {z} are too close");
                    previous = z;
                }
            }
        }

        [Test]
        public void NothingIsPlacedOverTheSeamIntoTheNextChunk()
        {
            foreach (ChunkShape shape in AllShapes)
            {
                uint rng = 7u;
                ChunkLayout layout = ChunkLayouts.Build(shape, 10, 3, ref rng);
                foreach (PlannedGate g in layout.Gates)
                {
                    Assert.GreaterOrEqual(g.Position, 0f, shape.ToString());
                    Assert.LessOrEqual(g.Position, ChunkLayouts.ChunkMeters - ChunkLayouts.TailMargin,
                        shape.ToString());
                }
                foreach (PlannedPack p in layout.Packs)
                    Assert.LessOrEqual(p.Position, ChunkLayouts.ChunkMeters - ChunkLayouts.TailMargin,
                        shape.ToString());
            }
        }

        [Test]
        public void EveryLaneUsedIsARealLane()
        {
            foreach (ChunkShape shape in AllShapes)
            for (int d = 0; d < 30; d += 3)
            {
                uint rng = (uint)(d + 1);
                ChunkLayout layout = ChunkLayouts.Build(shape, d, d, ref rng);
                foreach (PlannedGate g in layout.Gates)
                {
                    Assert.GreaterOrEqual(g.Lane, -1, shape.ToString());
                    Assert.LessOrEqual(g.Lane, 1, shape.ToString());
                    Assert.GreaterOrEqual(g.Value, 0, "GateMath throws on a negative value");
                }
                foreach (PlannedPack p in layout.Packs)
                {
                    Assert.GreaterOrEqual(p.Lane, -1, shape.ToString());
                    Assert.LessOrEqual(p.Lane, 1, shape.ToString());
                    Assert.GreaterOrEqual(p.ForceCost, 1, shape.ToString());
                }
            }
        }

        [Test]
        public void NoShapeExceedsThePoolBudget()
        {
            // Pools are prewarmed from these. If a shape grew past them, ObjectPool.Get would
            // silently instantiate mid-run, which doc 04 bans.
            foreach (ChunkShape shape in AllShapes)
            {
                uint rng = 99u;
                ChunkLayout layout = ChunkLayouts.Build(shape, 25, 9, ref rng);
                Assert.LessOrEqual(layout.Gates.Count, ChunkLayouts.MaxGatesPerChunk, shape.ToString());
                Assert.LessOrEqual(layout.Packs.Count, ChunkLayouts.MaxPacksPerChunk, shape.ToString());
            }
        }

        [Test]
        public void ATollLeavesNoFreeLane()
        {
            uint rng = 4u;
            ChunkLayout toll = ChunkLayouts.Build(ChunkShape.Toll, 10, 2, ref rng);
            var laneCosts = new HashSet<int>();
            foreach (PlannedGate g in toll.Gates)
                if (g.Op == GateOp.Subtract) laneCosts.Add(g.Lane);
            Assert.AreEqual(3, laneCosts.Count, "the whole point is that every lane costs something");
        }

        [Test]
        public void AMinefieldLeavesExactlyOneSafeLane()
        {
            uint rng = 4u;
            ChunkLayout mines = ChunkLayouts.Build(ChunkShape.Minefield, 10, 2, ref rng);
            int subtracts = 0, adds = 0;
            var lanes = new HashSet<int>();
            foreach (PlannedGate g in mines.Gates)
            {
                lanes.Add(g.Lane);
                if (g.Op == GateOp.Subtract) subtracts++;
                if (g.Op == GateOp.Add) adds++;
            }
            Assert.AreEqual(3, lanes.Count);
            Assert.AreEqual(2, subtracts);
            Assert.AreEqual(1, adds);
        }

        [Test]
        public void AVaultPutsThePrizeBehindThePrice()
        {
            uint rng = 4u;
            ChunkLayout vault = ChunkLayouts.Build(ChunkShape.Vault, 10, 2, ref rng);
            Assert.AreEqual(1, vault.Packs.Count);
            PlannedPack pack = vault.Packs[0];

            PlannedGate multiply = default;
            bool found = false;
            foreach (PlannedGate g in vault.Gates)
                if (g.Op == GateOp.Multiply) { multiply = g; found = true; }

            Assert.IsTrue(found, "a vault without a prize is just a pack");
            Assert.AreEqual(pack.Lane, multiply.Lane, "taking the prize has to mean paying for it");
            Assert.Greater(multiply.Position, pack.Position, "the price comes first");
        }

        [Test]
        public void AGauntletOffersNothingAndABreatherAsksNothing()
        {
            uint rng = 4u;
            ChunkLayout gauntlet = ChunkLayouts.Build(ChunkShape.Gauntlet, 10, 2, ref rng);
            Assert.IsEmpty(gauntlet.Gates, "a gauntlet is steering, not shopping");
            Assert.AreEqual(3, gauntlet.Packs.Count);

            rng = 4u;
            ChunkLayout breather = ChunkLayouts.Build(ChunkShape.Breather, 10, 2, ref rng);
            Assert.IsEmpty(breather.Packs);
            Assert.AreEqual(1, breather.Gates.Count);
        }

        // --- the sequence ------------------------------------------------------

        [Test]
        public void ARoundNeverRepeatsAShapeBackToBack()
        {
            for (int round = 0; round < 120; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                ChunkShape[] shapes = ChunkLayouts.SequenceFor(plan, plan.ChunkCount);
                for (int i = 1; i < shapes.Length; i++)
                    Assert.AreNotEqual(shapes[i - 1], shapes[i], $"round {round} at chunk {i}");
            }
        }

        [Test]
        public void ARoundNeverOpensByPunishing()
        {
            for (int round = 0; round < 120; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                ChunkShape[] shapes = ChunkLayouts.SequenceFor(plan, plan.ChunkCount);
                Assert.IsTrue(shapes[0] == ChunkShape.Ladder || shapes[0] == ChunkShape.Breather,
                    $"round {round} opens on {shapes[0]}");
                for (int i = 0; i < 3 && i < shapes.Length; i++)
                {
                    Assert.AreNotEqual(ChunkShape.Gauntlet, shapes[i], $"round {round} chunk {i}");
                    Assert.AreNotEqual(ChunkShape.Toll, shapes[i], $"round {round} chunk {i}");
                    Assert.AreNotEqual(ChunkShape.Minefield, shapes[i], $"round {round} chunk {i}");
                }
            }
        }

        [Test]
        public void EveryRoundHasSomewhereToExhale()
        {
            for (int round = 0; round < 120; round++)
            {
                RoundPlan plan = RoundPlan.For(round);
                ChunkShape[] shapes = ChunkLayouts.SequenceFor(plan, plan.ChunkCount);
                bool lateBreather = false;
                for (int i = shapes.Length / 2; i < shapes.Length; i++)
                    if (shapes[i] == ChunkShape.Breather) lateBreather = true;
                Assert.IsTrue(lateBreather, $"round {round} has no breather in its back half");
            }
        }

        [Test]
        public void TwoRoundsOfTheSameLengthStillPlayDifferently()
        {
            // The failure this whole file exists to prevent.
            int same = 0, compared = 0;
            for (int a = 0; a < 60; a++)
            for (int b = a + 1; b < 60; b++)
            {
                RoundPlan pa = RoundPlan.For(a), pb = RoundPlan.For(b);
                if (pa.ChunkCount != pb.ChunkCount) continue;
                compared++;
                ChunkShape[] sa = ChunkLayouts.SequenceFor(pa, pa.ChunkCount);
                ChunkShape[] sb = ChunkLayouts.SequenceFor(pb, pb.ChunkCount);
                bool identical = true;
                for (int i = 0; i < sa.Length; i++)
                    if (sa[i] != sb[i]) { identical = false; break; }
                if (identical) same++;
            }

            Assert.Greater(compared, 50, "the test needs same-length pairs to compare");
            Assert.AreEqual(0, same, "two rounds generated the identical sequence of shapes");
        }

        [Test]
        public void ARoundIsGeneratedIdenticallyEveryTimeItIsPlayed()
        {
            for (int round = 0; round < 30; round++)
            {
                ChunkLayout[] a = ChunkLayouts.BuildRound(RoundPlan.For(round));
                ChunkLayout[] b = ChunkLayouts.BuildRound(RoundPlan.For(round));
                Assert.AreEqual(a.Length, b.Length, $"round {round}");
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Shape, b[i].Shape, $"round {round} chunk {i}");
                    Assert.AreEqual(a[i].Gates.Count, b[i].Gates.Count);
                    for (int g = 0; g < a[i].Gates.Count; g++)
                    {
                        Assert.AreEqual(a[i].Gates[g].Lane, b[i].Gates[g].Lane);
                        Assert.AreEqual(a[i].Gates[g].Value, b[i].Gates[g].Value);
                    }
                }
            }
        }

        // --- par force ---------------------------------------------------------

        [Test]
        public void ParForceDoesNotRunAwayWithDepth()
        {
            // DELIBERATELY REWRITTEN, and the direction of the assertion is the change.
            // It used to be `shallow < mid < deep`, which was true when a gate's value grew
            // with the round index: a deep round handed out bigger absolute numbers, so par
            // rose. A gate is a SHARE now, so a deeper round is not more generous — it is
            // LONGER (more chunks) and more dangerous (the ambush weight and depth ramp both
            // climb), and those pull in opposite directions on purpose.
            //
            // What must still hold is that par stays a sane multiple of the army that walked
            // in, at every depth, because it sizes the revive.
            // MEASURED, and the shape is the difficulty arriving where it was aimed: par
            // from a seed army runs 5.5x at round 0, 2.8x at round 5, 1.5x at round 15, and
            // is pinned at the floor from about round 21 on. A player steering at par is
            // below break-even in the back half of the campaign; StandingArmy.Floor is what
            // stops that from being a spiral, not this number.
            Assert.Greater(Par(0), Par(15), "the road must get harder, not more generous");
            for (int round = 0; round < 80; round++)
            {
                double par = Par(round);
                Assert.GreaterOrEqual(par, 1.0, $"round {round} par went to {par}");
                Assert.Less(par, 5.0 * 40.0, $"round {round} par ran away to {par}");
            }
        }

        [Test]
        public void ParBlendsTheBestLaneWithTheWorstOne()
        {
            // DELIBERATELY REWRITTEN. The old assertion was "5 + 95 = 100, and 60% of that
            // is 60" — a statement about absolute gate values, which no longer exist. Par is
            // now the geometric blend of the best and worst lane through each chunk, which
            // is both simpler and closer to what a real player does than the log-damped
            // multiply fudge it replaced.
            var layout = new ChunkLayout(ChunkShape.Ladder);
            layout.Gates.Add(new PlannedGate(GateOp.Add, 2, 0, 10f));
            layout.Gates.Add(new PlannedGate(GateOp.Subtract, 2, 1, 10f));

            double best = GateMath.Factor(GateOp.Add, 2, 0);
            double worst = GateMath.Factor(GateOp.Subtract, 2, 0);
            double expected = 100.0 * (worst + (best - worst) * ChunkLayouts.ParKeepFraction);

            Assert.AreEqual(expected, ChunkLayouts.EstimateParForce(new[] { layout }, 100.0), 1e-9);
        }

        [Test]
        public void ParScalesWithTheArmyThatWalksIn()
        {
            // Par sizes the revive, and the army is continuous now — a par computed from a
            // fixed five men would hand a player at ten billion a revive of nothing.
            var layouts = ChunkLayouts.BuildRound(RoundPlan.For(6));
            double small = ChunkLayouts.EstimateParForce(layouts, 100.0);
            double large = ChunkLayouts.EstimateParForce(layouts, 100e9);
            Assert.AreEqual(large / small, 1e9, 1e9 * 1e-6,
                "par must be proportional to the army, since every gate in it is");
        }

        [Test]
        public void ParForceDoesNotSwingWildlyBetweenNeighbouringRounds()
        {
            // The defect this replaced. The old estimate compounded every multiply, so par
            // was driven by how many multiply gates a round happened to roll: measured at
            // 297 on round 2 and 12,533 on round 5, with rounds 20 and 30 both pinned at the
            // soft cap. Par sizes the revive a player pays an ad for, so that swing was the
            // difference between coming back with 99 units and coming back with four thousand.
            double previous = Par(0);
            for (int round = 1; round < 80; round++)
            {
                double now = Par(round);
                Assert.Less(now, previous * 3.0 + 50.0, $"round {round} jumped from {previous} to {now}");
                Assert.Greater(now * 3.0 + 50.0, previous, $"round {round} fell from {previous} to {now}");
                previous = now;
            }
        }

        [Test]
        public void ParForceIsNeverZero()
        {
            // It divides revive amounts. A zero par would resurrect a player with nothing.
            Assert.GreaterOrEqual(ChunkLayouts.EstimateParForce(null, 5.0), 1.0);
            Assert.GreaterOrEqual(ChunkLayouts.EstimateParForce(new ChunkLayout[0], 1.0), 1.0);
        }

        private static double Par(int round)
        {
            RoundPlan plan = RoundPlan.For(round);
            return ChunkLayouts.EstimateParForce(ChunkLayouts.BuildRound(plan), 5.0);
        }
    }
}
