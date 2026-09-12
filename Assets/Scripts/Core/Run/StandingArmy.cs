using System;

namespace BattleRunner.Core.Run
{
    /// <summary>
    /// The army between rounds — what carries, what is at risk, and what can never be taken.
    ///
    /// THE COMPLAINT THIS ANSWERS. Every round began by setting the army to five men, so a
    /// player who finished a round with two thousand eight hundred started the next with
    /// five. The words were "now every round the crowd shrinks to minimum... once you
    /// achieved a certain level or crowd size you never lose it", and that is a description
    /// of a game that resets a thing the player thinks of as a level.
    ///
    /// TWO NUMBERS, AND THE DIFFERENCE BETWEEN THEM IS THE WHOLE DESIGN.
    ///
    ///   Banked   — the army as it actually stands, carried unbroken from the last round.
    ///              This is what makes the campaign continuous rather than a series of
    ///              disconnected sprints.
    ///   BestEver — the largest army ever fielded, a high-water mark that only ever rises.
    ///              <see cref="Floor"/> is a share of it, and the round can never start
    ///              below that floor.
    ///
    /// So a disastrous round costs real ground — up to <see cref="FloorShare"/>'s complement
    /// of everything above the floor, which is the stake that makes the red gates matter —
    /// and no sequence of disastrous rounds can ever put the player back at the beginning.
    /// Both halves of the request are satisfied at once, and neither is satisfied by either
    /// number alone: Banked without BestEver is a game that can ruin you permanently, and
    /// BestEver without Banked is a game where the last round did not matter.
    ///
    /// WHY THE FLOOR IS NOT 100%. A floor equal to the best ever fielded would make every
    /// loss purely notional — the army would be restored in full at the next round's start
    /// and nothing in the run could cost anything. That is not an RPG level, it is an
    /// invulnerability, and the difficulty this was asked for alongside would have nowhere
    /// to land.
    /// </summary>
    public static class StandingArmy
    {
        /// <summary>The army a brand new profile musters. The one absolute number left.</summary>
        public const double Seed = 5.0;

        /// <summary>
        /// The share of the best army ever fielded that is permanent.
        ///
        /// At 0.55 a player who peaks at ten thousand can never again start a round below
        /// five and a half thousand, whatever happens in between — so the worst run in the
        /// game costs somewhat less than half of a career, and the next one starts from a
        /// number that is still obviously theirs.
        /// </summary>
        public const double FloorShare = 0.55;

        /// <summary>The army that can never be taken, given the best ever fielded.</summary>
        public static double Floor(double bestEver) =>
            Math.Max(Seed, SafePositive(bestEver) * FloorShare);

        /// <summary>
        /// What a round starts with: whatever survived the last one, lifted to the floor.
        ///
        /// Monotone in BOTH arguments by construction, which is the property the promise
        /// rests on — no reachable pair of inputs produces a smaller army than a smaller
        /// pair would, so "it never shrinks to minimum" is a theorem about this function
        /// rather than a hope about the code that calls it.
        /// </summary>
        public static double StartOfRound(double banked, double bestEver) =>
            Math.Max(SafePositive(banked), Floor(bestEver));

        /// <summary>The high-water mark after a round that peaked at <paramref name="peak"/>.</summary>
        public static double Record(double bestEver, double peak) =>
            Math.Max(SafePositive(bestEver), SafePositive(peak));

        /// <summary>
        /// Rank, purely for display: how many doublings above the seed the army stands.
        ///
        /// The army itself spans ten orders of magnitude over a campaign and "63,590,221,004"
        /// is not a number anyone reads as progress. A rank that ticks over every doubling
        /// is, and it is the number the "you never lose a level" promise is legible in.
        /// </summary>
        public static int Rank(double force)
        {
            double f = SafePositive(force);
            if (f <= Seed) return 1;
            return 1 + (int)Math.Floor(Math.Log(f / Seed, 2.0));
        }

        private static double SafePositive(double value) =>
            double.IsNaN(value) || value < 0.0 ? 0.0 : value;
    }
}
