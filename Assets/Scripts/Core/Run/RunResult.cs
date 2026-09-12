using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// The phase-to-phase contract (doc 01, R9): produced when the runner phase ends,
    /// consumed by the boss encounter and the loot phase. Both consumers must be fully
    /// drivable from a hand-authored instance of this class.
    /// </summary>
    public sealed class RunResult
    {
        /// <summary>The army at the finish line, and what it carries into the fight.</summary>
        public double FinalForceCount;

        /// <summary>
        /// The army that WALKED IN at the start of the round.
        ///
        /// Carried because the boss fight needs it twice and cannot reconstruct it: the
        /// blow that lands on the army is priced against the army that showed up rather
        /// than against whatever is left (or a big army would passively survive more
        /// blows), and the round's surplus — which used to be over-cap overflow — is the
        /// ratio between these two numbers.
        /// </summary>
        public double StartingForceCount;

        /// <summary>The largest the army was at any point during the round.</summary>
        public double PeakForceCount;

        public StatSheet HeroStats;
        public int SpellChargesRemaining;
        public float Distance;
        public int GatesHit;
        public bool ReachedBoss;

        /// <summary>Bonus multiplier earned by growing the army hard, applied to boss damage and loot luck.</summary>
        public float SurplusBonus() =>
            Talents.SurplusMultiplier(FinalForceCount, StartingForceCount,
                HeroStats?.Get(StatIds.OverflowBank) ?? 0f);
    }
}
