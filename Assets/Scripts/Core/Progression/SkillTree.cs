using System;
using System.Collections.Generic;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Progression
{
    /// <summary>The three ways to build a warlord. Each is a column in the tree.</summary>
    public enum SkillBranch
    {
        Warlord = 0, // hit the boss harder
        Warden = 1,  // survive the road
        Zealot = 2   // grow the army faster
    }

    /// <summary>One talent. Immutable; the player's choices live in a separate list of ids.</summary>
    public sealed class SkillNode
    {
        public string Id { get; }
        public SkillBranch Branch { get; }
        /// <summary>1, 2 or 3. Tier 2 needs tier 1 of the same branch, tier 3 needs both.</summary>
        public int Tier { get; }
        public string DisplayName { get; }
        public string Description { get; }
        /// <summary>Node in the same tier and branch that this one locks out, if any.</summary>
        public string Excludes { get; }
        public StatModifier[] Modifiers { get; }

        public SkillNode(string id, SkillBranch branch, int tier, string displayName, string description,
            string excludes, params StatModifier[] modifiers)
        {
            Id = id;
            Branch = branch;
            Tier = tier;
            DisplayName = displayName;
            Description = description;
            Excludes = excludes;
            Modifiers = modifiers ?? Array.Empty<StatModifier>();
        }
    }

    /// <summary>
    /// The talent tree: three branches of three tiers, one point per node, with an exclusive
    /// choice at tier 2 so a branch cannot be taken wholesale.
    ///
    /// It replaces three flat "+2 per point" stats whose shared flaw was that all three only
    /// mattered during the boss fight — nothing a player bought changed the forty seconds of
    /// running that is most of the game. The Zealot branch exists to fix exactly that.
    ///
    /// Engine-free and static: the shape of the tree is design, not content, and it wants to
    /// be exhaustively testable without an editor.
    /// </summary>
    public static class SkillTree
    {
        public const int PointCost = 1;

        private static readonly SkillNode[] NodeTable =
        {
            // --- Warlord: damage and spell, the boss killer -------------------------
            new SkillNode("wl_edge", SkillBranch.Warlord, 1, "Keen Edge",
                "+4 Might", null,
                new StatModifier(StatIds.Damage, ModifierKind.Flat, 4f)),
            new SkillNode("wl_cleave", SkillBranch.Warlord, 2, "Cleave",
                "+15% Might", "wl_execute",
                new StatModifier(StatIds.Damage, ModifierKind.Percent, 0.15f)),
            new SkillNode("wl_execute", SkillBranch.Warlord, 2, "Executioner",
                "+40% spell damage", "wl_cleave",
                new StatModifier(StatIds.SpellPower, ModifierKind.Percent, 0.40f)),
            new SkillNode("wl_annihilate", SkillBranch.Warlord, 3, "Annihilation",
                "+25% Might, +50% spell damage", null,
                new StatModifier(StatIds.Damage, ModifierKind.Percent, 0.25f),
                new StatModifier(StatIds.SpellPower, ModifierKind.Percent, 0.50f)),

            // --- Warden: staying alive on the road -----------------------------------
            new SkillNode("wd_hide", SkillBranch.Warden, 1, "Thick Hide",
                "+25 Vigor", null,
                new StatModifier(StatIds.Health, ModifierKind.Flat, 25f)),
            new SkillNode("wd_bulwark", SkillBranch.Warden, 2, "Bulwark",
                "Shield lasts 1s longer", "wd_thorns",
                new StatModifier(StatIds.ShieldDuration, ModifierKind.Flat, 1.0f)),
            new SkillNode("wd_thorns", SkillBranch.Warden, 2, "Bramble",
                "Enemy packs cost 35% less", "wd_bulwark",
                new StatModifier(StatIds.EnemyResist, ModifierKind.Flat, 0.35f)),
            new SkillNode("wd_undying", SkillBranch.Warden, 3, "Undying",
                "+60 Vigor, packs cost 25% less", null,
                new StatModifier(StatIds.Health, ModifierKind.Flat, 60f),
                new StatModifier(StatIds.EnemyResist, ModifierKind.Flat, 0.25f)),

            // --- Zealot: the branch that pays off during the RUN ---------------------
            new SkillNode("zl_avarice", SkillBranch.Zealot, 1, "Avarice",
                "+12% from every gate", null,
                new StatModifier(StatIds.GateYield, ModifierKind.Flat, 0.12f)),
            new SkillNode("zl_zeal", SkillBranch.Zealot, 2, "Zeal",
                "+12% speed, +8% from gates", "zl_fortune",
                new StatModifier(StatIds.RunSpeed, ModifierKind.Flat, 0.12f),
                new StatModifier(StatIds.GateYield, ModifierKind.Flat, 0.08f)),
            new SkillNode("zl_fortune", SkillBranch.Zealot, 2, "Fortune",
                "+30% rare loot", "zl_zeal",
                new StatModifier(StatIds.Fortune, ModifierKind.Flat, 0.30f)),
            new SkillNode("zl_multiply", SkillBranch.Zealot, 3, "Multiplication",
                "+25% from gates, -12% cooldowns", null,
                new StatModifier(StatIds.GateYield, ModifierKind.Flat, 0.25f),
                new StatModifier(StatIds.Cooldown, ModifierKind.Flat, 0.12f))
        };

        private static readonly Dictionary<string, SkillNode> ById = BuildIndex();

        private static Dictionary<string, SkillNode> BuildIndex()
        {
            var map = new Dictionary<string, SkillNode>();
            foreach (SkillNode node in NodeTable) map[node.Id] = node;
            return map;
        }

        public static IReadOnlyList<SkillNode> Nodes => NodeTable;

        public static SkillNode Find(string id) =>
            id != null && ById.TryGetValue(id, out SkillNode node) ? node : null;

        /// <summary>Nodes of one branch, tier order.</summary>
        public static List<SkillNode> Branch(SkillBranch branch)
        {
            var result = new List<SkillNode>();
            foreach (SkillNode node in NodeTable)
                if (node.Branch == branch) result.Add(node);
            result.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            return result;
        }

        /// <summary>Why a node cannot be taken, or null when it can.</summary>
        public static string BlockedReason(string nodeId, IReadOnlyCollection<string> taken, int unspentPoints)
        {
            SkillNode node = Find(nodeId);
            if (node == null) return "Unknown talent";
            if (Has(taken, nodeId)) return "Already learned";
            if (unspentPoints < PointCost) return "No points to spend";

            if (node.Excludes != null && Has(taken, node.Excludes))
                return $"{Find(node.Excludes).DisplayName} rules it out";

            int inBranch = CountInBranch(taken, node.Branch);
            if (node.Tier >= 2 && inBranch < node.Tier - 1)
                return node.Tier == 2
                    ? "Needs the first talent of this path"
                    : "Needs two talents of this path";

            return null;
        }

        public static bool CanTake(string nodeId, IReadOnlyCollection<string> taken, int unspentPoints) =>
            BlockedReason(nodeId, taken, unspentPoints) == null;

        /// <summary>
        /// Why a learned talent cannot be handed back, or null when it can.
        ///
        /// Removal is leaf-first. The tier gates count talents already held in the branch, so
        /// a node of tier T can only have been bought with T-1 others beside it — pulling one
        /// out from under a capstone would leave the capstone standing on nothing. Whatever
        /// would stop clearing its own bar is named, so the player knows what to drop first.
        ///
        /// Unlearning is free and unlimited: this is a tree the player meets three talents in,
        /// long before they can read it, and a build they cannot walk back is a trap, not a
        /// choice. Charging for a respec is a lever to pull once the tree is deep enough that
        /// commitment means something.
        /// </summary>
        public static string UnlearnBlockedReason(string nodeId, IReadOnlyCollection<string> taken)
        {
            SkillNode node = Find(nodeId);
            if (node == null) return "Unknown talent";
            if (!Has(taken, nodeId)) return "Not learned";

            int remaining = 0;
            foreach (string id in taken)
            {
                if (string.Equals(id, nodeId, StringComparison.Ordinal)) continue;
                SkillNode other = Find(id);
                if (other != null && other.Branch == node.Branch) remaining++;
            }

            SkillNode blocker = null;
            foreach (string id in taken)
            {
                if (string.Equals(id, nodeId, StringComparison.Ordinal)) continue;
                SkillNode other = Find(id);
                if (other == null || other.Branch != node.Branch) continue;
                if (remaining >= other.Tier) continue;
                if (blocker == null || other.Tier > blocker.Tier) blocker = other;
            }

            return blocker == null ? null : $"Unlearn {blocker.DisplayName} first";
        }

        public static bool CanUnlearn(string nodeId, IReadOnlyCollection<string> taken) =>
            UnlearnBlockedReason(nodeId, taken) == null;

        /// <summary>Every modifier the taken nodes contribute, for StatSheet.Resolve.</summary>
        public static List<StatModifier> ModifiersFor(IEnumerable<string> taken)
        {
            var mods = new List<StatModifier>();
            if (taken == null) return mods;
            foreach (string id in taken)
            {
                SkillNode node = Find(id);
                if (node != null) mods.AddRange(node.Modifiers);
            }
            return mods;
        }

        /// <summary>How many points a set of choices cost, for refunding on respec.</summary>
        public static int PointsSpent(IReadOnlyCollection<string> taken)
        {
            if (taken == null) return 0;
            int spent = 0;
            foreach (string id in taken)
                if (Find(id) != null) spent += PointCost;
            return spent;
        }

        public static int CountInBranch(IEnumerable<string> taken, SkillBranch branch)
        {
            int count = 0;
            if (taken == null) return 0;
            foreach (string id in taken)
            {
                SkillNode node = Find(id);
                if (node != null && node.Branch == branch) count++;
            }
            return count;
        }

        private static bool Has(IEnumerable<string> taken, string id)
        {
            if (taken == null) return false;
            foreach (string t in taken)
                if (string.Equals(t, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
