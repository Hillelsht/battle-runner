using System;
using System.Numerics;

namespace BattleRunner.Core.Crowd
{
    /// <summary>
    /// Formation and steering math for the crowd (doc 04). Phyllotaxis packing grows
    /// the formation from the center without reshuffling: slot i keeps its offset for
    /// the crowd's whole lifetime, so multiplier gates never reorder existing units.
    /// </summary>
    public static class CrowdMath
    {
        /// <summary>Golden angle in radians.</summary>
        public const float GoldenAngle = 2.39996323f;

        /// <summary>Local-space (x, z) offset of formation slot <paramref name="index"/>.</summary>
        public static Vector2 PhyllotaxisSlot(int index, float spacing)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == 0) return Vector2.Zero;
            float radius = spacing * MathF.Sqrt(index);
            float angle = index * GoldenAngle;
            return new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }

        /// <summary>
        /// Critically-damped spring step toward a target; returns the new position and
        /// updates velocity. Stable for dt spikes up to ~0.1 s at the default stiffness.
        /// </summary>
        public static float SpringDamperStep(float current, ref float velocity, float target, float stiffness, float dt)
        {
            if (stiffness <= 0f) throw new ArgumentOutOfRangeException(nameof(stiffness));
            float omega = stiffness;
            float x = current - target;
            float exp = 1f / (1f + omega * dt + 0.48f * omega * omega * dt * dt + 0.235f * omega * omega * omega * dt * dt * dt);
            float change = (velocity + omega * x * dt) * dt;
            velocity = (velocity - omega * omega * change * dt) * exp * exp;
            return target + (x + change) * exp * exp;
        }

        /// <summary>How many units to actually render for a logical force count, per device tier cap (doc 01, R2).</summary>
        public static int VisibleUnits(long forceCount, int tierCap)
        {
            if (tierCap <= 0) throw new ArgumentOutOfRangeException(nameof(tierCap));
            if (forceCount <= 0) return 0;
            return forceCount > tierCap ? tierCap : (int)forceCount;
        }

        /// <summary>Hero scale bump expressing over-cap growth the bodies can't (1.0 at or below cap).</summary>
        public static float HeroScaleFor(long forceCount, int tierCap)
        {
            if (forceCount <= tierCap || tierCap <= 0) return 1f;
            double ratio = (double)forceCount / tierCap;
            return 1f + 0.35f * (float)Math.Log10(ratio + 1.0);
        }
    }
}
