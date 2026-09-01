using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// The two planes a piece of track furniture crosses, which are NOT the same event.
    ///
    /// They used to be: a gate was recycled the instant the crowd drew level with it, so a
    /// gate in another lane correctly did not score but also popped out of existence beside
    /// the player instead of sliding past them. Resolution answers "does this score?" and
    /// happens at the crowd's leading plane; despawn answers "can the player still see it?"
    /// and happens behind the camera. Between them is a band where a gate is spent but still
    /// on screen, and that band is the whole fix.
    /// </summary>
    public static class TrackVisibility
    {
        /// <summary>The crowd has drawn level with this object; it scores now or never.</summary>
        public static bool HasPassed(float objectZ, float crowdFrontZ) => objectZ <= crowdFrontZ;

        /// <summary>World Z behind which nothing is visible, so recycling is unobservable.</summary>
        public static float DespawnPlane(float crowdCenterZ, float cameraSetback, float margin)
        {
            if (cameraSetback <= 0f) throw new ArgumentOutOfRangeException(nameof(cameraSetback));
            if (margin < 0f) throw new ArgumentOutOfRangeException(nameof(margin));
            return crowdCenterZ - cameraSetback - margin;
        }

        /// <summary>Safe to recycle: the camera can no longer see it.</summary>
        public static bool IsBehindCamera(float objectZ, float crowdCenterZ, float cameraSetback, float margin) =>
            objectZ < DespawnPlane(crowdCenterZ, cameraSetback, margin);

        /// <summary>
        /// Metres an object stays on screen after it has stopped mattering. Must be positive:
        /// at zero the two planes collapse back together and gates vanish under the player.
        /// </summary>
        public static float LingerDistance(float crowdFrontZ, float crowdCenterZ, float cameraSetback, float margin) =>
            crowdFrontZ - DespawnPlane(crowdCenterZ, cameraSetback, margin);
    }
}
