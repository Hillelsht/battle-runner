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
                    expected = Math.Max(0.0, expected * factor);
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
