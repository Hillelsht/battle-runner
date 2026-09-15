using System;

namespace BattleRunner.Core.World
{
    /// <summary>What flies over a world. None for the seven that have nothing.</summary>
    public enum SkyRiderKind
    {
        None = 0,
        /// <summary>A winged pony, circling. Thistlewood's, and asked for by name.</summary>
        Pegasus = 1
    }

    /// <summary>
    /// The flight path of whatever circles over a world.
    ///
    /// NUMBERS RATHER THAN A CURVE IN THE ENGINE, for one reason that a test can check and an
    /// eyeball cannot: this is the only moving thing in the game that is not on the ground,
    /// and the road already has things over it. A gothic arch crowns at 8.7 m. A creature
    /// authored at eight would fly THROUGH the arches of any world that had both, once every
    /// lap, and it would look like a bug rather than like a collision because nothing here
    /// collides with anything.
    /// </summary>
    public readonly struct SkyPath
    {
        /// <summary>Metres above the road at the middle of the bob.</summary>
        public readonly float Height;
        /// <summary>How far out from the centreline the circle reaches.</summary>
        public readonly float RadiusX;
        /// <summary>And along it. Wider than RadiusX, so the path is an ellipse down the road.</summary>
        public readonly float RadiusZ;
        /// <summary>How far ahead of the army the circle is centred.</summary>
        public readonly float Ahead;
        /// <summary>Laps per second. Slow — this is scenery, not a threat.</summary>
        public readonly float LapsPerSecond;
        /// <summary>Metres the creature rises and falls over a lap.</summary>
        public readonly float Bob;
        /// <summary>Wingbeats per second.</summary>
        public readonly float BeatsPerSecond;
        /// <summary>Degrees the wings sweep either side of level.</summary>
        public readonly float BeatDegrees;

        public SkyPath(float height, float radiusX, float radiusZ, float ahead,
            float lapsPerSecond, float bob, float beatsPerSecond, float beatDegrees)
        {
            Height = height;
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            Ahead = ahead;
            LapsPerSecond = lapsPerSecond;
            Bob = bob;
            BeatsPerSecond = beatsPerSecond;
            BeatDegrees = beatDegrees;
        }

        /// <summary>The lowest the creature ever gets.</summary>
        public float Floor => Height - Bob;
    }

    public static class SkyRiders
    {
        /// <summary>
        /// The tallest thing the game puts over the road, so the one number a flight path has
        /// to beat. Kept here rather than computed from RoadArches so a test can assert the
        /// two agree — a crown that grew and a path that did not is exactly the drift this
        /// guards against.
        /// </summary>
        public const float TallestArchCrown = 8.40f;

        /// <summary>How much daylight a flier must leave over the tallest span.</summary>
        public const float ArchMargin = 4.0f;

        // THE CAMERA, DUPLICATED, and unlike RoadArches.RoadHalfWidth this one cannot be
        // cross-checked: CameraRig lives in Gameplay and Core cannot see it. So it is written
        // out with its source rather than left as three bare numbers — CameraRig.cs:239-244
        // puts the camera at y = 5.5 ten metres behind the crowd, aims it at y = 1.5 ten
        // metres ahead, and runs a 60-degree VERTICAL field of view.
        //
        // They are here because "can the player see it" is the property that decides whether
        // a flier is worth drawing at all, and it is pure trigonometry.
        public const float CameraHeight = 5.5f;
        public const float CameraSetback = 10.0f;
        public const float CameraPitchDegrees = -11.3f;
        public const float CameraHalfFovDegrees = 30.0f;
        /// <summary>Portrait, so the horizontal field is far narrower than the vertical.</summary>
        public const float CameraHalfFovHorizontalDegrees = 15.0f;

        /// <summary>
        /// Whether a sample would be inside the frame, taken from the crowd's own position.
        /// Approximate — it ignores the camera's lateral follow and every bit of shake — but
        /// it is the difference between a flier that circles in and out of view and one that
        /// spends most of its lap above the top of the screen.
        /// </summary>
        public static bool InFrame(float x, float y, float z)
        {
            double dz = z + CameraSetback;
            if (dz <= 0.0) return false;
            double elevation = Math.Atan2(y - CameraHeight, dz) * 180.0 / Math.PI;
            double azimuth = Math.Abs(Math.Atan2(x, dz) * 180.0 / Math.PI);
            return elevation <= CameraPitchDegrees + CameraHalfFovDegrees
                   && azimuth <= CameraHalfFovHorizontalDegrees;
        }

        /// <summary>What fraction of a lap the flier is actually on screen for.</summary>
        public static float VisibleShare(SkyPath path, int samples = 180)
        {
            if (path.LapsPerSecond <= 0f || samples <= 0) return 0f;
            float lap = 1f / path.LapsPerSecond;
            int seen = 0;
            for (int i = 0; i < samples; i++)
            {
                SampleAt(path, lap * i / samples, out float x, out float y, out float z, out _, out _);
                if (InFrame(x, y, path.Ahead + z)) seen++;
            }
            return seen / (float)samples;
        }

        private static readonly SkyPath[] Table =
        {
            // None. Sized anyway so nothing has to special-case it.
            new SkyPath(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            // Pegasus: high, wide and slow, and every one of these numbers was MEASURED
            // against the camera rather than chosen. The first draft — 16 m up, centred 58 m
            // ahead on a 26 x 44 ellipse — put the animal inside the frame for **36% of its
            // lap**: at the near end of the orbit it sat 37 degrees above the horizontal and
            // the top of the frame is 18.7. A flier nobody can see for two thirds of the time
            // is not scenery, it is an intermittent glitch at the top of the screen.
            //
            // Pushed out to 78 m and pulled in to 22 x 38, it is in frame for 84%. Height
            // drops to 15 to buy some of that back and still clears the tallest arch: the
            // floor of the bob is 12.8 against a crown of 8.4 plus the 4 m margin.
            new SkyPath(15.0f, 22.0f, 38.0f, 78.0f, 0.045f, 2.2f, 1.6f, 26f)
        };

        public const int Count = 2;

        public static SkyPath For(SkyRiderKind kind)
        {
            int i = (int)kind;
            return i < 0 || i >= Table.Length ? Table[0] : Table[i];
        }

        /// <summary>
        /// Where it is at a given time, as an offset from the circle's centre. Returned as
        /// three floats rather than a vector because Core has no vector type and does not
        /// want one — the engine layer assembles this into a transform.
        /// </summary>
        public static void SampleAt(SkyPath path, float seconds,
            out float x, out float y, out float z, out float headingDegrees, out float rollDegrees)
        {
            double a = seconds * path.LapsPerSecond * 2.0 * Math.PI;
            x = path.RadiusX * (float)Math.Cos(a);
            z = path.RadiusZ * (float)Math.Sin(a);
            // Twice a lap, so it rises on each straight and settles into each turn.
            y = path.Height + path.Bob * (float)Math.Sin(a * 2.0);

            // The TANGENT of the ellipse, not the radius. Facing along the radius would have
            // it flying sideways for the whole lap, which is the single most obvious way to
            // get a circling creature wrong.
            float dx = (float)(-path.RadiusX * Math.Sin(a));
            float dz = (float)(path.RadiusZ * Math.Cos(a));
            headingDegrees = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);

            // Banking INTO the turn. A flier that stays level through a circle reads as a
            // cardboard cut-out being dragged around on a wire.
            rollDegrees = 22f * (float)Math.Cos(a);
        }

        /// <summary>Wing angle at a given time: a beat, with the downstroke faster.</summary>
        public static float WingDegrees(SkyPath path, float seconds)
        {
            double b = seconds * path.BeatsPerSecond * 2.0 * Math.PI;
            // sin cubed rather than sin: a real wing hangs at the top of the stroke and snaps
            // through the bottom, and a pure sine gives equal time to both.
            double s = Math.Sin(b);
            return path.BeatDegrees * (float)(s * s * s);
        }
    }
}
