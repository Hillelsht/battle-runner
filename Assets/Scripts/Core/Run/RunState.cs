namespace BattleRunner.Core.Run
{
    /// <summary>Transient state of one runner phase. Exists only during RunnerLoop.</summary>
    public sealed class RunState
    {
        /// <summary>The army, carried in from the last round rather than reset to five.</summary>
        public double ForceCount;

        /// <summary>What walked in, kept so the round's surplus and the boss blow can be priced.</summary>
        public double StartingForce;

        /// <summary>The largest this round ever got — what the permanent floor is recorded from.</summary>
        public double PeakForce;
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

        /// <summary>
        /// Records the army after a change, keeping the round's high-water mark honest.
        /// Every write to ForceCount goes through here so the peak cannot be missed by a
        /// caller that forgets — and the peak is what the permanent floor is built from.
        /// </summary>
        public void SetForce(double force)
        {
            ForceCount = force < 0.0 ? 0.0 : force;
            if (ForceCount > PeakForce) PeakForce = ForceCount;
        }

        public bool IsDefeated => ForceCount <= 0.0;
    }
}
