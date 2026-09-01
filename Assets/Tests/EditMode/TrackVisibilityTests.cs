using BattleRunner.Core.Run;
using NUnit.Framework;

namespace BattleRunner.Tests
{
    [TestFixture]
    public class TrackVisibilityTests
    {
        // The shipped rig and track constants.
        private const float CameraSetback = 10f;
        private const float DespawnMargin = 4f;

        [Test]
        public void APassedGateStaysOnScreen()
        {
            // THE regression. Resolution and despawn were one event, so a gate in another
            // lane correctly did not score but vanished level with the player.
            const float centerZ = 100f;
            const float frontZ = 101.3f; // a large crowd's leading plane

            float linger = TrackVisibility.LingerDistance(frontZ, centerZ, CameraSetback, DespawnMargin);

            Assert.Greater(linger, 10f,
                "a spent gate must remain visible for metres, not vanish where it was spent");
        }

        [Test]
        public void TheTwoPlanesNeverCollapse()
        {
            // Sweep every crowd size: FrontZ ranges from centre+0.6 (one unit) to centre+6
            // at the simulation cap. The linger band must stay open across all of it.
            for (float front = 0.6f; front <= 6f; front += 0.1f)
            {
                float linger = TrackVisibility.LingerDistance(front, 0f, CameraSetback, DespawnMargin);
                Assert.Greater(linger, 0f, $"planes collapsed at frontZ offset {front}");
            }
        }

        [Test]
        public void ObjectIsSpentButVisibleBetweenThePlanes()
        {
            const float centerZ = 50f;
            const float frontZ = 51.3f;
            float despawn = TrackVisibility.DespawnPlane(centerZ, CameraSetback, DespawnMargin);

            float midband = (frontZ + despawn) * 0.5f;
            Assert.IsTrue(TrackVisibility.HasPassed(midband, frontZ), "should have scored by now");
            Assert.IsFalse(TrackVisibility.IsBehindCamera(midband, centerZ, CameraSetback, DespawnMargin),
                "but must still be drawing");
        }

        [Test]
        public void ObjectAheadOfTheCrowdHasNotPassed()
        {
            Assert.IsFalse(TrackVisibility.HasPassed(60f, 51.3f));
            Assert.IsTrue(TrackVisibility.HasPassed(51.3f, 51.3f), "exactly level counts as passed");
        }

        [Test]
        public void DespawnIsAlwaysBehindTheCamera()
        {
            const float centerZ = 200f;
            float cameraZ = centerZ - CameraSetback;
            float despawn = TrackVisibility.DespawnPlane(centerZ, CameraSetback, DespawnMargin);

            Assert.Less(despawn, cameraZ, "recycling must never happen in front of the lens");
        }

        [Test]
        public void ZeroMarginIsAllowedButLeavesNoClearance()
        {
            const float centerZ = 0f;
            float despawn = TrackVisibility.DespawnPlane(centerZ, CameraSetback, 0f);
            Assert.AreEqual(-CameraSetback, despawn, 1e-4f);
        }

        [Test]
        public void NonsenseGeometryIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => TrackVisibility.DespawnPlane(0f, 0f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => TrackVisibility.DespawnPlane(0f, 10f, -1f));
        }
    }
}
