namespace BattleRunner.Core.Run
{
    /// <summary>Transient state of one runner phase. Exists only during RunnerLoop.</summary>
    public sealed class RunState
    {
        public long ForceCount;
        public long OverflowAccumulated;
        public float Distance;
        public float SpellCooldownRemaining;
        public float ShieldCooldownRemaining;
        public int SpellCharges;
        public int GatesHit;

        /// <summary>
        /// Multiply gates landed back to back, for the Zealot chain talents. Reset by any
        /// gate that is not a multiply, so a subtract genuinely breaks the run rather than
        /// merely pausing it — that break is the decision the talent is about.
        /// </summary>
        public int MultiplyChain;

        /// <summary>Second Wind fires once per run, or it is not a comeback, it is immortality.</summary>
        public bool SecondWindSpent;

        public bool IsDefeated => ForceCount <= 0;
    }
}
