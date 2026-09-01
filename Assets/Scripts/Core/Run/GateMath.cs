using System;

namespace BattleRunner.Core.Run
{
    public enum GateOp
    {
        Add = 0,
        Multiply = 1,
        Subtract = 2
    }

    /// <summary>
    /// Force arithmetic for math gates, including the soft cap that converts
    /// runaway multiplier chains into overflow bonus instead of raw force (doc 01, R4).
    /// </summary>
    public static class GateMath
    {
        /// <summary>
        /// Applies one gate to the current force. Force never drops below zero and never
        /// exceeds <paramref name="softCap"/>; the amount above the cap is returned in
        /// <paramref name="overflow"/> for conversion into bonus damage / loot luck.
        /// </summary>
        public static long ApplyGate(long force, GateOp op, int value, long softCap, out long overflow)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Gate values are authored non-negative; the op carries the sign.");
            if (softCap <= 0) throw new ArgumentOutOfRangeException(nameof(softCap));

            long result = op switch
            {
                GateOp.Add => SaturatingAdd(force, value),
                GateOp.Multiply => SaturatingMultiply(force, value),
                GateOp.Subtract => Math.Max(0L, force - value),
                _ => throw new ArgumentOutOfRangeException(nameof(op))
            };

            if (result > softCap)
            {
                overflow = result - softCap;
                return softCap;
            }

            overflow = 0;
            return result;
        }

        /// <summary>
        /// Applies a gate, then amplifies whatever it GAINED by the hero's gate-yield stat.
        ///
        /// Scaling the gain rather than the gate's printed value keeps one rule for both
        /// operators: a +10 with 20% yield gives 12, and a x2 on 50 force gains 50 and so
        /// gives 60. A gate that costs force is untouched — yield is a reward, not a shield.
        /// </summary>
        public static long ApplyGateWithYield(long force, GateOp op, int value, long softCap,
            float gateYield, out long overflow)
        {
            long result = ApplyGate(force, op, value, softCap, out overflow);
            if (gateYield <= 0f || result <= force) return result;

            long gain = result - force;
            long amplified = SaturatingAdd(force, (long)Math.Round(gain * (1.0 + gateYield)));
            if (amplified <= softCap) return amplified;

            overflow += amplified - softCap;
            return softCap;
        }

        /// <summary>Diminishing conversion of accumulated overflow into a bonus multiplier (1.0 = no bonus).</summary>
        public static float OverflowToBonusMultiplier(long accumulatedOverflow, long softCap)
        {
            if (accumulatedOverflow <= 0 || softCap <= 0) return 1f;
            // Log-shaped: doubling the cap in overflow grants +25% — rewarding, never balance-breaking.
            double ratio = (double)accumulatedOverflow / softCap;
            return 1f + 0.25f * (float)Math.Log(1.0 + ratio, 2.0);
        }

        private static long SaturatingAdd(long a, long b)
        {
            long r = unchecked(a + b);
            return r < a ? long.MaxValue : r;
        }

        private static long SaturatingMultiply(long a, long b)
        {
            if (a == 0 || b == 0) return 0;
            if (a > long.MaxValue / b) return long.MaxValue;
            return a * b;
        }
    }
}
