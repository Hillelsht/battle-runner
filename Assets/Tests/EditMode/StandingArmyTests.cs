using System;
using System.Collections.Generic;
using System.Linq;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The promise, as tests: "once you achieved a certain level or crowd size you never
    /// lose it", alongside a campaign that still gets harder.
    ///
    /// The last fixture walks the REAL generator through the REAL gate arithmetic for
    /// sixty-two rounds — the whole of the authored campaign — because every number in
    /// this design is a compounding one, and a compounding number is exactly the kind that
    /// looks right in isolation and is off by ten orders of magnitude by the end.
    /// </summary>
    [TestFixture]
    public class StandingArmyTests
    {
        [Test]
        public void ABrandNewProfileMustersTheSeed()
        {
            Assert.AreEqual(StandingArmy.Seed, StandingArmy.StartOfRound(0.0, 0.0), 1e-9);
            Assert.AreEqual(StandingArmy.Seed, StandingArmy.Floor(0.0), 1e-9);
        }

        [Test]
        public void ARoundNeverStartsSmallerThanTheOneBefore()
        {
            // The literal reading of the request, as an assertion over every reachable pair
            // of (banked, best-ever). Monotone in both arguments means no sequence of rounds,
            // however disastrous, can produce a smaller start than a better history would.
            double[] samples = { 0.0, 1.0, 5.0, 47.0, 2884.0, 1e6, 4.2e12 };
            foreach (double best in samples)
                foreach (double a in samples)
                    foreach (double b in samples)
                    {
                        if (a > b) continue;
                        Assert.LessOrEqual(
                            StandingArmy.StartOfRound(a, best),
                            StandingArmy.StartOfRound(b, best) + 1e-9,
                            $"banked {a} started higher than banked {b}");
                    }
        }

        [Test]
        public void TheFloorRisesWithTheBestArmyEverFieldedAndNeverFalls()
        {
            double best = 0.0;
            double previousFloor = StandingArmy.Floor(best);
            foreach (double peak in new[] { 12.0, 9.0, 400.0, 80.0, 1e5, 3.0, 7e11 })
            {
                best = StandingArmy.Record(best, peak);
                double floor = StandingArmy.Floor(best);
                Assert.GreaterOrEqual(floor, previousFloor,
                    $"a peak of {peak} lowered the permanent floor");
                previousFloor = floor;
            }
        }

        [Test]
        public void ACatastrophicRoundCostsGroundButNotTheRank()
        {
            // Both halves at once, which is the whole design: losing is possible, and being
            // sent back to the beginning is not.
            const double peak = 10_000.0;
            double best = StandingArmy.Record(0.0, peak);
            double afterDisaster = StandingArmy.StartOfRound(1.0, best);

            Assert.Less(afterDisaster, peak, "a disastrous round must cost real ground");
            Assert.AreEqual(peak * StandingArmy.FloorShare, afterDisaster, 1e-6);
            Assert.Greater(afterDisaster, StandingArmy.Seed * 100.0,
                "and must never put the player back near the seed");
        }

        [Test]
        public void RankIsAReadableLadderRatherThanARawHeadcount()
        {
            Assert.AreEqual(1, StandingArmy.Rank(StandingArmy.Seed));
            Assert.AreEqual(2, StandingArmy.Rank(StandingArmy.Seed * 2));
            Assert.AreEqual(11, StandingArmy.Rank(StandingArmy.Seed * 1024));

            // Monotone, or it is not a rank.
            int previous = 0;
            for (double f = 1.0; f < 1e15; f *= 1.7)
            {
                int rank = StandingArmy.Rank(f);
                Assert.GreaterOrEqual(rank, previous);
                previous = rank;
            }
        }

        // ===================================================================
        // The campaign, walked end to end.
        // ===================================================================

        /// <summary>
        /// Play one generated round, steering at <paramref name="quality"/> between the
        /// worst lane and the best at every decision. 1.0 is perfect lane choice.
        ///
        /// THE REVEAL LAG IS MODELLED, because it moves the curve and the curve is what this
        /// file is for. A gate or a pack commits to a headcount when it comes within
        /// Reveal.LineMeters of the army's front and takes or gives THAT many men when the
        /// army arrives — so its effect is a share of the army as it was 34 m earlier, not of
        /// the army standing in front of it. At 45 m chunks that is up to six gates of lag,
        /// and it is not a rounding error: it turns a x(1+s) into a x(1 + s*F_reveal/F_contact)
        /// on both signs at once.
        /// </summary>
        private static (double peak, double final) PlayRound(int roundIndex, double start,
            double quality)
        {
            ChunkLayout[] layouts = ChunkLayouts.BuildRound(RoundPlan.For(roundIndex));
            double force = Math.Max(1.0, start);
            double peak = force;

            var events = new List<(double z, int lane, GateOp op, int weight, bool blocks, int chunk)>();
            for (int chunk = 0; chunk < layouts.Length; chunk++)
            {
                double baseZ = chunk * (double)ChunkLayouts.ChunkMeters;
                foreach (PlannedGate g in layouts[chunk].Gates)
                    events.Add((baseZ + g.Position, g.Lane, g.Op, g.Value, false, chunk));
                foreach (PlannedPack p in layouts[chunk].Packs)
                    events.Add((baseZ + p.Position, p.Lane, GateOp.Subtract, p.ForceCost,
                        p.BlocksAllLanes, chunk));
            }

            // Where the army was, so a gate can be priced against the army that saw it. Seeded
            // with the start so anything revealed before the army has moved is priced off that.
            var history = new List<(double z, double force)> { (double.MinValue, force) };
            double RevealedAgainst(double z)
            {
                double at = z - RevealLine;
                double seen = history[0].force;
                for (int i = 0; i < history.Count && history[i].z <= at; i++) seen = history[i].force;
                return seen;
            }

            foreach (var group in events.GroupBy(e => Math.Round(e.z, 1)).OrderBy(g => g.Key))
            {
                double atReveal = RevealedAgainst(group.Key);
                double best = double.MinValue, worst = double.MaxValue;
                // -1 to 1, which is the range the generator actually authors into. This
                // walked 0 to 2 and so shared EstimateParForce's blind spot: a free
                // imaginary lane on the right and an unread one on the left.
                for (int lane = ChunkLayouts.LaneMin; lane <= ChunkLayouts.LaneMax; lane++)
                {
                    double factor = 1.0;
                    foreach (var e in group)
                    {
                        if (e.blocks) { factor *= Elite.UnshieldedFactor(e.weight); continue; }
                        if (e.lane != lane) continue;
                        if (e.op == GateOp.Multiply)
                        {
                            // A rally's sign is a factor, so it never pops and never latches.
                            factor *= GateMath.Factor(e.op, e.weight, e.chunk);
                            continue;
                        }
                        // The men it committed to, against the army that will actually meet them.
                        double men = GateMath.Headcount(atReveal, e.op, e.weight, e.chunk);
                        factor *= 1.0 + men / Math.Max(1.0, force);
                    }
                    if (factor > best) best = factor;
                    if (factor < worst) worst = factor;
                }
                force *= Math.Max(0.0, worst + (best - worst) * quality);
                if (force < 1.0) force = 1.0;
                if (force > peak) peak = force;
                history.Add((group.Key, force));
            }

            return (peak, force);
        }

        private static (double banked, double floor, int rank) Campaign(double quality, int rounds)
        {
            double banked = StandingArmy.Seed, best = StandingArmy.Seed;
            for (int r = 0; r < rounds; r++)
            {
                double start = StandingArmy.StartOfRound(banked, best);
                (double peak, double final) = PlayRound(r, start, quality);
                banked = final;
                best = StandingArmy.Record(best, peak);
            }
            return (banked, StandingArmy.Floor(best), StandingArmy.Rank(banked));
        }

        internal static (double banked, double floor, int rank) ProbeCampaign(double q, int rounds)
            => Campaign(q, rounds);

        /// <summary>The reveal line, in metres. See Core/Run/Reveal.</summary>
        private const double RevealLine = BattleRunner.Core.Run.Reveal.LineMeters;

        /// <summary>Rounds in the sixteen authored acts — the whole campaign.</summary>
        private const int CampaignRounds = 62;

        [Test]
        public void AGoodPlayerGrowsAnArmyWorthCountingInBillions()
        {
            (double banked, _, int rank) = Campaign(0.85, CampaignRounds);
            Assert.Greater(banked, 1e9, $"a competent campaign ended on {banked:0.##E+0}");
            Assert.Greater(rank, 30, "and should read as a serious rank");
        }

        [Test]
        public void PlayingBadlyCostsGroundAndTheFloorStopsTheSpiral()
        {
            // The difficulty asked for, and the promise made alongside it, in one test.
            // At 0.65 lane quality the army does NOT grow across the campaign — which is
            // the point, because a game where every line of play compounds upward has no
            // difficulty in it — and the floor is what keeps that from being a spiral.
            (double banked, double floor, _) = Campaign(0.65, CampaignRounds);
            Assert.Less(banked, 1e4, "sloppy steering must not build an empire");
            Assert.GreaterOrEqual(floor, StandingArmy.Seed,
                "and must never leave the player below the seed muster");
        }

        [Test]
        public void SteeringWellIsWorthOrdersOfMagnitudeOverACampaign()
        {
            (double sloppy, _, _) = Campaign(0.72, CampaignRounds);
            (double careful, _, _) = Campaign(0.85, CampaignRounds);
            Assert.Greater(careful, sloppy * 1000.0,
                "if lane choice does not compound, the run is not a game");
        }

        [Test]
        public void TheWholeCampaignFitsInADoubleWithRoomToSpare()
        {
            // THE REASON FORCE IS NOT A LONG. At the measured growth of a competent player a
            // long (9.2e18) overflows around round 52 — inside the authored content, so this
            // is not a theoretical ceiling. Even perfect lane choice has to stay finite.
            (double banked, double floor, _) = Campaign(1.0, CampaignRounds);
            Assert.IsFalse(double.IsInfinity(banked), "perfect play overflowed the army");
            Assert.IsFalse(double.IsNaN(banked));
            Assert.Greater(banked, 0.0);
            Assert.Greater(floor, 0.0);
        }

        [Test]
        public void EveryRoundOfTheCampaignStartsAtOrAboveTheFloor()
        {
            double banked = StandingArmy.Seed, best = StandingArmy.Seed;
            for (int r = 0; r < CampaignRounds; r++)
            {
                double floor = StandingArmy.Floor(best);
                double start = StandingArmy.StartOfRound(banked, best);
                Assert.GreaterOrEqual(start, floor - 1e-9,
                    $"round {r} started below the floor the player was promised");
                (double peak, double final) = PlayRound(r, start, 0.70);
                banked = final;
                best = StandingArmy.Record(best, peak);
            }
        }
    }
}
