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
    /// Force arithmetic for math gates.
    ///
    /// WHAT CHANGED, AND WHY IT HAD TO. Until now a gate carried an absolute headcount: a
    /// +30 gate added thirty men whatever size the army was, and the army was reset to five
    /// at the start of every round so that thirty meant something. The report was that the
    /// crowd "shrinks to minimum" every round and should behave like an RPG level — earned
    /// once, never taken away — and an absolute gate cannot survive that change. Simulated
    /// against the real generator, carrying the army over with absolute gates pins the
    /// player at the soft cap by ROUND FIVE and every round after it is a flat 1.00x: every
    /// gate in the game stops doing anything measurable. Even below the cap the effect is
    /// fatal, because a round entered with 2,461 men is worth 1.9x against the 45x the same
    /// round pays at five men. The gate does not merely get weaker; it stops being an event.
    ///
    /// So a gate is now a SHARE of the army that walks into it. A recruit gate takes on 3%
    /// of your strength per weight, an ambush costs 13%, and both mean exactly as much at
    /// fifty men as at fifty billion. That is the only arithmetic under which "the army is
    /// continuous" and "the gate is worth steering for" are simultaneously true, and it is
    /// what lets the soft cap go: nothing runs away, because nothing is absolute.
    ///
    /// THE PLAYER STILL SEES HEADCOUNTS. A share is the mechanism, not the display —
    /// <see cref="Headcount"/> resolves a gate against the army in front of it, so the sign
    /// on a gate still reads "+340" and "-1,300" exactly as it did when those numbers were
    /// authored. What used to be the authored value is now a small WEIGHT, which is why the
    /// generator's 4 + 2*difficulty + step became a weight curve instead.
    ///
    /// FORCE IS A DOUBLE. It was a long, and at the measured growth of a competent player
    /// (~2.4x a round) a long overflows at round 52 — inside the sixteen acts of authored
    /// content, so this is not a theoretical ceiling. A double holds the same campaign with
    /// two hundred rounds to spare, is exact below 2^53 (nine quadrillion, far past any
    /// headcount a player will ever read individually), and needs no saturation reasoning
    /// at all. An army of ten billion men does not need to be exact to the man.
    /// </summary>
    public static class GateMath
    {
        /// <summary>Share of the army a recruit (+) gate takes on, per point of weight.</summary>
        public const double RecruitShare = 0.026;

        /// <summary>
        /// Share a rally (x) gate takes on, per point of weight — the golden arch.
        ///
        /// It is worth nearly seven recruit gates and is still nothing like the x2 it used
        /// to be, because a literal doubling and a continuous army cannot coexist. Measured
        /// over sixty rounds with x2 gates left in place, EVERY skill level from 0.70 to
        /// 1.00 lands within one order of magnitude of the same colossal number: the rally
        /// gates dominate so completely that nothing else the player does is detectable.
        /// Skill expression and the x2 arch are the same trade, and the arch lost.
        /// </summary>
        public const double RallyShare = 0.150;

        /// <summary>Share an ambush (-) gate takes OFF, per point of weight.</summary>
        public const double AmbushShare = 0.125;

        /// <summary>
        /// Extra ambush share per chunk into the round.
        ///
        /// The red side scales with depth and the green side does not — that asymmetry is
        /// the difficulty, and it is deliberate. A round's back half has to be able to cost
        /// you ground, or a continuous army is a ratchet with no tension in it.
        /// </summary>
        public const double AmbushDepthStep = 0.004;

        /// <summary>No single red gate may take more than this share, however deep the round.</summary>
        public const double AmbushShareMax = 0.62;

        /// <summary>
        /// What one gate multiplies the army by. This is the whole arithmetic; everything
        /// else in this file is presentation or clamping.
        /// </summary>
        public static double Factor(GateOp op, int weight, int depth)
        {
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight),
                "Gate weights are authored non-negative; the op carries the sign.");
            int w = Math.Max(0, weight);
            int d = Math.Max(0, depth);

            switch (op)
            {
                case GateOp.Add:
                    return 1.0 + RecruitShare * w;
                case GateOp.Multiply:
                    return 1.0 + RallyShare * w;
                case GateOp.Subtract:
                    double share = Math.Min(AmbushShareMax, (AmbushShare + AmbushDepthStep * d) * w);
                    return 1.0 - share;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op));
            }
        }

        /// <summary>Applies one gate. Force never goes negative and has no ceiling.</summary>
        public static double ApplyGate(double force, GateOp op, int weight, int depth)
        {
            if (double.IsNaN(force)) throw new ArgumentOutOfRangeException(nameof(force));
            double result = Math.Max(0.0, force) * Factor(op, weight, depth);
            return result < 0.0 ? 0.0 : result;
        }

        /// <summary>
        /// The signed number of men this gate will hand over or take away from the army in
        /// front of it — what goes on the sign.
        ///
        /// Rounded away from zero so a gate that does anything at all never reads "+0": a
        /// 3% recruit gate in front of ten men is worth 0.3 of a man, and printing zero on
        /// a gate the player is about to gain from is a lie about the mechanic.
        /// </summary>
        public static long Headcount(double force, GateOp op, int weight, int depth)
        {
            double delta = ApplyGate(force, op, weight, depth) - Math.Max(0.0, force);
            if (delta > 0.0 && delta < 1.0) return 1L;
            if (delta < 0.0 && delta > -1.0) return -1L;
            if (delta >= long.MaxValue) return long.MaxValue;
            if (delta <= long.MinValue) return long.MinValue;
            return (long)Math.Round(delta, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Applies a gate, then amplifies whatever it GAINED by the hero's gate-yield stat.
        ///
        /// Scaling the gain rather than the factor keeps one rule for both operators, which
        /// is the same property this had when gates were absolute: a recruit worth 30 men
        /// with 20% yield gives 36, and a rally worth 300 gives 360. A gate that COSTS force
        /// is untouched — yield is a reward, not a shield.
        /// </summary>
        public static double ApplyGateWithYield(double force, GateOp op, int weight, int depth,
            double gateYield)
        {
            double result = ApplyGate(force, op, weight, depth);
            double from = Math.Max(0.0, force);
            if (gateYield <= 0.0 || result <= from) return result;
            return from + (result - from) * (1.0 + gateYield);
        }

        /// <summary>
        /// Diminishing conversion of a run's surplus into a bonus multiplier (1.0 = none).
        ///
        /// This used to be fed by force spilled over a hard cap. There is no cap now, so the
        /// surplus it measures is how far the army grew DURING the round relative to what
        /// walked in — which is the same thing it was always trying to reward (a run played
        /// greedily) without needing a ceiling to detect it.
        /// </summary>
        public static float SurplusToBonusMultiplier(double finalForce, double startingForce)
        {
            if (finalForce <= 0.0 || startingForce <= 0.0) return 1f;
            double ratio = finalForce / startingForce;
            if (ratio <= 1.0) return 1f;
            // Doubling the army over a round grants +25%; rewarding, never balance-breaking.
            return 1f + 0.25f * (float)Math.Log(ratio, 2.0);
        }
    }
}
