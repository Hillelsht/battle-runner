using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The clash. What is pinned here is that adding a second of battle animation did not
    /// quietly change the game: the outcome of a fight must be exactly what `force -= cost`
    /// produced before, because the talent maths, the level pacing and every tuned difficulty
    /// number in the project are built on it.
    /// </summary>
    [TestFixture]
    public class MeleeTests
    {
        [Test]
        public void TheBiggerCrowdWinsAndKeepsTheDifference()
        {
            var clash = new Melee(100, 30);
            Assert.IsTrue(clash.AlliesWin);
            Assert.AreEqual(70, clash.SurvivingAllies);
            Assert.AreEqual(0, clash.SurvivingEnemies);

            var losing = new Melee(30, 100);
            Assert.IsFalse(losing.AlliesWin);
            Assert.AreEqual(0, losing.SurvivingAllies);
            Assert.AreEqual(70, losing.SurvivingEnemies);
        }

        [Test]
        public void TheOutcomeIsExactlyTheSubtractionItReplaced()
        {
            // This is the whole safety property. A clash is a PACING change, not a balance
            // change: every tuned number in the game assumes taking a -N pack costs N.
            foreach (long force in new long[] { 1, 7, 40, 512, 100000 })
            foreach (long cost in new long[] { 1, 5, 39, 40, 41, 999 })
            {
                long expected = force - cost > 0 ? force - cost : 0;
                Assert.AreEqual(expected, new Melee(force, cost).SurvivingAllies,
                    $"{force} against {cost}");
            }
        }

        [Test]
        public void AnEvenClashAnnihilatesBothSides()
        {
            var clash = new Melee(64, 64);
            Assert.AreEqual(0, clash.SurvivingAllies);
            Assert.AreEqual(0, clash.SurvivingEnemies);
            Assert.IsFalse(clash.AlliesWin, "a draw is not a win");
        }

        [Test]
        public void BothSidesDrainMonotonicallyAndLandExactlyOnTheirSurvivors()
        {
            // A count that dips below its own final total and comes back up is a crowd that
            // visibly flickers at the end of every single fight.
            var clash = new Melee(90, 35);
            long previousAllies = long.MaxValue, previousEnemies = long.MaxValue;
            for (int step = 0; step <= 40; step++)
            {
                float t = Melee.Duration * step / 40f;
                clash.At(t, out long allies, out long enemies);

                Assert.LessOrEqual(allies, previousAllies, $"allies grew at t={t}");
                Assert.LessOrEqual(enemies, previousEnemies, $"enemies grew at t={t}");
                Assert.GreaterOrEqual(allies, clash.SurvivingAllies, $"allies undershot at t={t}");
                Assert.GreaterOrEqual(enemies, clash.SurvivingEnemies, $"enemies undershot at t={t}");
                Assert.LessOrEqual(allies, clash.Allies, $"allies overshot at t={t}");

                previousAllies = allies;
                previousEnemies = enemies;
            }

            clash.At(0f, out long a0, out long e0);
            Assert.AreEqual(90, a0);
            Assert.AreEqual(35, e0);
            clash.At(Melee.Duration, out long a1, out long e1);
            Assert.AreEqual(clash.SurvivingAllies, a1);
            Assert.AreEqual(0, e1);
        }

        [Test]
        public void TheLosingSideIsStillStandingUntilTheClashEnds()
        {
            // An enemy squad that vanishes at 80% leaves the player's soldiers swinging at
            // air for the last quarter second, which reads as the animation being broken.
            var clash = new Melee(500, 12);
            clash.At(Melee.Duration * 0.85f, out _, out long enemies);
            Assert.Greater(enemies, 0, "the enemy was gone before the fight was");
        }

        [Test]
        public void TimeOutsideTheClashIsClampedRatherThanExtrapolated()
        {
            var clash = new Melee(50, 20);
            clash.At(-5f, out long a, out long e);
            Assert.AreEqual(50, a);
            Assert.AreEqual(20, e);
            clash.At(999f, out a, out e);
            Assert.AreEqual(30, a);
            Assert.AreEqual(0, e);
        }

        [Test]
        public void ADetachmentIsSentRatherThanTheWholeArmy()
        {
            // Five hundred men do not run at nine skeletons, and if they did the formation
            // this whole game is about would dissolve every time a pack arrived.
            var clash = new Melee(500, 9);
            int fighters = clash.Fighters(400);
            Assert.LessOrEqual(fighters, Melee.MaxFighters);
            Assert.LessOrEqual(fighters, 18, "sending more than two per enemy is a crowd, not a fight");
            Assert.Greater(fighters, 0);

            // Never more than are actually on the field.
            Assert.LessOrEqual(new Melee(500, 200).Fighters(6), 6);

            // And never zero while there is someone to fight — a clash with no visible
            // fighters is precisely the bug this class exists to remove.
            Assert.AreEqual(1, new Melee(1, 1).Fighters(1));
            Assert.Greater(new Melee(3, 1).Fighters(3), 0);
        }

        [Test]
        public void NothingToFightMeansNobodyLeavesFormation()
        {
            Assert.AreEqual(0, new Melee(100, 0).Fighters(100));
            Assert.AreEqual(0, new Melee(0, 10).Fighters(0));
        }

        [Test]
        public void NegativeCountsAreClampedRatherThanPropagated()
        {
            // A subtract gate that overshoots, or a pack built from bad content, must not put
            // a negative count into the renderer's instance loop.
            var clash = new Melee(-5, -9);
            Assert.AreEqual(0, clash.Allies);
            Assert.AreEqual(0, clash.Enemies);
            clash.At(Melee.Duration * 0.5f, out long a, out long e);
            Assert.AreEqual(0, a);
            Assert.AreEqual(0, e);
        }
    }
}
