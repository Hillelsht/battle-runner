using System;
using System.Collections.Generic;

namespace BattleRunner.Core.Save
{
    /// <summary>
    /// Ordered schema migrations, wired from day 1 (doc 01, R8). JSON parsing happens
    /// in the service layer; migrations operate on the deserialized profile, where a
    /// field absent from old JSON arrives as its type default.
    /// </summary>
    public static class SaveMigrator
    {
        public const int CurrentVersion = 5;

        private static readonly Dictionary<int, Action<PlayerProfile>> Steps = new Dictionary<int, Action<PlayerProfile>>
        {
            // v1 -> v2: PityCounter and Keys introduced; old saves default them to 0,
            // and null lists from hand-edited or truncated saves are healed.
            [1] = profile =>
            {
                profile.Inventory ??= new List<GearItemInstance>();
                profile.Equipped ??= new List<EquippedSlot>();
                profile.StatPoints ??= new List<StatSpend>();
                if (profile.PityCounter < 0) profile.PityCounter = 0;
                if (profile.Keys < 0) profile.Keys = 0;
            },

            // v2 -> v3: TutorialMask introduced. An existing save belongs to someone who has
            // already played, so mark every step taught rather than coaching a veteran. A
            // genuinely new profile starts at 0 and gets the tutorial.
            [2] = profile => profile.TutorialMask = AllTutorialStepsTaught,

            // v3 -> v4: flat stat points became a skill tree. Refund whatever was spent as
            // unspent points rather than guessing an equivalent set of talents — the old
            // three stats have no faithful mapping onto the new nodes, and a free respec is
            // the honest trade.
            [3] = profile =>
            {
                profile.SkillNodes ??= new List<string>();
                if (profile.StatPoints != null)
                {
                    foreach (StatSpend spend in profile.StatPoints)
                        if (spend != null && spend.Points > 0) profile.UnspentStatPoints += spend.Points;
                    profile.StatPoints.Clear();
                }
            },

            // v4 -> v5: the tree gained RANKS, so a set of taken ids became a map of
            // id -> rank. Every old node was worth exactly one point, so each one migrates
            // to rank 1 — no refund needed and no build is lost.
            //
            // Ids that no longer exist are dropped rather than carried: the v5 tree renamed
            // and re-costed most of the table, and a rank pointing at nothing would be a
            // point the player paid for and can never see or reclaim. Their cost is handed
            // back as unspent points so the total stays honest.
            [4] = profile =>
            {
                profile.SkillRanks ??= new List<RankEntry>();
                profile.ParagonRanks ??= new List<RankEntry>();
                if (profile.SkillNodes == null) return;

                foreach (string id in profile.SkillNodes)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (Progression.SkillTree.Find(id) == null)
                    {
                        profile.UnspentStatPoints += 1;
                        continue;
                    }

                    bool merged = false;
                    foreach (RankEntry entry in profile.SkillRanks)
                    {
                        if (entry == null || entry.Id != id) continue;
                        entry.Rank += 1;
                        merged = true;
                        break;
                    }
                    if (!merged) profile.SkillRanks.Add(new RankEntry { Id = id, Rank = 1 });
                }
                profile.SkillNodes.Clear();
            }
        };

        /// <summary>Four steps: Steer, Gate, Spell, Shield. Kept here so Core has no
        /// dependency direction problem between Save and Tutorial.</summary>
        public const int AllTutorialStepsTaught = 0b1111;

        /// <summary>Runs every migration from the profile's version up to CurrentVersion.</summary>
        public static PlayerProfile Migrate(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.SchemaVersion > CurrentVersion)
                throw new NotSupportedException(
                    $"Save schema v{profile.SchemaVersion} is newer than this build supports (v{CurrentVersion}).");

            for (int v = Math.Max(1, profile.SchemaVersion); v < CurrentVersion; v++)
            {
                if (Steps.TryGetValue(v, out Action<PlayerProfile> step)) step(profile);
                profile.SchemaVersion = v + 1;
            }
            return profile;
        }
    }
}
