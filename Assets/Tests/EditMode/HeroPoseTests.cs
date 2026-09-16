using System;
using BattleRunner.Core.Heroes;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The select-screen choreography. Three of these are contracts rather than taste: every
    /// act must start and end at rest so the three are interchangeable, nothing may jump
    /// between frames, and the four heroes must actually move differently — which is the whole
    /// of *"Main characters should look differently and have different animation."*
    /// </summary>
    [TestFixture]
    public class HeroPoseTests
    {
        private static readonly HeroClass[] Heroes =
        {
            HeroClass.Warden, HeroClass.Ashcaller, HeroClass.Houndmaster, HeroClass.Revenant
        };

        private static readonly HeroAct[] Acts = { HeroAct.Idle, HeroAct.Greet, HeroAct.Fight };

        private static float Reach(HeroPose p) =>
            Math.Abs(p.Pitch) + Math.Abs(p.Yaw) + Math.Abs(p.Roll)
            + Math.Abs(p.MainSwing) + Math.Abs(p.MainLift) + Math.Abs(p.OffLift)
            + Math.Abs(p.Rise) * 100f + Math.Abs(p.Step) * 100f;

        [Test]
        public void EveryActStartsAndEndsAtRest()
        {
            // THE PROPERTY THAT MAKES THE THREE INTERCHANGEABLE. The screen cuts from a greeting
            // into an idle and from an idle into a fight on a tap, with no blend; if an act
            // finished anywhere but rest the hero would snap on every transition. It is why
            // Settle is forced to land on exactly 1 by an envelope rather than by clamping.
            foreach (HeroClass hero in Heroes)
                foreach (HeroAct act in Acts)
                {
                    float span = HeroChoreography.ActSeconds(hero, act);
                    Assert.Less(Reach(HeroChoreography.At(hero, act, 0f)), 0.5f,
                        $"{hero} does not start {act} at rest");
                    Assert.Less(Reach(HeroChoreography.At(hero, act, span)), 0.5f,
                        $"{hero} does not end {act} at rest");
                    Assert.Less(Reach(HeroChoreography.At(hero, act, span * 4f)), 0.5f,
                        $"{hero} is still moving long after {act} finished");
                }
        }

        [Test]
        public void NothingJumpsBetweenFrames()
        {
            // A rigid figure has no articulation to hide a discontinuity behind, so a step in
            // any channel reads as a dropped frame rather than as a snap of animation.
            //
            // MEASURED AS A FRACTION OF THE CHANNEL'S OWN RANGE, not in degrees, because a
            // strike is SUPPOSED to be fast: the Warden's mace crosses 228 degrees in a fifth
            // of a second and an absolute bound tight enough to call that smooth would also
            // forbid the swing. What must not happen is a frame that carries a large part of
            // the whole movement, which is what a missing sweep phase looks like — the first
            // version crossed the entire arc between two frames.
            //
            // Sampled at 60 Hz, which is what the device runs.
            foreach (HeroClass hero in Heroes)
                foreach (HeroAct act in Acts)
                {
                    float span = HeroChoreography.ActSeconds(hero, act);
                    int frames = Math.Max(8, (int)(span * 60f));
                    var pitch = new float[frames + 1];
                    var yaw = new float[frames + 1];
                    var swing = new float[frames + 1];
                    var rise = new float[frames + 1];
                    var step = new float[frames + 1];
                    for (int i = 0; i <= frames; i++)
                    {
                        HeroPose p = HeroChoreography.At(hero, act, span * i / frames);
                        pitch[i] = p.Pitch; yaw[i] = p.Yaw; swing[i] = p.MainSwing;
                        rise[i] = p.Rise; step[i] = p.Step;
                    }
                    NoStep(pitch, $"{hero} {act} pitch");
                    NoStep(yaw, $"{hero} {act} yaw");
                    NoStep(swing, $"{hero} {act} main hand");
                    NoStep(rise, $"{hero} {act} rise");
                    NoStep(step, $"{hero} {act} step");
                }
        }

        /// <summary>
        /// No single frame may carry more than 30% of a channel's whole travel.
        ///
        /// THE BAR IS SET AGAINST THE BUG, not against a feeling. The missing sweep phase put
        /// 100% of the pitch into one frame; the fastest hero's genuine strike puts 26% of it
        /// into one, because the Ashcaller crosses her whole arc in nine frames and the swing
        /// is eased to be quickest as it leaves the wind-up. Anything tight enough to call 26%
        /// a jump would forbid a fast character from moving fast, which is most of what makes
        /// four heroes read as four people.
        ///
        /// A channel that barely moves at all is exempt by the absolute floor — 30% of nothing
        /// is nothing, and holding it to that would fail on rounding.
        /// </summary>
        private static void NoStep(float[] samples, string what)
        {
            float lo = float.MaxValue, hi = float.MinValue, worst = 0f;
            foreach (float v in samples) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            for (int i = 1; i < samples.Length; i++)
                worst = Math.Max(worst, Math.Abs(samples[i] - samples[i - 1]));
            float range = hi - lo;
            Assert.Less(worst, range * 0.3f + 1e-3f,
                $"{what} moves {worst:0.###} in one frame out of {range:0.###} of travel");
        }

        [Test]
        public void TheFourDoNotMoveTheSameWay()
        {
            // *"Main characters should look differently and have DIFFERENT ANIMATION."* Four
            // heroes running one curve with one set of numbers would pass every other test in
            // this file and still be four identical performances. Sampled over a whole fight,
            // no two of the four may trace the same path.
            foreach (HeroClass a in Heroes)
                foreach (HeroClass b in Heroes)
                {
                    if (a >= b) continue;
                    float apart = 0f;
                    for (int i = 0; i <= 60; i++)
                    {
                        // Against a COMMON clock, not each hero's own phase: two heroes with
                        // the same shape at different speeds are still two performances, and
                        // comparing them at matched fractions would call them identical.
                        float s = 1.4f * i / 60f;
                        HeroPose pa = HeroChoreography.At(a, HeroAct.Fight, s);
                        HeroPose pb = HeroChoreography.At(b, HeroAct.Fight, s);
                        apart += Math.Abs(pa.MainSwing - pb.MainSwing)
                               + Math.Abs(pa.Pitch - pb.Pitch) * 2f;
                    }
                    Assert.Greater(apart / 61f, 4f,
                        $"{a} and {b} fight within {apart / 61f:0.0} degrees of each other");
                }
        }

        [Test]
        public void AGreetingRaisesSomethingAndHoldsIt()
        {
            // A salute that arrives and immediately leaves is a twitch. The hold is the whole
            // act, so it is asserted: the main hand must be near its peak across a real span
            // in the middle rather than at one instant.
            foreach (HeroClass hero in Heroes)
            {
                float span = HeroChoreography.ActSeconds(hero, HeroAct.Greet);
                float peak = 0f;
                for (int i = 0; i <= 100; i++)
                    peak = Math.Max(peak,
                        HeroChoreography.At(hero, HeroAct.Greet, span * i / 100f).MainLift);
                Assert.Greater(peak, 25f, $"{hero} barely lifts anything to greet with");

                int held = 0;
                for (int i = 0; i <= 100; i++)
                    if (HeroChoreography.At(hero, HeroAct.Greet, span * i / 100f).MainLift
                        > peak * 0.95f) held++;
                Assert.Greater(held, 20, $"{hero}'s greeting is a twitch, not a salute");
            }
        }

        [Test]
        public void ABlowGathersSlowlyAndLandsFast()
        {
            // WHERE THE WEIGHT COMES FROM, and the only thing separating a hit from a wave.
            // The main hand must travel backwards first, cross zero after the midpoint of the
            // wind-up rather than at the start, and reach further forward than it went back.
            foreach (HeroClass hero in Heroes)
            {
                float span = HeroChoreography.ActSeconds(hero, HeroAct.Fight);
                float back = 0f, forward = 0f, hottest = 0f;
                for (int i = 0; i <= 200; i++)
                {
                    HeroPose p = HeroChoreography.At(hero, HeroAct.Fight, span * i / 200f);
                    back = Math.Min(back, p.MainSwing);
                    forward = Math.Max(forward, p.MainSwing);
                    hottest = Math.Max(hottest, p.Heat);
                }
                Assert.Less(back, -20f, $"{hero} never winds up");
                Assert.Greater(forward, -back, $"{hero}'s follow-through is shorter than the gather");
                Assert.Greater(hottest, 1.3f, $"{hero}'s blow has no moment in it");

                // Eased IN, so the first quarter of the gather covers less than a quarter of it.
                float quarter = Math.Abs(
                    HeroChoreography.At(hero, HeroAct.Fight, span * 0.1125f).MainSwing);
                Assert.Less(quarter, Math.Abs(back) * 0.25f,
                    $"{hero} gathers at a constant speed, which reads as a wave");
            }
        }

        [Test]
        public void StandingStillIsNeverActuallyStill()
        {
            // A figure that stops moving on a select screen reads as the game having frozen.
            // The idle also has to LOOP: it is sampled forever and a seam would tick.
            foreach (HeroClass hero in Heroes)
            {
                float span = HeroChoreography.ActSeconds(hero, HeroAct.Idle);
                float moved = 0f;
                for (int i = 0; i <= 200; i++)
                    moved = Math.Max(moved,
                        Reach(HeroChoreography.At(hero, HeroAct.Idle, span * i / 200f)));
                Assert.Greater(moved, 3f, $"{hero} stands like a prop");

                HeroPose first = HeroChoreography.At(hero, HeroAct.Idle, 0f);
                HeroPose looped = HeroChoreography.At(hero, HeroAct.Idle, span * 7f);
                Assert.AreEqual(first.Yaw, looped.Yaw, 1e-3f, $"{hero}'s idle does not loop");
                Assert.AreEqual(first.Pitch, looped.Pitch, 1e-3f);
            }
        }

        [Test]
        public void NoPoseEverLeavesTheStage()
        {
            // The stage is lit and framed for a figure standing on its mark. Everything here is
            // bounded so no act can push a hero out of the light or through the plinth.
            foreach (HeroClass hero in Heroes)
                foreach (HeroAct act in Acts)
                {
                    float span = HeroChoreography.ActSeconds(hero, act);
                    for (int i = 0; i <= 200; i++)
                    {
                        HeroPose p = HeroChoreography.At(hero, act, span * i / 200f);
                        Assert.Less(Math.Abs(p.Pitch), 30f, $"{hero} {act} falls over");
                        Assert.Less(Math.Abs(p.Yaw), 30f, $"{hero} {act} turns its back");
                        Assert.Less(Math.Abs(p.Roll), 20f, $"{hero} {act} topples sideways");
                        Assert.GreaterOrEqual(p.Rise, -1e-4f, $"{hero} {act} sinks into the plinth");
                        Assert.Less(p.Rise, 0.30f, $"{hero} {act} takes off");
                        Assert.Less(Math.Abs(p.Step), 0.40f, $"{hero} {act} walks off the mark");
                        Assert.Greater(p.Heat, 0f, $"{hero} {act} goes black");
                        Assert.Less(p.Heat, 3f, $"{hero} {act} blows out the bloom");
                    }
                }
        }
    }
}
