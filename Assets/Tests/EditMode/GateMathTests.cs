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
    }
}
