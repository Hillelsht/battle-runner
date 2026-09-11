using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// One clash between the player's army and an enemy squad, resolved over about a second.
    ///
    /// WHAT WAS THERE BEFORE. A pack was five frozen bodies, and the frame the crowd's leading
    /// plane reached them it was `Defeat()`ed, its cost subtracted, and the object released to
    /// the pool. There was no window in which a battle could happen, because the whole event
    /// was one frame long. "add a real animation of the fighting" has no seam to hang on until
    /// that frame becomes a second.
    ///
    /// THE OUTCOME IS DELIBERATELY UNCHANGED. The bigger crowd wins and keeps the difference,
    /// which is exactly what `force -= cost` already did — so the balance, the talent maths and
    /// the whole tuned difficulty curve are untouched. What is new is that the subtraction is
    /// now SPREAD across the clash instead of landing in one frame, so the army visibly shrinks
    /// while it fights rather than teleporting to its new size.
    ///
    /// Engine-free, like the rest of Core: whether a clash resolves fairly, terminates, and
    /// conserves its own arithmetic is decidable without an editor, and this file is where it
    /// is decided.
    /// </summary>
    public readonly struct Melee
    {
        /// <summary>
        /// How long a clash takes. Long enough to read as a fight; short enough that the run
        /// does not stop for it — the army is still moving at 10 m/s throughout, so this is
        /// about twelve metres of road.
        /// </summary>
        public const float Duration = 1.15f;

        /// <summary>
        /// The most units that break formation to fight, however large the army.
        ///
        /// A five-hundred-strong army does not send five hundred men at nine skeletons, and
        /// if it did, the formation the whole game is about would dissolve every time a pack
        /// arrived. The fighters are a detachment from the front rank; everyone else keeps
        /// marching, which is also what makes the detachment read AS one.
        /// </summary>
        public const int MaxFighters = 24;

        public readonly long Allies;
        public readonly long Enemies;

        public Melee(long allies, long enemies)
        {
            Allies = allies < 0 ? 0 : allies;
            Enemies = enemies < 0 ? 0 : enemies;
        }

        /// <summary>The bigger crowd wins and keeps the difference.</summary>
        public long SurvivingAllies => Allies > Enemies ? Allies - Enemies : 0;
        public long SurvivingEnemies => Enemies > Allies ? Enemies - Allies : 0;
        public bool AlliesWin => Allies > Enemies;

        /// <summary>
        /// How many of each side are still standing at `t` seconds into the clash.
        ///
        /// Smoothstep rather than linear, and this is the one piece of feel in the file: a
        /// linear drain reads as a number ticking down, while a curve that starts slow, empties
        /// fast through the middle and eases out reads as two lines meeting, breaking, and one
        /// of them holding. The losing side is gone exactly when the clash ends, never before —
        /// an enemy squad that vanishes at 80% leaves the player's soldiers swinging at air.
        /// </summary>
        public void At(float seconds, out long allies, out long enemies)
        {
            float t = Duration <= 0f ? 1f : seconds / Duration;
            if (t <= 0f)
            {
                allies = Allies;
                enemies = Enemies;
                return;
            }
            if (t >= 1f)
            {
                allies = SurvivingAllies;
                enemies = SurvivingEnemies;
                return;
            }

            float k = t * t * (3f - 2f * t);
            allies = Allies - (long)Math.Round((Allies - SurvivingAllies) * (double)k);
            enemies = Enemies - (long)Math.Round((Enemies - SurvivingEnemies) * (double)k);

            // Rounding must never push a side below where it finishes or above where it
            // started: a count that dips under its own survivor total and comes back up is a
            // crowd that visibly flickers at the end of every fight.
            if (allies < SurvivingAllies) allies = SurvivingAllies;
            if (allies > Allies) allies = Allies;
            if (enemies < SurvivingEnemies) enemies = SurvivingEnemies;
            if (enemies > Enemies) enemies = Enemies;
        }

        /// <summary>
        /// How many units actually break formation, given how big the fight is.
        ///
        /// Never more than the army has, never more than the cap, and never more than about
        /// twice the enemy — sending twenty men at three is not a battle, it is a crowd
        /// standing around. Always at least one when there is anyone to fight, because a clash
        /// with no visible fighters is the bug this class exists to remove.
        /// </summary>
        public int Fighters(int displayedAllies)
        {
            if (Enemies <= 0 || displayedAllies <= 0) return 0;
            long want = Enemies * 2;
            if (want > MaxFighters) want = MaxFighters;
            if (want > displayedAllies) want = displayedAllies;
            return want < 1 ? 1 : (int)want;
        }
    }
}
