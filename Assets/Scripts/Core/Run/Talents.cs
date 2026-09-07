using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// The arithmetic behind the tree's nine new mechanics.
    ///
    /// THE POINT OF THIS FILE IS THAT THE TREE HOOKS INTO VERBS, NOT INTO NUMBERS. A tree
    /// of "+3 Might" fifty times over is a slot machine; what makes a build feel like a
    /// build is a rule that changes — a gate that sometimes pays double, a chain that
    /// escalates, a blocked blow that hits back. Those rules are decisions, so they live
    /// here in Core where they can be pinned by dotnet test, and the engine layer supplies
    /// only the two things Core cannot have: a random number and a place to draw.
    ///
    /// EVERY ROLL IS PASSED IN, NEVER TAKEN. A function that calls Random internally is a
    /// function that can only be tested statistically, and a mechanic whose edge cases are
    /// only ever sampled is a mechanic whose edge cases ship. Rolls arrive as a float in
    /// [0,1) and the caller owns the generator.
    ///
    /// EVERY MECHANIC IS INERT AT ZERO. A player who has not taken the talent must get
    /// byte-identical behaviour to the game before the talent existed, which is what makes
    /// it safe to thread these through paths the whole game already runs.
    /// </summary>
    public static class Talents
    {
        /// <summary>Longest run of multiply gates a chain still pays for.</summary>
        public const int MaxChainLength = 5;

        /// <summary>A chance stat against a roll in [0,1). Zero chance never fires.</summary>
        public static bool Rolls(float chance, float roll) => chance > 0f && roll < chance;

        /// <summary>
        /// Extra gate yield from an unbroken run of multiply gates.
        ///
        /// <paramref name="chainLength"/> counts the multiplies ALREADY landed, so the
        /// first one in a run pays nothing extra and the escalation is something the player
        /// builds rather than something they are handed. Capped, because the reward here is
        /// compounding on top of an operator that already compounds.
        /// </summary>
        public static float ChainYield(int chainLength, float chainMultiply)
        {
            if (chainMultiply <= 0f || chainLength <= 0) return 0f;
            int length = Math.Min(chainLength, MaxChainLength);
            return chainMultiply * length;
        }

        /// <summary>
        /// One gate, with yield, chain escalation and a possible critical.
        ///
        /// A critical DOUBLES THE GAIN rather than the printed value, which is the only
        /// definition that behaves the same for both operators: a +10 that gained 12 gains
        /// 24, and a x3 on 50 that gained 100 gains 200. Since ApplyGateWithYield already
        /// scales the gain by (1 + yield), doubling it is exactly yield' = 2*yield + 1 —
        /// no second code path, and losses stay untouched because a crit is a reward.
        /// </summary>
        public static long ApplyGate(long force, GateOp op, int value, long softCap,
            float gateYield, float chainYield, bool critical, out long overflow)
        {
            float yield = Math.Max(0f, gateYield) + Math.Max(0f, chainYield);
            if (critical) yield = yield * 2f + 1f;
            return GateMath.ApplyGateWithYield(force, op, value, softCap, yield, out overflow);
        }

        /// <summary>
        /// Whether a gate is worth being dragged toward. Magnetism must never pull a player
        /// into a subtract gate they successfully dodged — a talent that makes you worse at
        /// the game is a bug however it is worded.
        /// </summary>
        public static bool IsBeneficial(GateOp op, int value) => op switch
        {
            GateOp.Add => value > 0,
            GateOp.Multiply => value > 1,
            _ => false
        };

        /// <summary>
        /// What a pack actually costs. Resist is capped so a pack always bites for at least
        /// one unit; a shattered pack costs nothing at all, which is the whole fantasy.
        /// </summary>
        public static long PackBite(int forceCost, float resist, bool shattered)
        {
            if (shattered || forceCost <= 0) return 0L;
            double kept = 1.0 - Math.Min(0.85f, Math.Max(0f, resist));
            return (long)Math.Ceiling(forceCost * kept);
        }

        /// <summary>
        /// Banked overflow. The base curve is unchanged at zero bank, and the talent scales
        /// how much of the over-cap force comes back as damage rather than changing the
        /// shape — the log is what keeps a runaway multiply chain from ending the boss on
        /// arrival, and that property should survive every talent.
        /// </summary>
        public static float OverflowMultiplier(long accumulatedOverflow, long softCap, float bank)
        {
            if (accumulatedOverflow <= 0 || softCap <= 0) return 1f;
            double ratio = (double)accumulatedOverflow / softCap;
            float baseBonus = 0.25f * (float)Math.Log(1.0 + ratio, 2.0);
            return 1f + baseBonus * (1f + Math.Max(0f, bank));
        }

        /// <summary>A boss this far into its last breath simply falls over.</summary>
        public static bool Executes(float bossHp, float bossHpMax, float threshold)
        {
            if (threshold <= 0f || bossHpMax <= 0f) return false;
            return bossHp > 0f && bossHp <= bossHpMax * threshold;
        }

        /// <summary>
        /// The army a second wind leaves standing, or zero when the talent is not held.
        ///
        /// Deliberately measured against the level's par rather than against what the
        /// player had: reviving proportionally to a crowd that just hit zero is a rounding
        /// error, and the point of the talent is a second chance, not a formality.
        /// </summary>
        public static long SecondWindForce(long parForceAtFinish, float fraction)
        {
            if (fraction <= 0f) return 0L;
            long revived = (long)Math.Round(Math.Max(0L, parForceAtFinish) * (double)fraction);
            return Math.Max(10L, revived);
        }

        /// <summary>
        /// Damage a blocked blow throws back. Scaled off the spell's damage so it lands on a
        /// magnitude the encounter is already balanced around, instead of inventing a
        /// second one that would need its own tuning pass.
        /// </summary>
        public static float ReflectedDamage(float spellDamage, float reflect) =>
            Math.Max(0f, spellDamage) * Math.Max(0f, reflect);
    }
}
