using System;
using System.Collections.Generic;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Progression
{
    /// <summary>
    /// What a point is worth once the tree is full.
    ///
    /// The old tree emptied in three boss kills and every point after that vanished into
    /// nothing. Even a tree of 240 spends eventually ends, and a player who has spent
    /// thirty hours getting there is exactly the player who should not be told their next
    /// reward is meaningless. So the tree has a floor under it: six tracks, no cap.
    ///
    /// TWO RULES MAKE IT WORK AS A SINK RATHER THAN AS INFLATION.
    ///
    /// Cost escalates. Rank N costs 1 + N/CostStep points, so the tenth rank is cheap and
    /// the two hundredth is not. Without that, an endless track outruns the authored tree
    /// within an evening and every real decision in the game stops mattering.
    ///
    /// Value is a diminishing SUM, not a flat per-rank amount. Each rank is worth
    /// PerRank / (1 + rank/Falloff), so the total grows without bound — a point is never
    /// worthless — while the curve flattens hard enough that nobody outgrows the content
    /// by grinding. The total is closed-form and monotonic, which is what makes it
    /// testable without simulating a save.
    ///
    /// Engine-free: this is arithmetic and it should be pinned by dotnet test, not
    /// discovered on a phone.
    /// </summary>
    public static class Paragon
    {
        /// <summary>Ranks per +1 to the cost of the next one.</summary>
        public const int CostStep = 12;

        /// <summary>Ranks before a track's per-rank value has halved.</summary>
        public const float Falloff = 25f;

        /// <summary>One endless track. Kept alongside the tree's stats on purpose.</summary>
        public sealed class Track
        {
            public string Id { get; }
            public string StatId { get; }
            public string DisplayName { get; }
            /// <summary>Value of the FIRST rank; later ranks decay from here.</summary>
            public float PerRank { get; }

            public Track(string id, string statId, string displayName, float perRank)
            {
                Id = id;
                StatId = statId;
                DisplayName = displayName;
                PerRank = perRank;
            }
        }

        private static readonly Track[] TrackTable =
        {
            new Track("pg_might", StatIds.Damage, "Might", 4f),
            new Track("pg_vigor", StatIds.Health, "Vigor", 22f),
            new Track("pg_gates", StatIds.GateYield, "Gates", 0.02f),
            new Track("pg_fortune", StatIds.Fortune, "Fortune", 0.04f),
            new Track("pg_focus", StatIds.Cooldown, "Focus", 0.02f),
            new Track("pg_speed", StatIds.RunSpeed, "Speed", 0.012f)
        };

        public static IReadOnlyList<Track> Tracks => TrackTable;

        public static Track Find(string id)
        {
            if (id == null) return null;
            foreach (Track track in TrackTable)
                if (string.Equals(track.Id, id, StringComparison.Ordinal)) return track;
            return null;
        }

        /// <summary>Points the NEXT rank costs, given how many are already held overall.</summary>
        public static int NextRankCost(int totalRanksHeld) =>
            1 + Math.Max(0, totalRanksHeld) / CostStep;

        /// <summary>Total ranks across every track.</summary>
        public static int TotalRanks(IReadOnlyDictionary<string, int> paragonRanks)
        {
            if (paragonRanks == null) return 0;
            int total = 0;
            foreach (KeyValuePair<string, int> pair in paragonRanks)
                if (Find(pair.Key) != null) total += Math.Max(0, pair.Value);
            return total;
        }

        /// <summary>
        /// What a track is worth at a given rank: the sum of a diminishing series, so it
        /// always grows and never doubles back. Closed enough to test exactly.
        /// </summary>
        public static float ValueAt(Track track, int rank)
        {
            if (track == null || rank <= 0) return 0f;
            double total = 0.0;
            for (int r = 0; r < rank; r++) total += track.PerRank / (1.0 + r / (double)Falloff);
            return (float)total;
        }

        /// <summary>Every modifier the held paragon ranks contribute.</summary>
        public static List<StatModifier> ModifiersFor(IReadOnlyDictionary<string, int> paragonRanks)
        {
            var mods = new List<StatModifier>();
            if (paragonRanks == null) return mods;
            foreach (KeyValuePair<string, int> pair in paragonRanks)
            {
                Track track = Find(pair.Key);
                if (track == null) continue;
                float value = ValueAt(track, Math.Max(0, pair.Value));
                if (value <= 0f) continue;
                mods.Add(new StatModifier(track.StatId, ModifierKind.Flat, value));
            }
            return mods;
        }

        /// <summary>
        /// Why a rank cannot be bought, or null when it can. Paragon opens behind a
        /// keystone rather than behind "tree complete": reaching a keystone is the moment
        /// a build has an identity, and it is a far better place to hand someone an
        /// endless sink than the moment they have taken literally everything.
        /// </summary>
        public static string BlockedReason(string trackId,
            IReadOnlyDictionary<string, int> paragonRanks,
            IReadOnlyDictionary<string, int> skillRanks,
            int unspentPoints)
        {
            if (Find(trackId) == null) return "Unknown path";
            if (!SkillTree.AnyKeystoneTaken(skillRanks)) return "Reach a keystone first";
            int cost = NextRankCost(TotalRanks(paragonRanks));
            if (unspentPoints < cost) return $"Costs {cost} points";
            return null;
        }
    }
}
