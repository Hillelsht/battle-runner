using System;

namespace BattleRunner.Core.Progression
{
    /// <summary>
    /// What a round pays.
    ///
    /// THE PROBLEM THIS SOLVES IS ARITHMETIC, NOT DESIGN. Until now every round ended in a
    /// boss fight and every boss paid `StatPointsPerBossKill + roundIndex / 2`. Making bosses
    /// rare — one fight every three to five rounds — would have cut talent income by roughly
    /// four times without a single line about rewards being touched, and the tree was sized
    /// against per-round income. A player would have reached round thirty with a quarter of
    /// the points the tree expects and concluded the tree was broken.
    ///
    /// So an act must pay at least what the same rounds used to pay. `LegacyIncome` is that
    /// old curve, kept precisely so a test can compare against it rather than against a
    /// number someone remembered.
    ///
    /// The SHAPE changes even though the total does not: a normal round pays a little and a
    /// boss round pays a lot. That is the point — three rounds of small change and then a
    /// windfall reads as a reward, where four identical payments read as a salary.
    /// </summary>
    public static class RoundRewards
    {
        /// <summary>Loot luck on a round that only threatened. Below a boss round, above nothing.</summary>
        public const float ThreatRoundLuck = 0.55f;

        /// <summary>
        /// Stat points a round pays. A boss round is worth roughly three normal ones, plus a
        /// bonus for how long the act made the player wait for it.
        /// </summary>
        public static int PointsFor(RoundPlan plan, int perBossKill)
        {
            int baseAward = Math.Max(1, perBossKill);
            if (plan.IsBossRound)
                return baseAward * 2 + plan.RoundIndex + plan.ActLength;

            // Deliberately not perBossKill/2: at the shipped value of 3 that rounds to 1, and
            // a round that pays one point on a tree of 200 spends does not feel like it paid.
            return Math.Max(1, baseAward * 2 / 3) + plan.RoundIndex * 2 / 5;
        }

        /// <summary>How hard the loot roll leans on the player's luck this round.</summary>
        public static float LootLuck(RoundPlan plan) => plan.IsBossRound ? 1f : ThreatRoundLuck;

        /// <summary>
        /// Whether a round guarantees something worth keeping. Only a fight does — otherwise
        /// there would be no reason to look forward to one beyond the spectacle.
        /// </summary>
        public static bool GuaranteesUpgrade(RoundPlan plan) => plan.IsBossRound;

        /// <summary>Everything one act pays, boss round included.</summary>
        public static int ActIncome(int firstRoundOfAct, int perBossKill)
        {
            RoundPlan first = RoundPlan.For(firstRoundOfAct);
            int total = 0;
            for (int i = 0; i < first.ActLength; i++)
                total += PointsFor(RoundPlan.For(firstRoundOfAct + i), perBossKill);
            return total;
        }

        /// <summary>
        /// What the game paid before bosses became rare: every round was a boss round and
        /// every one of them paid `perBossKill + roundIndex / 2`. Kept as the yardstick the
        /// new curve is tested against, so "income did not regress" is a fact rather than a
        /// recollection.
        /// </summary>
        public static int LegacyIncome(int fromRound, int rounds, int perBossKill)
        {
            int total = 0;
            for (int i = 0; i < Math.Max(0, rounds); i++)
            {
                int round = Math.Max(0, fromRound + i);
                total += Math.Max(1, perBossKill) + round / 2;
            }
            return total;
        }
    }
}
