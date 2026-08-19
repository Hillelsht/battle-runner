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
        public long FinalForceCount;
        public long OverflowAccumulated;
        public StatSheet HeroStats;
        public int SpellChargesRemaining;
        public float Distance;
        public int GatesHit;
        public bool ReachedBoss;

        /// <summary>Bonus multiplier earned from over-cap gate chains, applied to boss damage and loot luck.</summary>
        public float OverflowBonus(long softCap) =>
            GateMath.OverflowToBonusMultiplier(OverflowAccumulated, softCap);
    }
}
