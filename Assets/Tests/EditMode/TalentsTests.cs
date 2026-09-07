using BattleRunner.Core.Crowd;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The nine mechanics the tree hooks into. Every one is pinned at ZERO first: a player
    /// who has not taken the talent must get the behaviour the game had before it existed,
    /// which is what makes it safe to run these through paths every run already takes.
    /// </summary>
    [TestFixture]
    public class TalentsTests
    {
        private const long Cap = 100_000L;

        // --- rolls -------------------------------------------------------------

        [Test]
        public void ZeroChanceNeverFiresEvenOnAZeroRoll()
        {
            Assert.IsFalse(Talents.Rolls(0f, 0f));
            Assert.IsFalse(Talents.Rolls(-1f, 0f));
        }

        [Test]
        public void ARollBelowTheChanceHitsAndOneAtItMisses()
        {
            Assert.IsTrue(Talents.Rolls(0.25f, 0.2499f));
            Assert.IsFalse(Talents.Rolls(0.25f, 0.25f), "Random.value can return exactly the bound");
            Assert.IsTrue(Talents.Rolls(1f, 0.999f));
        }

        // --- chain -------------------------------------------------------------

        [Test]
        public void TheFirstMultiplyOfAChainPaysNothingExtra()
        {
            Assert.AreEqual(0f, Talents.ChainYield(0, 0.5f));
        }

        [Test]
        public void ChainYieldGrowsPerLinkAndThenStops()
        {
            Assert.AreEqual(0.5f, Talents.ChainYield(1, 0.5f), 1e-5f);
            Assert.AreEqual(1.5f, Talents.ChainYield(3, 0.5f), 1e-5f);
            Assert.AreEqual(Talents.MaxChainLength * 0.5f, Talents.ChainYield(50, 0.5f), 1e-5f,
                "an unbounded chain compounds on top of an operator that already compounds");
        }

        [Test]
        public void ChainYieldIsInertWithoutTheTalent()
        {
            Assert.AreEqual(0f, Talents.ChainYield(9, 0f));
        }

        // --- gates -------------------------------------------------------------

        [Test]
        public void AGateWithNoTalentsMatchesThePlainArithmetic()
        {
            long plain = GateMath.ApplyGate(40L, GateOp.Multiply, 3, Cap, out long plainOver);
            long talented = Talents.ApplyGate(40L, GateOp.Multiply, 3, Cap, 0f, 0f, false, out long over);
            Assert.AreEqual(plain, talented);
            Assert.AreEqual(plainOver, over);
        }

        [Test]
        public void ACriticalDoublesTheGainNotThePrintedValue()
        {
            // x3 on 40 gains 80. Doubled, it gains 160, so it lands on 200 — not on 240,
            // which is what doubling the multiplier would have given.
            long crit = Talents.ApplyGate(40L, GateOp.Multiply, 3, Cap, 0f, 0f, true, out _);
            Assert.AreEqual(200L, crit);

            // And the same rule reads correctly for an add: +10 gains 10, so it gains 20.
            Assert.AreEqual(60L, Talents.ApplyGate(40L, GateOp.Add, 10, Cap, 0f, 0f, true, out _));
        }

        [Test]
        public void ACriticalStacksMultiplicativelyOnTopOfYield()
        {
            // 50% yield turns a gain of 80 into 120; the crit then doubles it to 240.
            Assert.AreEqual(280L, Talents.ApplyGate(40L, GateOp.Multiply, 3, Cap, 0.5f, 0f, true, out _));
        }

        [Test]
        public void ACriticalNeverSoftensALoss()
        {
            long normal = Talents.ApplyGate(100L, GateOp.Subtract, 30, Cap, 0f, 0f, false, out _);
            long crit = Talents.ApplyGate(100L, GateOp.Subtract, 30, Cap, 2f, 3f, true, out _);
            Assert.AreEqual(70L, normal);
            Assert.AreEqual(normal, crit, "yield and crits are rewards, not shields");
        }

        [Test]
        public void ChainYieldAndGateYieldAddBeforeTheyAmplify()
        {
            long a = Talents.ApplyGate(40L, GateOp.Multiply, 3, Cap, 0.25f, 0.25f, false, out _);
            long b = Talents.ApplyGate(40L, GateOp.Multiply, 3, Cap, 0.50f, 0.00f, false, out _);
            Assert.AreEqual(b, a);
        }

        [Test]
        public void EverythingAboveTheCapStillBecomesOverflow()
        {
            long result = Talents.ApplyGate(80_000L, GateOp.Multiply, 4, Cap, 1f, 1f, true, out long over);
            Assert.AreEqual(Cap, result);
            Assert.Greater(over, 0L);
        }

        // --- magnetism ---------------------------------------------------------

        [Test]
        public void OnlyGainsAreWorthBeingPulledToward()
        {
            Assert.IsTrue(Talents.IsBeneficial(GateOp.Add, 5));
            Assert.IsTrue(Talents.IsBeneficial(GateOp.Multiply, 2));
            Assert.IsFalse(Talents.IsBeneficial(GateOp.Subtract, 5));
            Assert.IsFalse(Talents.IsBeneficial(GateOp.Multiply, 1), "x1 is not a gain");
            Assert.IsFalse(Talents.IsBeneficial(GateOp.Multiply, 0), "x0 is a wipe");
            Assert.IsFalse(Talents.IsBeneficial(GateOp.Add, 0));
        }

        [Test]
        public void WithoutMagnetismReachIsExactlyTheOccupiedLane()
        {
            const float w = 2.2f;
            for (float x = -3.3f; x <= 3.3f; x += 0.05f)
                for (int lane = -1; lane <= 1; lane++)
                    Assert.AreEqual(CrowdMath.LaneIndex(x, w) == lane,
                        CrowdMath.LaneReaches(x, lane, w, 0f), $"x={x} lane={lane}");
        }

        [Test]
        public void MagnetismOnlyEverAddsANeighbour()
        {
            const float w = 2.2f;
            // Standing dead centre, a neighbour's centre is a full lane away — further than
            // half a lane plus half a metre, so the talent grants nothing for free.
            Assert.IsFalse(CrowdMath.LaneReaches(0f, 1, w, 0.5f));
            // Drifting 0.6 m toward it closes the gap to exactly the widened edge.
            Assert.IsTrue(CrowdMath.LaneReaches(0.6f, 1, w, 0.5f));
            // And the lane actually occupied never stops counting.
            Assert.IsTrue(CrowdMath.LaneReaches(0.6f, 0, w, 0.5f));
        }

        // --- packs -------------------------------------------------------------

        [Test]
        public void AShatteredPackCostsNothing()
        {
            Assert.AreEqual(0L, Talents.PackBite(400, 0f, true));
        }

        [Test]
        public void ResistIsCappedSoAPackAlwaysBites()
        {
            Assert.AreEqual(400L, Talents.PackBite(400, 0f, false));
            Assert.AreEqual(200L, Talents.PackBite(400, 0.5f, false));
            Assert.AreEqual(60L, Talents.PackBite(400, 0.99f, false), "resist caps at 85%");
            Assert.AreEqual(1L, Talents.PackBite(1, 0.99f, false));
        }

        // --- overflow ----------------------------------------------------------

        [Test]
        public void OverflowWithoutBankingMatchesTheOriginalCurve()
        {
            Assert.AreEqual(GateMath.OverflowToBonusMultiplier(50_000L, Cap),
                Talents.OverflowMultiplier(50_000L, Cap, 0f), 1e-6f);
            Assert.AreEqual(1f, Talents.OverflowMultiplier(0L, Cap, 5f));
        }

        [Test]
        public void BankingScalesTheBonusAndNotTheBaseline()
        {
            float plain = Talents.OverflowMultiplier(Cap, Cap, 0f);
            float banked = Talents.OverflowMultiplier(Cap, Cap, 1f);
            Assert.AreEqual(1.25f, plain, 1e-5f, "one cap of overflow is +25%");
            Assert.AreEqual(1.50f, banked, 1e-5f, "doubling the bonus, never the whole multiplier");
        }

        // --- execute, second wind, reflect --------------------------------------

        [Test]
        public void ExecuteNeedsTheTalentAndALivingBoss()
        {
            Assert.IsFalse(Talents.Executes(5f, 100f, 0f));
            Assert.IsTrue(Talents.Executes(5f, 100f, 0.08f));
            Assert.IsFalse(Talents.Executes(9f, 100f, 0.08f));
            Assert.IsTrue(Talents.Executes(8f, 100f, 0.08f), "the threshold is inclusive");
            Assert.IsFalse(Talents.Executes(0f, 100f, 0.5f), "a dead boss is already dead");
            Assert.IsFalse(Talents.Executes(5f, 0f, 0.5f));
        }

        [Test]
        public void SecondWindIsNothingWithoutTheTalent()
        {
            Assert.AreEqual(0L, Talents.SecondWindForce(800L, 0f));
        }

        [Test]
        public void SecondWindRevivesAgainstParAndNeverBelowAFloor()
        {
            Assert.AreEqual(200L, Talents.SecondWindForce(800L, 0.25f));
            Assert.AreEqual(10L, Talents.SecondWindForce(4L, 0.25f), "a token army is not a comeback");
            Assert.AreEqual(10L, Talents.SecondWindForce(0L, 0.25f));
        }

        [Test]
        public void ReflectIsInertWithoutTheTalent()
        {
            Assert.AreEqual(0f, Talents.ReflectedDamage(500f, 0f));
            Assert.AreEqual(150f, Talents.ReflectedDamage(500f, 0.3f), 1e-4f);
        }

        // --- the whole chain, as the runner phase drives it ----------------------

        [Test]
        public void AFourGateMultiplyChainOutRunsFourLoneMultiplies()
        {
            const float chainStat = 0.6f;
            long chained = 10L;
            long lone = 10L;
            for (int i = 0; i < 4; i++)
            {
                chained = Talents.ApplyGate(chained, GateOp.Multiply, 2, Cap, 0f,
                    Talents.ChainYield(i, chainStat), false, out _);
                lone = Talents.ApplyGate(lone, GateOp.Multiply, 2, Cap, 0f, 0f, false, out _);
            }
            Assert.AreEqual(160L, lone);
            Assert.Greater(chained, lone * 3, "the whole point of a chain is that it compounds");
        }

        [Test]
        public void OverflowBankReachesTheBossThroughTheRunResult()
        {
            StatSheet stats = StatSheet.Resolve(
                new System.Collections.Generic.Dictionary<string, float>(),
                new[] { new StatModifier(StatIds.OverflowBank, ModifierKind.Flat, 1f) });
            var result = new RunResult { OverflowAccumulated = Cap, HeroStats = stats };
            Assert.AreEqual(1.50f, result.OverflowBonus(Cap), 1e-5f);

            var bare = new RunResult { OverflowAccumulated = Cap, HeroStats = null };
            Assert.AreEqual(1.25f, bare.OverflowBonus(Cap), 1e-5f);
        }
    }
}
