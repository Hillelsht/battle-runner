using System;
using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class GateMathTests
    {
        private const long Cap = 100_000;

        [Test]
        public void Add_IncreasesForce()
        {
            Assert.AreEqual(15, GateMath.ApplyGate(10, GateOp.Add, 5, Cap, out long overflow));
            Assert.AreEqual(0, overflow);
        }

        [Test]
        public void Multiply_ScalesForce()
        {
            Assert.AreEqual(30, GateMath.ApplyGate(10, GateOp.Multiply, 3, Cap, out _));
        }

        [Test]
        public void Subtract_NeverGoesBelowZero()
        {
            Assert.AreEqual(0, GateMath.ApplyGate(10, GateOp.Subtract, 25, Cap, out _));
        }

        [Test]
        public void MultiplyByZero_IsZero()
        {
            Assert.AreEqual(0, GateMath.ApplyGate(10, GateOp.Multiply, 0, Cap, out _));
        }

        [Test]
        public void SoftCap_ConvertsExcessToOverflow()
        {
            long force = GateMath.ApplyGate(60_000, GateOp.Multiply, 3, Cap, out long overflow);
            Assert.AreEqual(Cap, force);
            Assert.AreEqual(80_000, overflow);
        }

        [Test]
        public void MultiplyChain_NeverOverflowsLong()
        {
            long force = 2;
            for (int i = 0; i < 80; i++)
                force = GateMath.ApplyGate(force, GateOp.Multiply, 1000, long.MaxValue - 1, out _);
            Assert.Greater(force, 0, "saturating multiply must never wrap negative");
        }

        [Test]
        public void RandomizedSequences_MatchReferenceArithmetic()
        {
            var random = new Random(1234);
            for (int trial = 0; trial < 500; trial++)
            {
                long force = random.Next(0, 500);
                long expected = force;
                long totalOverflow = 0;
                for (int g = 0; g < 12; g++)
                {
                    var op = (GateOp)random.Next(0, 3);
                    int value = op == GateOp.Multiply ? random.Next(0, 5) : random.Next(0, 200);
                    force = GateMath.ApplyGate(force, op, value, Cap, out long overflow);
                    totalOverflow += overflow;

                    expected = op switch
                    {
                        GateOp.Add => expected + value,
                        GateOp.Multiply => expected * value,
                        _ => Math.Max(0, expected - value)
                    };
                    if (expected > Cap) expected = Cap; // reference clamps too; overflow tracked separately
                }
                Assert.AreEqual(expected, force, $"trial {trial} diverged");
                Assert.GreaterOrEqual(totalOverflow, 0);
            }
        }

        [Test]
        public void OverflowBonus_IsOneWithoutOverflow_AndGrowsDiminishingly()
        {
            Assert.AreEqual(1f, GateMath.OverflowToBonusMultiplier(0, Cap));
            float small = GateMath.OverflowToBonusMultiplier(Cap, Cap);
            float large = GateMath.OverflowToBonusMultiplier(Cap * 8, Cap);
            Assert.Greater(small, 1f);
            Assert.Greater(large, small);
            float gainSmall = small - 1f;
            float gainLarge = large - small;
            Assert.Less(gainLarge / 7f, gainSmall, "per-overflow gain must diminish");
        }

        [Test]
        public void NegativeGateValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GateMath.ApplyGate(10, GateOp.Add, -1, Cap, out _));
        }

        // --- Gate yield (skill tree) --------------------------------------------------

        [Test]
        public void GateYield_AmplifiesWhatAnAddGatePaid()
        {
            long result = GateMath.ApplyGateWithYield(100, GateOp.Add, 10, 100_000, 0.20f, out _);
            Assert.AreEqual(112, result, "gained 10, +20% yield = 12");
        }

        [Test]
        public void GateYield_AmplifiesAMultiplierByTheSameRule()
        {
            // x2 on 50 gains 50; +20% of the GAIN is 60, not 120.
            long result = GateMath.ApplyGateWithYield(50, GateOp.Multiply, 2, 100_000, 0.20f, out _);
            Assert.AreEqual(110, result);
        }

        [Test]
        public void GateYield_NeverSoftensALoss()
        {
            long withYield = GateMath.ApplyGateWithYield(100, GateOp.Subtract, 30, 100_000, 0.50f, out _);
            long without = GateMath.ApplyGate(100, GateOp.Subtract, 30, 100_000, out _);
            Assert.AreEqual(without, withYield, "yield is a reward, not a shield");
        }

        [Test]
        public void GateYield_OfZeroChangesNothing()
        {
            foreach (GateOp op in new[] { GateOp.Add, GateOp.Multiply, GateOp.Subtract })
            {
                long plain = GateMath.ApplyGate(250, op, 7, 100_000, out long o1);
                long yielded = GateMath.ApplyGateWithYield(250, op, 7, 100_000, 0f, out long o2);
                Assert.AreEqual(plain, yielded, $"{op} drifted at zero yield");
                Assert.AreEqual(o1, o2);
            }
        }

        [Test]
        public void GateYield_StillRespectsTheSoftCap()
        {
            long result = GateMath.ApplyGateWithYield(99_000, GateOp.Add, 5_000, 100_000, 1.0f,
                out long overflow);
            Assert.AreEqual(100_000, result, "the cap holds however generous the yield");
            Assert.Greater(overflow, 0, "everything past the cap becomes overflow");
        }
    }
}
