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
            double plain = GateMath.ApplyGate(40.0, GateOp.Multiply, 2, 0);
            double talented = Talents.ApplyGate(40.0, GateOp.Multiply, 2, 0, 0f, 0f, false);
            Assert.AreEqual(plain, talented, 1e-12);
        }

        [Test]
        public void ACriticalDoublesTheGainNotThePrintedValue()
        {
            // A crit doubles the GAIN, which is the only definition that behaves the same
            // for both operators. It reads as a ratio now rather than as round numbers,
            // because the gate itself is a ratio.
            double gain = GateMath.ApplyGate(40.0, GateOp.Multiply, 2, 0) - 40.0;
            Assert.AreEqual(40.0 + gain * 2.0,
                Talents.ApplyGate(40.0, GateOp.Multiply, 2, 0, 0f, 0f, true), 1e-9);

            double addGain = GateMath.ApplyGate(40.0, GateOp.Add, 1, 0) - 40.0;
            Assert.AreEqual(40.0 + addGain * 2.0,
                Talents.ApplyGate(40.0, GateOp.Add, 1, 0, 0f, 0f, true), 1e-9);
        }

        [Test]
        public void ACriticalStacksMultiplicativelyOnTopOfYield()
        {
            // 50% yield lifts the gain by half; the crit then doubles what is left.
            double gain = GateMath.ApplyGate(40.0, GateOp.Multiply, 2, 0) - 40.0;
            Assert.AreEqual(40.0 + gain * 3.0,
                Talents.ApplyGate(40.0, GateOp.Multiply, 2, 0, 0.5f, 0f, true), 1e-9);
        }

        [Test]
        public void ACriticalNeverSoftensALoss()
        {
            double normal = Talents.ApplyGate(100.0, GateOp.Subtract, 1, 0, 0f, 0f, false);
            double crit = Talents.ApplyGate(100.0, GateOp.Subtract, 1, 0, 2f, 3f, true);
            Assert.Less(normal, 100.0);
            Assert.AreEqual(normal, crit, 1e-9, "yield and crits are rewards, not shields");
        }

        [Test]
        public void ChainYieldAndGateYieldAddBeforeTheyAmplify()
        {
            double a = Talents.ApplyGate(40.0, GateOp.Multiply, 2, 0, 0.25f, 0.25f, false);
            double b = Talents.ApplyGate(40.0, GateOp.Multiply, 2, 0, 0.50f, 0.00f, false);
            Assert.AreEqual(b, a, 1e-9);
        }

        /// <summary>
        /// REPLACES EverythingAboveTheCapStillBecomesOverflow, which asserted that a big
        /// enough gate chain pinned the army at 100,000 and spilled the rest. There is no
        /// cap any more — it was the single thing that made carrying the army between rounds
        /// unplayable, pinning a player flat by round five — so the property worth pinning
        /// in its place is that nothing runs away without one.
        /// </summary>
        [Test]
        public void NothingRunsAwayNowThatThereIsNoCapToStopIt()
        {
            double force = 5.0;
            for (int i = 0; i < 400; i++)
                force = Talents.ApplyGate(force, GateOp.Multiply, 3, 0, 1f, 1f, true);
            Assert.IsFalse(double.IsInfinity(force), "a gate chain must stay a finite number");
            Assert.Greater(force, 0.0);
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
            Assert.AreEqual(0.0, Talents.PackBite(400.0, 2, 0, 0f, true));
        }

        [Test]
        public void ResistIsCappedSoAPackAlwaysBites()
        {
            double full = Talents.PackBite(400.0, 2, 0, 0f, false);
            Assert.Greater(full, 0.0);
            Assert.AreEqual(full * 0.5, Talents.PackBite(400.0, 2, 0, 0.5f, false), 1e-9);
            Assert.AreEqual(full * 0.15, Talents.PackBite(400.0, 2, 0, 0.99f, false), full * 1e-6,
                "resist caps at 85%");
            // A pack is a SHARE now, so it scales with the army instead of rounding away:
            // the old floor of one man existed because an absolute cost of three against an
            // army of four hundred had already stopped meaning anything.
            Assert.AreEqual(full * 1000.0, Talents.PackBite(400_000.0, 2, 0, 0f, false),
                full * 1000.0 * 1e-9);
        }

        // --- overflow ----------------------------------------------------------

        [Test]
        public void OverflowWithoutBankingMatchesTheOriginalCurve()
        {
            Assert.AreEqual(GateMath.SurplusToBonusMultiplier(400.0, 100.0),
                Talents.SurplusMultiplier(400.0, 100.0, 0f), 1e-6f);
            Assert.AreEqual(1f, Talents.SurplusMultiplier(100.0, 100.0, 5f));
        }

        [Test]
        public void BankingScalesTheBonusAndNotTheBaseline()
        {
            float plain = Talents.SurplusMultiplier(200.0, 100.0, 0f);
            float banked = Talents.SurplusMultiplier(200.0, 100.0, 1f);
            Assert.AreEqual(1.25f, plain, 1e-5f, "doubling the army over a round is +25%");
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
        public void SecondWindRevivesAgainstTheArmyAndNeverBelowTheSeed()
        {
            // AGAINST THE ARMY THAT WALKED IN, not against par. Par is a share of the best
            // line through the round and, measured against the shipped generator, it falls
            // below 1.0 from about round twenty — a revive sized off it would have handed a
            // deep-run player fewer men than they started with, which is the opposite of a
            // comeback. The floor is the seed muster rather than a hard-coded ten.
            Assert.AreEqual(200.0, Talents.SecondWindForce(800.0, 0.25f), 1e-9);
            Assert.AreEqual(StandingArmy.Seed, Talents.SecondWindForce(4.0, 0.25f), 1e-9,
                "a token army is not a comeback");
            Assert.AreEqual(StandingArmy.Seed, Talents.SecondWindForce(0.0, 0.25f), 1e-9);
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
            double chained = 10.0;
            double lone = 10.0;
            for (int i = 0; i < 4; i++)
            {
                chained = Talents.ApplyGate(chained, GateOp.Multiply, 2, 0, 0f,
                    Talents.ChainYield(i, chainStat), false);
                lone = Talents.ApplyGate(lone, GateOp.Multiply, 2, 0, 0f, 0f, false);
            }
            Assert.Greater(chained, lone * 1.5, "the whole point of a chain is that it compounds");
        }

        [Test]
        public void SurplusBankReachesTheBossThroughTheRunResult()
        {
            StatSheet stats = StatSheet.Resolve(
                new System.Collections.Generic.Dictionary<string, float>(),
                new[] { new StatModifier(StatIds.OverflowBank, ModifierKind.Flat, 1f) });
            var result = new RunResult
            {
                StartingForceCount = 100.0, FinalForceCount = 200.0, HeroStats = stats
            };
            Assert.AreEqual(1.50f, result.SurplusBonus(), 1e-5f);

            var bare = new RunResult
            {
                StartingForceCount = 100.0, FinalForceCount = 200.0, HeroStats = null
            };
            Assert.AreEqual(1.25f, bare.SurplusBonus(), 1e-5f);
        }
    }
}
