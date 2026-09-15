using System;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// THIS WHOLE FIXTURE IS A DELIBERATE REWRITE, and the reason is worth stating because
    /// the project's rule is that a pinned test is never edited quietly.
    ///
    /// Every assertion here used to be about ABSOLUTE arithmetic — a +5 gate adds five men,
    /// a x3 gate triples, a soft cap converts the excess into overflow. Those were correct
    /// statements about a game that re-mustered five men at the start of every round, and
    /// they are meaningless in a game whose army is continuous, because that game has no
    /// absolute gate values and no cap for anything to spill over. Keeping the old tests
    /// passing would have required keeping the old arithmetic, which is the change.
    ///
    /// What survives is the INTENT of each one, re-expressed:
    ///   Add_IncreasesForce          -> a recruit gate still pays, at any scale
    ///   Multiply_ScalesForce        -> a rally is still worth more than a recruit
    ///   Subtract_NeverGoesBelowZero -> an ambush still cannot make the army negative
    ///   SoftCap_*                   -> retired; the cap is gone, and the property that
    ///                                  replaces it is scale invariance, tested below
    ///   MultiplyChain_NeverOverflows-> replaced by a full-campaign range test
    ///   RandomizedSequences_*       -> kept, against the new reference arithmetic
    ///   OverflowBonus_*             -> now SurplusToBonusMultiplier, same curve shape
    /// </summary>
    [TestFixture]
    public class GateMathTests
    {
        [Test]
        public void ARecruitGateIsWorthTheSameShareAtEveryScale()
        {
            // The whole point of the change: a gate cannot become irrelevant as the army
            // grows, because what it gives is measured in the army it meets.
            double small = GateMath.ApplyGate(50.0, GateOp.Add, 1, 0) / 50.0;
            double huge = GateMath.ApplyGate(50e12, GateOp.Add, 1, 0) / 50e12;
            Assert.AreEqual(small, huge, 1e-9,
                "a recruit gate must pay the same ratio at fifty men and at fifty trillion");
            Assert.Greater(small, 1.0);
        }

        [Test]
        public void ARallyIsWorthMoreThanARecruit()
        {
            Assert.Greater(GateMath.Factor(GateOp.Multiply, 1, 0),
                GateMath.Factor(GateOp.Add, 1, 0),
                "the golden arch has to be the gate worth steering for");
        }

        [Test]
        public void AnAmbushTakesForceAndNeverGoesBelowZero()
        {
            Assert.Less(GateMath.ApplyGate(100.0, GateOp.Subtract, 1, 0), 100.0);
            Assert.GreaterOrEqual(GateMath.ApplyGate(100.0, GateOp.Subtract, 9, 40), 0.0);
            Assert.AreEqual(0.0, GateMath.ApplyGate(0.0, GateOp.Subtract, 3, 0));
        }

        [Test]
        public void NoSingleAmbushCanTakeTheWholeArmy()
        {
            // A gate that could zero the army in one touch would make the run a coin flip
            // rather than a sequence of decisions, however deep the round got.
            for (int depth = 0; depth < 60; depth++)
                for (int weight = 1; weight <= 6; weight++)
                {
                    double left = GateMath.ApplyGate(1000.0, GateOp.Subtract, weight, depth);
                    Assert.Greater(left, 0.0, $"weight {weight} at depth {depth} wiped the army");
                }
        }

        [Test]
        public void TheRedSideScalesWithDepthAndTheGreenSideDoesNot()
        {
            // The difficulty asked for, as an assertion. If this ever flips, the run has
            // stopped getting harder as it goes on.
            Assert.Less(GateMath.Factor(GateOp.Subtract, 1, 12),
                GateMath.Factor(GateOp.Subtract, 1, 0),
                "an ambush must bite harder deeper into a round");
            Assert.AreEqual(GateMath.Factor(GateOp.Add, 1, 0),
                GateMath.Factor(GateOp.Add, 1, 12), 1e-12,
                "a recruit gate must NOT scale with depth, or the two cancel out");
        }

        [Test]
        public void TheSignShowsAHeadcountEvenWhenTheShareIsTiny()
        {
            // A gate reading "+0" in front of a player about to gain from it is a lie about
            // the mechanic, and at ten men a 3% share rounds to nothing.
            Assert.AreEqual(1L, GateMath.Headcount(10.0, GateOp.Add, 1, 0));
            Assert.AreEqual(-1L, GateMath.Headcount(3.0, GateOp.Subtract, 1, 0));
            Assert.Greater(GateMath.Headcount(1_000_000.0, GateOp.Add, 1, 0), 1000L);
        }

        // ===================================================================
        // The floor: a gate moves the army by at least its weight in men.
        // ===================================================================

        [Test]
        public void EveryGateMovesTheDisplayedArmyEvenAtTheSeedMuster()
        {
            // THE REPORTED BUG, as an assertion. "at the first level the adding +1 doesn't
            // add anything, multiplication does." A gate whose sign says it will do something
            // must do something the player can SEE — and what the player sees is
            // StatFormat.Army, which floors under a thousand.
            for (double army = StandingArmy.Seed; army < 60.0; army += 1.0)
                for (int weight = 1; weight <= 3; weight++)
                {
                    double after = GateMath.ApplyGate(army, GateOp.Add, weight, 0);
                    Assert.AreNotEqual(
                        BattleRunner.Core.Stats.StatFormat.Army(army),
                        BattleRunner.Core.Stats.StatFormat.Army(after),
                        $"a weight-{weight} recruit gate did not move the number at {army} men");
                }
        }

        [Test]
        public void TheSignAndTheArmyAlwaysAgree()
        {
            // The two used to be different functions that agreed only above 39 men, and the
            // disagreement was the bug. They are one function now, so this holds everywhere.
            foreach (double army in new[] { 1.0, 5.0, 7.0, 38.0, 39.0, 40.0, 1000.0, 4.2e12 })
                foreach (GateOp op in new[] { GateOp.Add, GateOp.Multiply, GateOp.Subtract })
                    for (int weight = 1; weight <= 3; weight++)
                    {
                        double delta = GateMath.ApplyGate(army, op, weight, 0) - army;
                        long sign = GateMath.Headcount(army, op, weight, 0);
                        Assert.AreEqual(Math.Sign(delta), Math.Sign(sign),
                            $"{op} w{weight} at {army}: army moved {delta}, sign said {sign}");
                        if (Math.Abs(delta) < 1e9)
                            Assert.AreEqual(delta, sign, 1.0,
                                $"{op} w{weight} at {army}: sign and army disagree in magnitude");
                    }
        }

        [Test]
        public void TheFloorIsWorthTheGatesWeightSoAHeavierGateIsStillHeavier()
        {
            // Three identical ticks would be worse than the bug: the player would see the
            // number move and learn that gate size does not matter.
            double one = GateMath.ApplyGate(5.0, GateOp.Add, 1, 0);
            double two = GateMath.ApplyGate(5.0, GateOp.Add, 2, 0);
            double three = GateMath.ApplyGate(5.0, GateOp.Add, 3, 0);
            Assert.AreEqual(6.0, one, 1e-9);
            Assert.AreEqual(7.0, two, 1e-9);
            Assert.AreEqual(8.0, three, 1e-9);
        }

        [Test]
        public void TheFloorStopsBindingAtThirtyNineMenForEveryWeight()
        {
            // The weight cancels — floor and share cross at the same army size whatever the
            // gate weighs — which is why nothing past the opening of the game is touched.
            for (int weight = 1; weight <= 3; weight++)
            {
                double atThirtyEight = GateMath.ApplyGate(38.0, GateOp.Add, weight, 0);
                Assert.AreEqual(38.0 + weight, atThirtyEight, 1e-9,
                    $"the floor should still bind at 38 men for weight {weight}");

                double atForty = GateMath.ApplyGate(40.0, GateOp.Add, weight, 0);
                Assert.AreEqual(40.0 * GateMath.Factor(GateOp.Add, weight, 0), atForty, 1e-9,
                    $"the share should have overtaken the floor at 40 men for weight {weight}");
            }
        }

        [Test]
        public void TheFloorNeverPushesAnArmyPastZero()
        {
            // An army of two meeting a weight-three ambush loses two, not three.
            Assert.AreEqual(0.0, GateMath.ApplyGate(2.0, GateOp.Subtract, 3, 0), 1e-9);
            Assert.AreEqual(0.0, GateMath.ApplyGate(0.0, GateOp.Subtract, 3, 0), 1e-9);
        }

        [Test]
        public void ScaleInvarianceSurvivesTheFloor()
        {
            // The property the whole continuous army rests on. Above the floor a gate is
            // still worth the same ratio at every size.
            double small = GateMath.ApplyGate(500.0, GateOp.Add, 1, 0) / 500.0;
            double huge = GateMath.ApplyGate(500e12, GateOp.Add, 1, 0) / 500e12;
            Assert.AreEqual(small, huge, 1e-9);
        }

        [Test]
        public void NegativeGateWeightThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GateMath.ApplyGate(10.0, GateOp.Add, -1, 0));
        }

        [Test]
        public void RandomizedSequencesMatchReferenceArithmetic()
        {
            var random = new Random(1234);
            for (int trial = 0; trial < 500; trial++)
            {
                double force = random.Next(1, 500);
                double expected = force;
                for (int g = 0; g < 12; g++)
                {
                    var op = (GateOp)random.Next(0, 3);
                    int weight = random.Next(0, 4);
                    int depth = random.Next(0, 20);
                    force = GateMath.ApplyGate(force, op, weight, depth);

                    double factor = op switch
                    {
                        GateOp.Add => 1.0 + GateMath.RecruitShare * weight,
                        GateOp.Multiply => 1.0 + GateMath.RallyShare * weight,
                        _ => 1.0 - Math.Min(GateMath.AmbushShareMax,
                            (GateMath.AmbushShare + GateMath.AmbushDepthStep * depth) * weight)
                    };
                    double moved = expected * factor;
                    // The floor: a gate moves the army by at least its weight in men. The
                    // reference has to model it too, or this test would pin the bug rather
                    // than the behaviour.
                    if (weight > 0)
                    {
                        if (moved > expected) moved = Math.Max(moved, expected + weight);
                        else if (moved < expected) moved = Math.Min(moved, expected - weight);
                    }
                    expected = Math.Max(0.0, moved);
                }
                Assert.AreEqual(expected, force, Math.Abs(expected) * 1e-9,
                    $"trial {trial} diverged");
            }
        }

        [Test]
        public void YieldAmplifiesWhatAGateGainedAndNeverSoftensALoss()
        {
            double plain = GateMath.ApplyGate(100.0, GateOp.Add, 1, 0);
            double yielded = GateMath.ApplyGateWithYield(100.0, GateOp.Add, 1, 0, 0.5);
            Assert.AreEqual((plain - 100.0) * 1.5, yielded - 100.0, 1e-9);

            // Same rule for the arch: the GAIN is scaled, not the factor.
            double rallyPlain = GateMath.ApplyGate(100.0, GateOp.Multiply, 1, 0);
            double rallyYielded = GateMath.ApplyGateWithYield(100.0, GateOp.Multiply, 1, 0, 0.5);
            Assert.AreEqual((rallyPlain - 100.0) * 1.5, rallyYielded - 100.0, 1e-9);

            // A loss is untouched. Yield is a reward, not a shield.
            Assert.AreEqual(GateMath.ApplyGate(100.0, GateOp.Subtract, 1, 0),
                GateMath.ApplyGateWithYield(100.0, GateOp.Subtract, 1, 0, 0.5), 1e-9);

            // And zero yield changes nothing at all, which is what makes it safe to thread
            // through a path every player runs.
            Assert.AreEqual(plain, GateMath.ApplyGateWithYield(100.0, GateOp.Add, 1, 0, 0.0), 1e-12);
        }

        [Test]
        public void SurplusBonusIsOneWithoutGrowthAndThenDiminishes()
        {
            Assert.AreEqual(1f, GateMath.SurplusToBonusMultiplier(100.0, 100.0));
            Assert.AreEqual(1f, GateMath.SurplusToBonusMultiplier(50.0, 100.0),
                "a round that lost ground must not pay a bonus");

            float small = GateMath.SurplusToBonusMultiplier(200.0, 100.0);
            float large = GateMath.SurplusToBonusMultiplier(1600.0, 100.0);
            Assert.Greater(small, 1f);
            Assert.Greater(large, small);
            Assert.Less(large - small, (small - 1f) * 7f, "per-doubling gain must diminish");
        }
    }
}
