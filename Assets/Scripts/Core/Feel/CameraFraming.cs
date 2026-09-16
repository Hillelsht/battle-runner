using System;

namespace BattleRunner.Core.Feel
{
    /// <summary>One camera rig: where it sits, where it aims, and how wide it sees.</summary>
    public readonly struct CameraFrame
    {
        /// <summary>Metres above the road.</summary>
        public readonly float Height;
        /// <summary>Metres behind the crowd's centre. Never more than <see cref="CameraFraming.SetbackMax"/>.</summary>
        public readonly float Setback;
        /// <summary>Metres ahead of the crowd's centre that the rig aims at.</summary>
        public readonly float LookAhead;
        /// <summary>Height of the aim point.</summary>
        public readonly float LookHeight;
        public readonly float FieldOfView;

        public CameraFrame(float height, float setback, float lookAhead, float lookHeight, float fieldOfView)
        {
            Height = height;
            Setback = setback;
            LookAhead = lookAhead;
            LookHeight = lookHeight;
            FieldOfView = fieldOfView;
        }

        /// <summary>How far the rig is tilted down, in degrees. Always positive.</summary>
        public float PitchDegrees =>
            (float)(Math.Atan2(Height - LookHeight, Setback + LookAhead) * 180.0 / Math.PI);

        /// <summary>
        /// Where the TOP edge of the frame points, in degrees below horizontal. Negative means
        /// the frame still contains sky; positive means the horizon has left the screen and the
        /// player is looking at nothing but ground.
        /// </summary>
        public float TopRayDegrees => PitchDegrees - FieldOfView * 0.5f;

        /// <summary>
        /// Where the BOTTOM edge of the frame meets the ground, as a z relative to the crowd's
        /// centre. The army's rear sits at -CrowdMath.RearDepthMax, so a value above that is
        /// clipping the back of the formation.
        /// </summary>
        public float GroundLineZ
        {
            get
            {
                double down = (PitchDegrees + FieldOfView * 0.5f) * Math.PI / 180.0;
                // Past 89.5 degrees the bottom ray is effectively straight down and tan blows
                // up; the frame simply ends beneath the lens.
                if (down >= 89.5 * Math.PI / 180.0) return -Setback;
                return -Setback + (float)(Height / Math.Tan(down));
            }
        }

        /// <summary>
        /// How many metres of road, measured from the figure outward, this rig cannot see
        /// because the hero is standing in front of it.
        ///
        /// THIS IS THE NUMBER THE WHOLE LIFT EXISTS FOR. A body of the given height and depth
        /// stands `standsAt` metres ahead of the crowd's centre; a road point is hidden when
        /// the ray from the lens to it passes through that body. Everything is in the plane of
        /// the road's centreline, which is where the hero and the gates both are.
        /// </summary>
        public float RoadHiddenBy(float bodyHeight, float bodyDepth, float standsAt, float aheadMetres)
        {
            float near = standsAt - bodyDepth * 0.5f;
            float far = standsAt + bodyDepth * 0.5f;
            float hidden = 0f;
            const float Step = 0.05f;
            for (float z = far; z < standsAt + aheadMetres; z += Step)
            {
                double dz = z + Setback;
                if (dz <= 0.0) continue;
                // Where the ray to this road point crosses the body's near and far faces.
                double tNear = (near + Setback) / dz;
                double tFar = (far + Setback) / dz;
                if (CrossesBody(tNear, bodyHeight) || CrossesBody(tFar, bodyHeight)
                    || CrossesBody((tNear + tFar) * 0.5, bodyHeight))
                {
                    hidden += Step;
                }
            }
            return hidden;
        }

        private bool CrossesBody(double t, float bodyHeight)
        {
            if (t <= 0.0 || t >= 1.0) return false;
            double y = Height - t * Height;
            return y >= 0.0 && y <= bodyHeight;
        }
    }

    /// <summary>
    /// How high the camera rides, as a function of how big the army has become.
    ///
    /// THE REPORT: *"when my army and my main character are too big, they become super big. And
    /// I like it. Change the camera... show everything from above so I could still see the road
    /// and what's on it. Otherwise I don't see anything, the character hides it."*
    ///
    /// Measured, the hero hides **25.9 m of road** at a million men — about two and a half
    /// seconds of running. And it is a WIDTH problem, not a height one: portrait gives a 60
    /// degree vertical field but only 29 degrees horizontally, so a 4.49 m hero standing 16 m
    /// from the lens is 52% of the frame's width while being 17% of its height.
    ///
    /// THE RIG MAY RISE AND CLOSE IN, BUT IT MAY NEVER MOVE BACK. `CameraRig.SetbackMeters` is
    /// a public const that `TrackController` uses to place the despawn plane, so a rig that
    /// drifted backwards would start deleting track inside the frame. Closing in is safe —
    /// objects simply despawn further behind the lens than they need to.
    ///
    /// WHY IT STOPS AT 12.5 METRES. Two constraints meet there, and both are tests:
    ///
    ///   * the top of the frame must keep some sky, or the arches, the castles and the
    ///     horizon — most of what the world work bought — stop being visible at exactly the
    ///     moment the player has the biggest army to look at it with;
    ///   * the bottom of the frame must not cut the army's tail off.
    ///
    /// The sky margin is not measured against zero. The juice layer eats into it: shake adds
    /// up to 1.5 degrees of pitch and a boss telegraph narrows the field by 2.5, dropping the
    /// top edge another 1.25. So the static margin has to be four degrees for the frame to
    /// survive a shaken telegraph, which is precisely the case where the player most wants to
    /// see what is in front of them. At 15 m the static margin is already NEGATIVE.
    /// </summary>
    public static class CameraFraming
    {
        // --- the shipped rig, which was verified on a device and must not drift ------
        public const float BaseHeight = 5.5f;
        public const float BaseSetback = 10f;
        public const float BaseLookAhead = 10f;
        public const float LookHeight = 1.5f;
        public const float BaseFieldOfView = 60f;

        // --- the lifted rig, at the frontier the two constraints leave ---------------
        public const float LiftedHeight = 12.5f;
        public const float LiftedSetback = 9f;
        public const float LiftedLookAhead = 11f;
        public const float LiftedFieldOfView = 66f;

        /// <summary>The despawn-plane contract: the rig may never sit further back than this.</summary>
        public const float SetbackMax = BaseSetback;

        /// <summary>
        /// Degrees of sky the top of the frame must keep in the static pose, so that shake
        /// (1.5) plus a boss telegraph's narrowing (1.25) cannot push the horizon off screen.
        /// </summary>
        public const float SkyMarginDegrees = 4f;

        /// <summary>
        /// How many powers of ten above the tier cap the lift takes to complete. Four decades
        /// means a rig fully lifted at ten thousand times the cap — two million men on a
        /// mid-tier device — with the halfway point around a hundred times the cap.
        /// </summary>
        public const float LiftDecades = 4f;

        /// <summary>
        /// How far along the lift an army of this size sits, 0 to 1.
        ///
        /// CONTINUOUS AT THE CAP, WHICH `CrowdMath.HeroScaleFor` IS NOT. That one returns 1.0
        /// at exactly the cap and `1 + 0.35*log10(ratio + 1)` above it, so one man past the
        /// cap it returns 1.106 — the hero's scale JUMPS by a tenth for a single recruit.
        /// Driving the camera off a discontinuous input would have put a visible hop in the
        /// frame at the same moment, so the lift is taken from `log10(force/cap)` instead,
        /// which is exactly zero at the cap and rises smoothly from there.
        /// </summary>
        public static float Lift(double force, int tierCap)
        {
            if (tierCap <= 0 || force <= tierCap) return 0f;
            double decades = Math.Log10(force / tierCap) / LiftDecades;
            if (decades <= 0.0) return 0f;
            return decades >= 1.0 ? 1f : (float)decades;
        }

        /// <summary>The rig at a given point along the lift.</summary>
        public static CameraFrame At(float lift)
        {
            float t = lift <= 0f ? 0f : (lift >= 1f ? 1f : lift);
            // Affine in the lift, so t == 0 returns the shipped constants BIT-EXACTLY rather
            // than to within a rounding error: `5.5f + 0f * (12.5f - 5.5f)` is 5.5f.
            return new CameraFrame(
                BaseHeight + t * (LiftedHeight - BaseHeight),
                BaseSetback + t * (LiftedSetback - BaseSetback),
                BaseLookAhead + t * (LiftedLookAhead - BaseLookAhead),
                LookHeight,
                BaseFieldOfView + t * (LiftedFieldOfView - BaseFieldOfView));
        }

        public static CameraFrame For(double force, int tierCap) => At(Lift(force, tierCap));
    }
}
