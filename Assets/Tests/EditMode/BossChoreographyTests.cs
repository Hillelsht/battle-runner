using BattleRunner.Core.Feel;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The boss's body language. One property here is load-bearing on FAIRNESS rather than on
    /// looks: the wind-up has to peak BEFORE the blow, because it is the only warning the
    /// player gets and a warning that arrives with the hit is not a warning.
    /// </summary>
    [TestFixture]
    public class BossChoreographyTests
    {
        [Test]
        public void TheWindUpRearsBackAndTheStrikeDrivesForward()
        {
            // Pulling AWAY from the player is what makes the release read as coming at them.
            BossPose calm = BossChoreography.Pose(0f, -1f, -1f, 0f);
            BossPose wound = BossChoreography.Pose(1f, -1f, -1f, 0f);
            Assert.Less(wound.Lean, calm.Lean - 8f, "a full wind-up barely moves");
            Assert.Greater(wound.Surge, calm.Surge, "the boss does not pull back at all");

            BossPose striking = BossChoreography.Pose(0f, BossChoreography.StrikeSeconds * 0.2f,
                -1f, 0f);
            Assert.Greater(striking.Lean, calm.Lean + 15f, "the strike does not drive forward");
            Assert.Less(striking.Surge, calm.Surge - 1.5f, "the boss never closes the gap");
        }

        [Test]
        public void TheStrikeIsFastInAndSlowOut()
        {
            // An attack that returns as fast as it arrives has no weight.
            float peak = BossChoreography.StrikeSeconds * 0.2f;
            BossPose atPeak = BossChoreography.Pose(0f, peak, -1f, 0f);
            BossPose quarterBefore = BossChoreography.Pose(0f, peak * 0.5f, -1f, 0f);
            BossPose sameGapAfter = BossChoreography.Pose(0f, peak + peak * 0.5f, -1f, 0f);

            Assert.Greater(atPeak.Lean, quarterBefore.Lean);
            Assert.Greater(atPeak.Lean, sameGapAfter.Lean);
            // The same distance either side of the peak must NOT be symmetric.
            float rise = atPeak.Lean - quarterBefore.Lean;
            float fall = atPeak.Lean - sameGapAfter.Lean;
            Assert.Greater(rise, fall * 1.5f, "the strike is symmetric, so it has no snap");
        }

        [Test]
        public void TheBossIsNeverCompletelyStill()
        {
            // A boss that is perfectly still between attacks is a prop. The difference
            // between a prop and a creature is mostly that a creature is always doing
            // something small.
            bool moved = false;
            BossPose first = BossChoreography.Pose(0f, -1f, -1f, 0f);
            for (int i = 1; i <= 60; i++)
            {
                BossPose later = BossChoreography.Pose(0f, -1f, -1f, i * 0.1f);
                if (System.Math.Abs(later.Lean - first.Lean) > 0.3f
                    || System.Math.Abs(later.Twist - first.Twist) > 0.5f)
                {
                    moved = true;
                    break;
                }
            }
            Assert.IsTrue(moved, "the boss stands perfectly still at rest");
        }

        [Test]
        public void ARecoilPullsBackAndRecoversWithinItsOwnWindow()
        {
            BossPose calm = BossChoreography.Pose(0f, -1f, -1f, 0f);
            BossPose hit = BossChoreography.Pose(0f, -1f, 0f, 0f);
            Assert.Less(hit.Lean, calm.Lean - 5f, "a hit does not visibly land");
            Assert.Less(hit.Scale, calm.Scale, "the boss does not flinch");

            BossPose recovered = BossChoreography.Pose(0f, -1f, BossChoreography.RecoilSeconds, 0f);
            Assert.AreEqual(calm.Lean, recovered.Lean, 0.01f, "the recoil never ends");
            Assert.AreEqual(calm.Scale, recovered.Scale, 0.001f);
        }

        [Test]
        public void EventsThatHaveNotHappenedContributeNothing()
        {
            // sinceBlow and sinceHit are negative until the event occurs, and a negative
            // must not extrapolate the curve backwards into a permanent pose.
            BossPose none = BossChoreography.Pose(0f, -99f, -99f, 1.7f);
            BossPose alsoNone = BossChoreography.Pose(0f, -1f, -1f, 1.7f);
            Assert.AreEqual(alsoNone.Lean, none.Lean, 1e-4f);
            Assert.AreEqual(alsoNone.Surge, none.Surge, 1e-4f);
            Assert.AreEqual(alsoNone.Scale, none.Scale, 1e-6f);
        }

        [Test]
        public void ThePoseNeverInvertsOrCollapsesTheBoss()
        {
            // Scale multiplies a 6x body scale. A value at or below zero turns the boss
            // inside out, and every combination here can co-occur: wound up, mid-strike and
            // freshly hit is an ordinary frame of a hard fight.
            for (float wind = 0f; wind <= 1f; wind += 0.25f)
            for (float blow = 0f; blow < BossChoreography.StrikeSeconds; blow += 0.05f)
            for (float hit = 0f; hit < BossChoreography.RecoilSeconds; hit += 0.05f)
            {
                BossPose pose = BossChoreography.Pose(wind, blow, hit, blow * 7f);
                Assert.Greater(pose.Scale, 0.5f, $"wind {wind} blow {blow} hit {hit}");
                Assert.Less(pose.Scale, 1.5f, $"wind {wind} blow {blow} hit {hit}");
                Assert.Less(System.Math.Abs(pose.Lean), 90f, "the boss went past horizontal");
                Assert.Less(System.Math.Abs(pose.Surge), 6f, "the boss left the arena");
            }
        }

        [Test]
        public void TheArmyPressesForwardAndIsDrivenBackByAnUnblockedBlow()
        {
            // The army never moved during a boss fight, which is half of why the encounter
            // read as two objects near each other rather than as a battle.
            Assert.AreEqual(0f, BossChoreography.ArmyAdvance(0f, -1f, false), 1e-4f);
            float early = BossChoreography.ArmyAdvance(2f, -1f, false);
            float late = BossChoreography.ArmyAdvance(20f, -1f, false);
            Assert.Greater(late, early, "the army never closes on the boss");
            // DELIBERATELY RAISED, 3.3 -> MaxPress + a margin. The old bound was a guard
            // against the army walking into a boss standing at +16 m; the boss now stands at
            // +11 and the press is 4.5, so the gap still never closes below about six metres
            // and this assertion still says exactly what it always said — it just says it
            // against the arena that exists rather than the one that did.
            Assert.LessOrEqual(late, BossChoreography.MaxPress,
                "the army presses further than it is allowed to");
            Assert.Less(BossChoreography.MaxPress, 11f - 4f,
                "the army would reach the boss's own body at +11 m");

            float blocked = BossChoreography.ArmyAdvance(10f, 0f, true);
            float unblocked = BossChoreography.ArmyAdvance(10f, 0f, false);
            Assert.Less(unblocked, blocked,
                "an unblocked blow drives the army back no harder than a blocked one");
        }
    }
}
