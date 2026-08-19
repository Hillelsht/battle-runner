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

        public bool IsDefeated => ForceCount <= 0;
    }
}
