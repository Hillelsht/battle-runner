using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// How an army reads once it stops fitting on a phone. The campaign spans five men to
    /// twenty trillion, so this is on the HUD every frame of the game.
    /// </summary>
    [TestFixture]
    public class ArmyFormatTests
    {
        [Test]
        public void SmallArmiesAreExactBecauseThePlayerCanCountThem()
        {
            Assert.AreEqual("5", StatFormat.Army(5));
            Assert.AreEqual("47", StatFormat.Army(47));
            Assert.AreEqual("999", StatFormat.Army(999.9));
            Assert.AreEqual("0", StatFormat.Army(0));
            Assert.AreEqual("0", StatFormat.Army(-12), "a negative army is a bug, not a display");
        }

        [Test]
        public void LargeArmiesCarryThreeSignificantFiguresAndASuffix()
        {
            Assert.AreEqual("1.00K", StatFormat.Army(1000));
            Assert.AreEqual("12.3K", StatFormat.Army(12_345));
            Assert.AreEqual("999K", StatFormat.Army(999_400));
            Assert.AreEqual("3.91B", StatFormat.Army(3_912_000_000.0));
            Assert.AreEqual("20.1T", StatFormat.Army(20_094_331_776_412.0));
        }

        [Test]
        public void ATierIsNeverPrintedAsAThousandOfTheTierBelow()
        {
            // "1000K" would be a tier the loop has already passed, and reads as a bug.
            for (double v = 1.0; v < 1e30; v *= 1.37)
                Assert.IsFalse(StatFormat.Army(v).StartsWith("1000"),
                    $"{v} rendered as {StatFormat.Army(v)}");
        }

        [Test]
        public void RunningOffTheEndOfTheTableFallsBackRatherThanLying()
        {
            // Perfect lane choice over sixty-two rounds reaches about 10^46, which is past
            // Decillion. A bare number there would be unreadable in exactly the place it is
            // least readable; scientific notation at least states the magnitude.
            string huge = StatFormat.Army(1e40);
            Assert.IsNotEmpty(huge);
            Assert.IsFalse(huge.Contains("NaN"));
        }

        [Test]
        public void AHeadcountCarriesItsOwnSign()
        {
            Assert.AreEqual("+340", StatFormat.Headcount(340));
            Assert.AreEqual("-1.30K", StatFormat.Headcount(-1300));
            Assert.AreEqual("0", StatFormat.Headcount(0));
        }

        [Test]
        public void AGatesSignNeverReadsAsNothingWhenItIsWorthSomething()
        {
            // A gate showing "+0" in front of a player about to gain from it is a lie about
            // the mechanic, and at the seed muster a 2.6% share is a quarter of a man.
            for (double army = StandingArmy.Seed; army < 1e12; army *= 3.3)
            {
                Assert.AreNotEqual("0", StatFormat.Headcount(
                    GateMath.Headcount(army, GateOp.Add, 1, 0)), $"recruit sign at {army}");
                Assert.AreNotEqual("0", StatFormat.Headcount(
                    GateMath.Headcount(army, GateOp.Subtract, 1, 0)), $"ambush sign at {army}");
            }
        }
    }
}
