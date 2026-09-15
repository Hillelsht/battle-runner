using System;
using BattleRunner.Core.World;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    /// <summary>
    /// The only moving thing in the game that is not on the ground.
    ///
    /// Nothing in this game collides with anything, so a flight path that intersects the
    /// scenery does not stop — it passes through it, once a lap, forever. That makes altitude
    /// a correctness property rather than a taste one.
    /// </summary>
    [TestFixture]
    public class SkyRiderTests
    {
        [Test]
        public void TheArchHeightItClearsIsTheArchHeightTheGameDraws()
        {
            // SkyRiders keeps its own copy of the tallest crown so a test has something to
            // compare against. A crown that grew and a path that did not is exactly the drift
            // this exists to catch.
            float tallest = 0f;
            foreach (ArchStyle style in Enum.GetValues(typeof(ArchStyle)))
            {
                float crown = RoadArches.For(style).Crown;
                if (crown > tallest) tallest = crown;
            }
            Assert.AreEqual(tallest, SkyRiders.TallestArchCrown, 1e-3f,
                "the flight paths are cleared against a crown height the arches no longer have");
        }

        [Test]
        public void NothingEverFliesThroughAnArch()
        {
            foreach (SkyRiderKind kind in Enum.GetValues(typeof(SkyRiderKind)))
            {
                if (kind == SkyRiderKind.None) continue;
                SkyPath p = SkyRiders.For(kind);
                Assert.GreaterOrEqual(p.Floor, SkyRiders.TallestArchCrown + SkyRiders.ArchMargin,
                    $"{kind} drops to {p.Floor:0.0} m and the tallest arch crowns at "
                    + $"{SkyRiders.TallestArchCrown:0.0}");
            }
        }

        [Test]
        public void ThePathIsSampledAsAClosedLoopWithNoJump()
        {
            SkyPath p = SkyRiders.For(SkyRiderKind.Pegasus);
            float lap = 1f / p.LapsPerSecond;

            SkyRiders.SampleAt(p, 0f, out float x0, out float y0, out float z0, out _, out _);
            SkyRiders.SampleAt(p, lap, out float x1, out float y1, out float z1, out _, out _);
            Assert.AreEqual(x0, x1, 1e-2f, "a lap does not return to where it started");
            Assert.AreEqual(y0, y1, 1e-2f);
            Assert.AreEqual(z0, z1, 1e-2f);

            // And no step anywhere in between. A creature that teleports once a lap is the
            // failure mode of every hand-rolled parametric path.
            const int Steps = 400;
            float prevX = x0, prevY = y0, prevZ = z0, worst = 0f;
            for (int i = 1; i <= Steps; i++)
            {
                SkyRiders.SampleAt(p, lap * i / Steps, out float x, out float y, out float z, out _, out _);
                float step = (float)Math.Sqrt((x - prevX) * (x - prevX) + (y - prevY) * (y - prevY)
                                              + (z - prevZ) * (z - prevZ));
                if (step > worst) worst = step;
                prevX = x; prevY = y; prevZ = z;
            }
            Assert.Less(worst, 2.0f, $"the path jumps {worst:0.0} m between adjacent samples");
        }

        [Test]
        public void ItFacesTheWayItIsGoing()
        {
            // Facing along the RADIUS rather than the tangent is the classic way to get a
            // circling creature wrong, and it reads immediately: the thing flies sideways for
            // the whole lap. Sampling a step ahead and checking the heading points at it.
            SkyPath p = SkyRiders.For(SkyRiderKind.Pegasus);
            float lap = 1f / p.LapsPerSecond;
            for (int i = 0; i < 12; i++)
            {
                float t = lap * i / 12f;
                SkyRiders.SampleAt(p, t, out float x, out _, out float z, out float heading, out _);
                SkyRiders.SampleAt(p, t + lap * 0.004f, out float nx, out _, out float nz, out _, out _);

                float travel = (float)(Math.Atan2(nx - x, nz - z) * 180.0 / Math.PI);
                float delta = Mathf.DeltaAngle(heading, travel);
                Assert.Less(Math.Abs(delta), 4f,
                    $"at t={t:0.00} it faces {heading:0} while travelling {travel:0}");
            }
        }

        [Test]
        public void ItBanksIntoTheTurnRatherThanStayingLevel()
        {
            SkyPath p = SkyRiders.For(SkyRiderKind.Pegasus);
            float lap = 1f / p.LapsPerSecond;
            float most = 0f, least = 0f;
            for (int i = 0; i < 64; i++)
            {
                SkyRiders.SampleAt(p, lap * i / 64f, out _, out _, out _, out _, out float roll);
                if (roll > most) most = roll;
                if (roll < least) least = roll;
            }
            Assert.Greater(most, 10f, "it never banks one way");
            Assert.Less(least, -10f, "it never banks the other");
        }

        [Test]
        public void TheWingBeatIsAsymmetricAndReturnsToLevel()
        {
            // sin cubed, not sin: a real wing hangs at the top of the stroke and snaps through
            // the bottom. The test is that the time spent near the extremes is NOT equal to
            // the time a sine would spend there.
            SkyPath p = SkyRiders.For(SkyRiderKind.Pegasus);
            Assert.AreEqual(0f, SkyRiders.WingDegrees(p, 0f), 1e-3f);

            const int Steps = 512;
            float beat = 1f / p.BeatsPerSecond;
            int nearLevel = 0;
            for (int i = 0; i < Steps; i++)
            {
                float w = SkyRiders.WingDegrees(p, beat * i / Steps);
                Assert.LessOrEqual(Math.Abs(w), p.BeatDegrees + 1e-3f, "the wing oversweeps");
                if (Math.Abs(w) < p.BeatDegrees * 0.25f) nearLevel++;
            }
            // A pure sine spends about 16% of its cycle inside a quarter of its amplitude;
            // a cubed one spends well over a third of it there.
            Assert.Greater(nearLevel / (float)Steps, 0.30f,
                "the beat is a plain sine, so the wing moves at the same speed everywhere");
        }

        [Test]
        public void ThePlayerCanActuallySeeIt()
        {
            // THE MEASUREMENT THAT MOVED THE ORBIT. The first draft circled 58 m ahead on a
            // 26 x 44 ellipse at 16 m, and at the near end of that orbit the animal sat 37
            // degrees above the horizontal against a frame top of 18.7 — in view for 36% of
            // its lap. A flier nobody can see for two thirds of the time is not scenery, it is
            // an intermittent glitch at the top of the screen.
            foreach (SkyRiderKind kind in Enum.GetValues(typeof(SkyRiderKind)))
            {
                if (kind == SkyRiderKind.None) continue;
                float share = SkyRiders.VisibleShare(SkyRiders.For(kind));
                Assert.Greater(share, 0.65f,
                    $"{kind} is on screen for {share:P0} of its lap");
                // And NOT all of it: something permanently parked in the frame stops being a
                // thing that flies over and becomes part of the HUD.
                Assert.Less(share, 0.97f, $"{kind} never leaves the frame");
            }
        }

        [Test]
        public void TheFrameTestRejectsWhatIsBehindTheCamera()
        {
            // z is measured from the crowd and the camera sits ten metres behind it, so a
            // sample at -20 is behind the lens. Atan2 would happily return an angle for it.
            Assert.IsFalse(SkyRiders.InFrame(0f, 15f, -20f));
            Assert.IsFalse(SkyRiders.InFrame(0f, 15f, -10f));
        }

        [Test]
        public void ExactlyOneWorldHasSomethingFlyingOverIt()
        {
            int fliers = 0;
            foreach (WorldTheme t in WorldThemes.All)
                if (t.Scenery.SkyRider != SkyRiderKind.None) fliers++;
            Assert.AreEqual(1, fliers,
                "a flier in every world is weather; a flier in one is that world's");
        }

        private static class Mathf
        {
            public static float DeltaAngle(float a, float b)
            {
                float d = (b - a) % 360f;
                if (d > 180f) d -= 360f;
                if (d < -180f) d += 360f;
                return d;
            }
        }
    }
}
