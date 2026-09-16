using BattleRunner.Core.Crowd;
using BattleRunner.Core.Feel;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The camera lift. Three of these are contracts with other systems rather than taste:
    /// the rig may never back away from the crowd (the despawn plane is placed from a
    /// constant), the horizon may never leave the frame, and the tail of the army may not be
    /// cut off. The fourth is that nothing moves at all until the army is past the tier cap.
    /// </summary>
    [TestFixture]
    public class CameraFramingTests
    {
        private const int Cap = 200;

        [Test]
        public void AtOrBelowTheCapTheRigIsExactlyWhatShipped()
        {
            // BIT-EXACT, not within a tolerance. The shipped framing was verified on a real
            // device and every other number in CameraRig's comments is derived from it — the
            // FOV floor, the despawn plane, the crowd's rear depth. It must not drift by a
            // rounding error just because the expression around it changed.
            foreach (double force in new[] { 0.0, 1.0, 5.0, 199.0, 200.0 })
            {
                CameraFrame f = CameraFraming.For(force, Cap);
                Assert.AreEqual(5.5f, f.Height, $"height moved at force {force}");
                Assert.AreEqual(10f, f.Setback, $"setback moved at force {force}");
                Assert.AreEqual(10f, f.LookAhead, $"look-ahead moved at force {force}");
                Assert.AreEqual(60f, f.FieldOfView, $"fov moved at force {force}");
            }
        }

        [Test]
        public void TheCameraDoesNotJumpWhenTheArmyPassesTheCap()
        {
            // THE TEST THAT EXISTS BECAUSE THE HERO'S SCALE DOES JUMP. CrowdMath.HeroScaleFor
            // returns 1.0 at exactly the cap and 1 + 0.35*log10(ratio + 1) above it, so at one
            // man past the cap it returns 1.106 — a tenth of the hero's size, for one recruit.
            // Driving the camera off that would have put the same hop in the frame.
            // The bar is "imperceptible", not "zero" — a log curve leaving zero has to move
            // SOMETHING. One extra man moves the rig by about four millimetres of height, or
            // 0.05% of the lift; the hero beside it changes size by 10.6%.
            CameraFrame at = CameraFraming.For(Cap, Cap);
            CameraFrame past = CameraFraming.For(Cap + 1, Cap);
            Assert.AreEqual(at.Height, past.Height, 0.01f,
                "the rig hops as the army passes the cap, the way the hero's scale does");
            Assert.AreEqual(at.Setback, past.Setback, 0.01f);
            Assert.AreEqual(at.FieldOfView, past.FieldOfView, 0.01f);
            Assert.Less(CameraFraming.Lift(Cap + 1, Cap), 0.001f,
                "one recruit past the cap should be the very start of the lift, not a step into it");
        }

        [Test]
        public void TheHorizonNeverLeavesTheFrame()
        {
            // The margin is measured against four degrees, not zero, and the four is a budget:
            // shake contributes up to 1.5 degrees of pitch (CameraRig.MaxShakePitchDegrees) and
            // a boss telegraph narrows the field by 2.5 (TelegraphFovLeanIn), which drops the
            // top edge a further 1.25. A frame that only just keeps the sky when nothing is
            // happening loses it exactly when a boss is winding up.
            for (int i = 0; i <= 1000; i++)
            {
                CameraFrame f = CameraFraming.At(i / 1000f);
                Assert.LessOrEqual(f.TopRayDegrees, -CameraFraming.SkyMarginDegrees,
                    $"at lift {i / 1000f:0.000} the top of the frame is only "
                    + $"{-f.TopRayDegrees:0.00} degrees above horizontal, which a shaken "
                    + "telegraph would take the rest of");
            }
        }

        [Test]
        public void TheRigNeverBacksAwayFromTheCrowd()
        {
            // THE DESPAWN CONTRACT. TrackController places the despawn plane from the constant
            // CameraRig.SetbackMeters, not from where the camera actually is, so a rig that
            // drifted backwards would start deleting track inside the frame. Closing in is
            // safe — things despawn further behind the lens than they strictly need to.
            for (int i = 0; i <= 1000; i++)
            {
                CameraFrame f = CameraFraming.At(i / 1000f);
                Assert.LessOrEqual(f.Setback, CameraFraming.SetbackMax + 1e-4f,
                    $"at lift {i / 1000f:0.000} the rig sits {f.Setback:0.00} m back, past the "
                    + "distance the despawn plane was computed from");
            }
        }

        [Test]
        public void TheBottomOfTheFrameNeverCutsMoreThanATouchOfTheTail()
        {
            // The army's rear reaches CrowdMath.RearDepthMax behind its centre. Some clipping
            // is acceptable and even reads well — an army continuing past the bottom of the
            // screen sells its size — but losing half the formation does not. 0.35 m is about
            // one body, and it is what stops someone later raising the endpoint to 15 m, where
            // the bottom of the frame eats 1.33 m of a 2.47 m tail.
            float floor = -(CrowdMath.RearDepthMax - 0.35f);
            for (int i = 0; i <= 1000; i++)
            {
                CameraFrame f = CameraFraming.At(i / 1000f);
                Assert.LessOrEqual(f.GroundLineZ, floor,
                    $"at lift {i / 1000f:0.000} the bottom of the frame meets the ground at "
                    + $"{f.GroundLineZ:0.00}, cutting into an army whose rear reaches "
                    + $"{-CrowdMath.RearDepthMax:0.00}");
            }
        }

        [Test]
        public void TheFramingIsMonotoneAndHasNoSteps()
        {
            // A camera that moves non-monotonically with the army hunts: grow a little, the
            // rig rises; grow a little more, it dips. And a step anywhere reads as a dropped
            // frame. Both are properties of the interpolation rather than of the endpoints,
            // which is exactly the kind of thing that survives an innocent-looking edit.
            CameraFrame previous = CameraFraming.At(0f);
            for (int i = 1; i <= 1000; i++)
            {
                CameraFrame f = CameraFraming.At(i / 1000f);
                Assert.GreaterOrEqual(f.Height, previous.Height - 1e-5f, "height dipped");
                Assert.LessOrEqual(f.Setback, previous.Setback + 1e-5f, "setback grew");
                Assert.GreaterOrEqual(f.FieldOfView, previous.FieldOfView - 1e-5f, "fov narrowed");
                Assert.GreaterOrEqual(f.PitchDegrees, previous.PitchDegrees - 1e-5f, "pitch flattened");
                Assert.Less(f.Height - previous.Height, 0.05f, "the height steps");
                Assert.Less(f.PitchDegrees - previous.PitchDegrees, 0.2f, "the pitch steps");
                previous = f;
            }
        }

        [Test]
        public void TheLiftActuallyGetsTheHeroOutOfTheWay()
        {
            // The point of the whole exercise. A 1e6 army puts the Warden at 3.33 m tall and
            // 4.49 m wide, standing about 5.8 m ahead of the crowd's centre; the shipped rig
            // hides 25.9 m of road behind him. This asserts the geometry that fixes it rather
            // than the occlusion itself: at full lift the rig must be looking down at least
            // twice as steeply, which is what turns a body between you and the road into a
            // body beside the road.
            CameraFrame shipped = CameraFraming.At(0f);
            CameraFrame lifted = CameraFraming.At(1f);
            Assert.Greater(lifted.PitchDegrees, shipped.PitchDegrees * 2f,
                $"the lifted rig only reaches {lifted.PitchDegrees:0.0} degrees against the "
                + $"shipped {shipped.PitchDegrees:0.0}, which will not clear the hero");
            Assert.Greater(lifted.Height, shipped.Height + 5f, "the rig barely rises");
        }

        [Test]
        public void TheLiftCancelsTheHeroSGrowthInsteadOfChasingIt()
        {
            // THE PROPERTY THE WHOLE FEATURE IS FOR, and it is stronger than "less occlusion":
            // the road the hero hides stays ROUGHLY CONSTANT as the army grows, because the
            // rig rises at about the rate the figure does. On the shipped rig it ran away —
            // 5.8 m at the cap, 13.5 m at twenty thousand, 25.9 m at a million, 64.5 m at a
            // hundred million, which is six seconds of road the player cannot see.
            //
            // The Warden is the widest of the four heroes: 1.074 tall and 0.75 deep in mesh
            // units, drawn at 1.35x the scale CrowdMath returns, standing on the crowd's
            // leading plane about 5.8 m ahead of its centre.
            foreach (double force in new[] { 200.0, 2e3, 2e4, 2e5, 1e6, 1e8 })
            {
                float scale = 1.35f * CrowdMath.HeroScaleFor(force, Cap);
                CameraFrame f = CameraFraming.For(force, Cap);
                float hidden = f.RoadHiddenBy(1.074f * scale, 0.70f * scale, 5.8f, 70f);
                Assert.Less(hidden, 10f,
                    $"at {force:N0} men the hero hides {hidden:0.0} m of road — the rig has "
                    + "stopped keeping up with the figure it is supposed to be looking over");
            }
        }

        [Test]
        public void TheLiftIsZeroBelowTheCapAndSaturates()
        {
            Assert.AreEqual(0f, CameraFraming.Lift(50, Cap));
            Assert.AreEqual(0f, CameraFraming.Lift(Cap, Cap));
            Assert.AreEqual(0f, CameraFraming.Lift(1000, 0), "a nonsense cap must not divide");
            Assert.Greater(CameraFraming.Lift(20_000, Cap), 0.4f);
            Assert.AreEqual(1f, CameraFraming.Lift(1e12, Cap), 1e-4f);
        }
    }
}
