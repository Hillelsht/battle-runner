using System;
using System.Collections.Generic;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Progression
{
    /// <summary>The four ways to build a warlord. Each is a column in the tree.</summary>
    public enum SkillBranch
    {
        Warlord = 0,    // hit harder, end fights faster
        Warden = 1,     // survive the road and the boss
        Zealot = 2,     // grow the army faster
        Crossroads = 3  // hybrid nodes, gated on investment in TWO branches
    }

    /// <summary>
    /// What a node is FOR, which decides how it is drawn and how it is priced.
    ///
    /// The mix is the whole point. Minors are the connective tissue that let a player keep
    /// spending; notables are the ones they remember taking; keystones are the ones they
    /// build around. A tree of nothing but minors is a slot machine, and a tree of nothing
    /// but keystones has no texture between the decisions.
    /// </summary>
    public enum SkillKind
    {
        Minor = 0,
        Notable = 1,
        Keystone = 2
    }

    /// <summary>One talent. Immutable; the player's ranks live in a separate map.</summary>
    public sealed class SkillNode
    {
        public string Id { get; }
        public SkillBranch Branch { get; }
        /// <summary>1..6. A tier unlocks at TierUnlockCost * (tier - 1) points in the branch.</summary>
        public int Tier { get; }
        public SkillKind Kind { get; }
        public int MaxRanks { get; }
        public string DisplayName { get; }
        /// <summary>Reads per RANK for a minor, or the whole effect for a notable/keystone.</summary>
        public string Description { get; }
        /// <summary>Node in the same tier and branch this one locks out, if any.</summary>
        public string Excludes { get; }
        /// <summary>Applied once per rank taken.</summary>
        public StatModifier[] PerRank { get; }
        /// <summary>Crossroads only: the second branch that must also be invested in.</summary>
        public SkillBranch? RequiresAlso { get; }

        public SkillNode(string id, SkillBranch branch, int tier, SkillKind kind, int maxRanks,
            string displayName, string description, string excludes,
            SkillBranch? requiresAlso, params StatModifier[] perRank)
        {
            Id = id;
            Branch = branch;
            Tier = tier;
            Kind = kind;
            MaxRanks = Math.Max(1, maxRanks);
            DisplayName = displayName;
            Description = description;
            Excludes = excludes;
            RequiresAlso = requiresAlso;
            PerRank = perRank ?? Array.Empty<StatModifier>();
        }
    }

    /// <summary>
    /// The talent tree: four branches, six tiers, ranked nodes, and mutually exclusive
    /// keystones at the bottom of each path.
    ///
    /// IT REPLACES A TREE THAT RAN OUT IN THREE ROUNDS. The old shape was 3 branches of
    /// (1 + 2-exclusive + 1) = 12 nodes with only NINE takeable, at one point each, against
    /// an income of 3 points per boss. Three bosses emptied it, and from the fourth onward
    /// every point the player earned had nowhere to go.
    ///
    /// RANKS ARE HOW THIS GETS DEEP WITHOUT GETTING PADDED. Hundreds of individually
    /// authored "+2 Might" nodes is filler and a player feels it by tier three. Most nodes
    /// here take 3-5 ranks, so ~60 authored nodes carry well over 200 point-spends, and the
    /// authoring effort goes into the notables and keystones that are actually memorable.
    ///
    /// TIERS UNLOCK ON POINTS SPENT IN THE BRANCH, not on specific prerequisite nodes. That
    /// keeps the rules explainable in one sentence, lets a player route around a node they
    /// dislike, and means adding a node later cannot orphan an existing save.
    ///
    /// Engine-free and static: the shape of the tree is design, not content, and it wants to
    /// be exhaustively testable without an editor.
    /// </summary>
    public static class SkillTree
    {
        public const int PointCost = 1;

        /// <summary>Points that must be spent IN A BRANCH before its tier N opens.</summary>
        public const int TierUnlockCost = 4;

        public const int MaxTier = 6;

        private static StatModifier Flat(string stat, float value) =>
            new StatModifier(stat, ModifierKind.Flat, value);

        private static StatModifier Pct(string stat, float value) =>
            new StatModifier(stat, ModifierKind.Percent, value);

        private static SkillNode Minor(string id, SkillBranch b, int tier, int ranks,
            string name, string desc, params StatModifier[] perRank) =>
            new SkillNode(id, b, tier, SkillKind.Minor, ranks, name, desc, null, null, perRank);

        private static SkillNode Notable(string id, SkillBranch b, int tier,
            string name, string desc, string excludes, params StatModifier[] perRank) =>
            new SkillNode(id, b, tier, SkillKind.Notable, 1, name, desc, excludes, null, perRank);

        private static SkillNode Keystone(string id, SkillBranch b,
            string name, string desc, string excludes, params StatModifier[] perRank) =>
            new SkillNode(id, b, MaxTier, SkillKind.Keystone, 1, name, desc, excludes, null, perRank);

        private static SkillNode Hybrid(string id, SkillBranch primary, SkillBranch also, int tier,
            string name, string desc, params StatModifier[] perRank) =>
            new SkillNode(id, SkillBranch.Crossroads, tier, SkillKind.Notable, 1, name, desc,
                null, also, perRank);

        // Every fraction-valued stat is granted FLAT, and that is load-bearing. StatSheet
        // resolves final = (base + flat) * (1 + percent), and these stats have a base of 0 —
        // a Percent modifier would multiply nothing and the node would be silently inert.
        // Only Damage and Health, which have real bases, take Percent modifiers.
        private static readonly SkillNode[] NodeTable =
        {
            // ================= WARLORD — end the fight ==============================
            Minor("wl_edge", SkillBranch.Warlord, 1, 5, "Keen Edge", "+3 Might",
                Flat(StatIds.Damage, 3f)),
            Minor("wl_focus", SkillBranch.Warlord, 1, 5, "Cold Focus", "+3% Focus",
                Flat(StatIds.Cooldown, 0.03f)),
            Minor("wl_spite", SkillBranch.Warlord, 1, 5, "Spite", "+6% spell damage",
                Flat(StatIds.SpellPower, 0.06f)),

            Minor("wl_cleave", SkillBranch.Warlord, 2, 5, "Cleave", "+4% Might",
                Pct(StatIds.Damage, 0.04f)),
            Minor("wl_temper", SkillBranch.Warlord, 2, 5, "Temper", "+8% spell damage",
                Flat(StatIds.SpellPower, 0.08f)),
            Notable("wl_echo", SkillBranch.Warlord, 2, "Echoing Word",
                "12% chance the spell casts twice", "wl_lance",
                Flat(StatIds.SpellEcho, 0.12f)),
            Notable("wl_lance", SkillBranch.Warlord, 2, "Sunder",
                "+22% spell damage, +6 Might", "wl_echo",
                Flat(StatIds.SpellPower, 0.22f), Flat(StatIds.Damage, 6f)),

            Minor("wl_grind", SkillBranch.Warlord, 3, 5, "Whetstone", "+5 Might",
                Flat(StatIds.Damage, 5f)),
            Minor("wl_haste", SkillBranch.Warlord, 3, 5, "Quickened", "+4% Focus",
                Flat(StatIds.Cooldown, 0.04f)),
            Minor("wl_wrath", SkillBranch.Warlord, 3, 4, "Wrath", "+5% Might",
                Pct(StatIds.Damage, 0.05f)),
            Notable("wl_execute", SkillBranch.Warlord, 3, "Executioner",
                "A boss under 8% health dies outright", null,
                Flat(StatIds.Execute, 0.08f)),

            Minor("wl_ruin", SkillBranch.Warlord, 4, 5, "Ruin", "+7 Might",
                Flat(StatIds.Damage, 7f)),
            Minor("wl_surge", SkillBranch.Warlord, 4, 5, "Surge", "+10% spell damage",
                Flat(StatIds.SpellPower, 0.10f)),
            Notable("wl_reap", SkillBranch.Warlord, 4, "Reaper's Due",
                "Execute threshold +7%", "wl_swift",
                Flat(StatIds.Execute, 0.07f)),
            Notable("wl_swift", SkillBranch.Warlord, 4, "Swiftness",
                "+9% Focus, 10% spell echo", "wl_reap",
                Flat(StatIds.Cooldown, 0.09f), Flat(StatIds.SpellEcho, 0.10f)),

            Minor("wl_malice", SkillBranch.Warlord, 5, 5, "Malice", "+6% Might",
                Pct(StatIds.Damage, 0.06f)),
            Minor("wl_venom", SkillBranch.Warlord, 5, 5, "Venom", "+12% spell damage",
                Flat(StatIds.SpellPower, 0.12f)),
            Minor("wl_urgency", SkillBranch.Warlord, 5, 4, "Urgency", "+5% Focus",
                Flat(StatIds.Cooldown, 0.05f)),
            Notable("wl_carnage", SkillBranch.Warlord, 5, "Carnage",
                "+14 Might and +20% spell damage", null,
                Flat(StatIds.Damage, 14f), Flat(StatIds.SpellPower, 0.20f)),

            Keystone("wl_annihilation", SkillBranch.Warlord, "ANNIHILATION",
                "+40% Might, +60% spell damage", "wl_headsman|wl_tempest",
                Pct(StatIds.Damage, 0.40f), Flat(StatIds.SpellPower, 0.60f)),
            Keystone("wl_headsman", SkillBranch.Warlord, "THE HEADSMAN",
                "Execute threshold +20%", "wl_annihilation|wl_tempest",
                Flat(StatIds.Execute, 0.20f)),
            Keystone("wl_tempest", SkillBranch.Warlord, "TEMPEST",
                "45% spell echo, +25% Focus", "wl_annihilation|wl_headsman",
                Flat(StatIds.SpellEcho, 0.45f), Flat(StatIds.Cooldown, 0.25f)),

            // ================= WARDEN — survive ====================================
            Minor("wd_hide", SkillBranch.Warden, 1, 5, "Thick Hide", "+18 Vigor",
                Flat(StatIds.Health, 18f)),
            Minor("wd_guard", SkillBranch.Warden, 1, 5, "Guarded", "Packs cost 4% less",
                Flat(StatIds.EnemyResist, 0.04f)),
            Minor("wd_brace", SkillBranch.Warden, 1, 5, "Brace", "Shield holds 0.2s longer",
                Flat(StatIds.ShieldDuration, 0.2f)),

            Minor("wd_plate", SkillBranch.Warden, 2, 5, "Plated", "+4% Vigor",
                Pct(StatIds.Health, 0.04f)),
            Minor("wd_bramble", SkillBranch.Warden, 2, 5, "Bramble", "Packs cost 5% less",
                Flat(StatIds.EnemyResist, 0.05f)),
            Notable("wd_shatter", SkillBranch.Warden, 2, "Shatterguard",
                "14% chance a pack shatters and costs nothing", "wd_bulwark",
                Flat(StatIds.PackShatter, 0.14f)),
            Notable("wd_bulwark", SkillBranch.Warden, 2, "Bulwark",
                "Shield holds 1s longer, +30 Vigor", "wd_shatter",
                Flat(StatIds.ShieldDuration, 1.0f), Flat(StatIds.Health, 30f)),

            Minor("wd_stone", SkillBranch.Warden, 3, 5, "Stoneblood", "+24 Vigor",
                Flat(StatIds.Health, 24f)),
            Minor("wd_ward", SkillBranch.Warden, 3, 5, "Warded", "Shield holds 0.25s longer",
                Flat(StatIds.ShieldDuration, 0.25f)),
            Minor("wd_scar", SkillBranch.Warden, 3, 4, "Scarred", "Packs cost 5% less",
                Flat(StatIds.EnemyResist, 0.05f)),
            Notable("wd_reflect", SkillBranch.Warden, 3, "Riposte",
                "A blocked blow returns 25% of it to the boss", null,
                Flat(StatIds.ShieldReflect, 0.25f)),

            Minor("wd_bastion", SkillBranch.Warden, 4, 5, "Bastion", "+6% Vigor",
                Pct(StatIds.Health, 0.06f)),
            Minor("wd_thorns", SkillBranch.Warden, 4, 5, "Thorns", "12% reflect",
                Flat(StatIds.ShieldReflect, 0.12f)),
            Notable("wd_wind", SkillBranch.Warden, 4, "Second Wind",
                "Once per run, death restores 30% of your army", "wd_aegis",
                Flat(StatIds.SecondWind, 0.30f)),
            Notable("wd_aegis", SkillBranch.Warden, 4, "Aegis",
                "Shield holds 1.4s longer, 18% shatter", "wd_wind",
                Flat(StatIds.ShieldDuration, 1.4f), Flat(StatIds.PackShatter, 0.18f)),

            Minor("wd_iron", SkillBranch.Warden, 5, 5, "Ironbound", "+34 Vigor",
                Flat(StatIds.Health, 34f)),
            Minor("wd_deny", SkillBranch.Warden, 5, 5, "Denial", "Packs cost 6% less",
                Flat(StatIds.EnemyResist, 0.06f)),
            Minor("wd_shell", SkillBranch.Warden, 5, 4, "Shell", "10% shatter",
                Flat(StatIds.PackShatter, 0.10f)),
            Notable("wd_unbroken", SkillBranch.Warden, 5, "Unbroken",
                "+70 Vigor, packs cost 12% less", null,
                Flat(StatIds.Health, 70f), Flat(StatIds.EnemyResist, 0.12f)),

            Keystone("wd_undying", SkillBranch.Warden, "UNDYING",
                "+120 Vigor, second wind restores 55%", "wd_immovable|wd_mirror",
                Flat(StatIds.Health, 120f), Flat(StatIds.SecondWind, 0.55f)),
            Keystone("wd_immovable", SkillBranch.Warden, "IMMOVABLE",
                "Packs cost 40% less, 30% shatter", "wd_undying|wd_mirror",
                Flat(StatIds.EnemyResist, 0.40f), Flat(StatIds.PackShatter, 0.30f)),
            Keystone("wd_mirror", SkillBranch.Warden, "MIRROR OF THORNS",
                "Blocked blows return 90%, shield holds 2s longer", "wd_undying|wd_immovable",
                Flat(StatIds.ShieldReflect, 0.90f), Flat(StatIds.ShieldDuration, 2.0f)),

            // ================= ZEALOT — grow the army ==============================
            Minor("zl_avarice", SkillBranch.Zealot, 1, 5, "Avarice", "+3% from every gate",
                Flat(StatIds.GateYield, 0.03f)),
            Minor("zl_stride", SkillBranch.Zealot, 1, 5, "Stride", "+2% speed",
                Flat(StatIds.RunSpeed, 0.02f)),
            Minor("zl_luck", SkillBranch.Zealot, 1, 5, "Fortune", "+6% rare loot",
                Flat(StatIds.Fortune, 0.06f)),

            Minor("zl_greed", SkillBranch.Zealot, 2, 5, "Greed", "+4% from every gate",
                Flat(StatIds.GateYield, 0.04f)),
            Minor("zl_lodestone", SkillBranch.Zealot, 2, 5, "Lodestone",
                "Gates reach 0.12 m wider", Flat(StatIds.Magnetism, 0.12f)),
            Notable("zl_crit", SkillBranch.Zealot, 2, "Zealotry",
                "10% chance a gate counts double", "zl_zeal",
                Flat(StatIds.GateCrit, 0.10f)),
            Notable("zl_zeal", SkillBranch.Zealot, 2, "Zeal",
                "+12% speed, +8% from gates", "zl_crit",
                Flat(StatIds.RunSpeed, 0.12f), Flat(StatIds.GateYield, 0.08f)),

            Minor("zl_tithe", SkillBranch.Zealot, 3, 5, "Tithe", "+5% from every gate",
                Flat(StatIds.GateYield, 0.05f)),
            Minor("zl_omen", SkillBranch.Zealot, 3, 5, "Omen", "+8% rare loot",
                Flat(StatIds.Fortune, 0.08f)),
            Minor("zl_fervour", SkillBranch.Zealot, 3, 4, "Fervour", "4% gate crit",
                Flat(StatIds.GateCrit, 0.04f)),
            Notable("zl_chain", SkillBranch.Zealot, 3, "Cascade",
                "Each consecutive x gate adds 15% more", null,
                Flat(StatIds.ChainMultiply, 0.15f)),

            Minor("zl_hunger", SkillBranch.Zealot, 4, 5, "Hunger", "+6% from every gate",
                Flat(StatIds.GateYield, 0.06f)),
            Minor("zl_pull", SkillBranch.Zealot, 4, 5, "Pull", "Gates reach 0.15 m wider",
                Flat(StatIds.Magnetism, 0.15f)),
            Notable("zl_hoard", SkillBranch.Zealot, 4, "Hoard",
                "Overflow past the cap returns 25% as boss damage", "zl_swarm",
                Flat(StatIds.OverflowBank, 0.25f)),
            Notable("zl_swarm", SkillBranch.Zealot, 4, "Swarm",
                "+18% from gates, 12% gate crit", "zl_hoard",
                Flat(StatIds.GateYield, 0.18f), Flat(StatIds.GateCrit, 0.12f)),

            Minor("zl_rapture", SkillBranch.Zealot, 5, 5, "Rapture", "+7% from every gate",
                Flat(StatIds.GateYield, 0.07f)),
            Minor("zl_frenzy", SkillBranch.Zealot, 5, 5, "Frenzy", "+3% speed",
                Flat(StatIds.RunSpeed, 0.03f)),
            Minor("zl_cascade", SkillBranch.Zealot, 5, 4, "Spiral", "+8% chain",
                Flat(StatIds.ChainMultiply, 0.08f)),
            Notable("zl_legion", SkillBranch.Zealot, 5, "Legion",
                "+25% from gates, +20% rare loot", null,
                Flat(StatIds.GateYield, 0.25f), Flat(StatIds.Fortune, 0.20f)),

            Keystone("zl_multiplication", SkillBranch.Zealot, "MULTIPLICATION",
                "+55% from gates, 25% gate crit", "zl_avalanche|zl_covetous",
                Flat(StatIds.GateYield, 0.55f), Flat(StatIds.GateCrit, 0.25f)),
            Keystone("zl_avalanche", SkillBranch.Zealot, "AVALANCHE",
                "Chain +60%, gates reach 0.5 m wider", "zl_multiplication|zl_covetous",
                Flat(StatIds.ChainMultiply, 0.60f), Flat(StatIds.Magnetism, 0.5f)),
            Keystone("zl_covetous", SkillBranch.Zealot, "COVETOUS",
                "Overflow returns 90%, +60% rare loot", "zl_multiplication|zl_avalanche",
                Flat(StatIds.OverflowBank, 0.90f), Flat(StatIds.Fortune, 0.60f)),

            // ================= CROSSROADS — hybrids ================================
            // Gated on investment in TWO branches, so they reward committing rather than
            // dabbling. Tier here means "points needed in EACH of the two branches".
            Hybrid("cr_warpriest", SkillBranch.Warlord, SkillBranch.Zealot, 2, "War Priest",
                "+10 Might, +8% from gates",
                Flat(StatIds.Damage, 10f), Flat(StatIds.GateYield, 0.08f)),
            Hybrid("cr_crusader", SkillBranch.Warlord, SkillBranch.Warden, 2, "Crusader",
                "+40 Vigor, +12% spell damage",
                Flat(StatIds.Health, 40f), Flat(StatIds.SpellPower, 0.12f)),
            Hybrid("cr_pilgrim", SkillBranch.Warden, SkillBranch.Zealot, 2, "Pilgrim",
                "Packs cost 10% less, +6% speed",
                Flat(StatIds.EnemyResist, 0.10f), Flat(StatIds.RunSpeed, 0.06f)),
            Hybrid("cr_martyr", SkillBranch.Warlord, SkillBranch.Warden, 4, "Martyr",
                "Blocked blows return 30%, +12 Might",
                Flat(StatIds.ShieldReflect, 0.30f), Flat(StatIds.Damage, 12f)),
            Hybrid("cr_prophet", SkillBranch.Warlord, SkillBranch.Zealot, 4, "Prophet",
                "Overflow returns 30%, +18% spell damage",
                Flat(StatIds.OverflowBank, 0.30f), Flat(StatIds.SpellPower, 0.18f)),
            Hybrid("cr_shepherd", SkillBranch.Warden, SkillBranch.Zealot, 4, "Shepherd",
                "18% shatter, +14% from gates",
                Flat(StatIds.PackShatter, 0.18f), Flat(StatIds.GateYield, 0.14f)),
            Hybrid("cr_apostle", SkillBranch.Warlord, SkillBranch.Zealot, 5, "Apostle",
                "Execute +6%, chain +20%",
                Flat(StatIds.Execute, 0.06f), Flat(StatIds.ChainMultiply, 0.20f)),
            Hybrid("cr_paladin", SkillBranch.Warlord, SkillBranch.Warden, 5, "Paladin",
                "+80 Vigor, +18 Might, shield holds 0.8s longer",
                Flat(StatIds.Health, 80f), Flat(StatIds.Damage, 18f),
                Flat(StatIds.ShieldDuration, 0.8f))
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

        /// <summary>Every takeable rank in the tree — the ceiling before paragon begins.</summary>
        public static int TotalRanks()
        {
            int total = 0;
            foreach (SkillNode node in NodeTable) total += node.MaxRanks;
            return total;
        }

        /// <summary>Nodes of one branch in tier order, then by id so the layout is stable.</summary>
        public static List<SkillNode> Branch(SkillBranch branch)
        {
            var result = new List<SkillNode>();
            foreach (SkillNode node in NodeTable)
                if (node.Branch == branch) result.Add(node);
            result.Sort((a, b) =>
            {
                int byTier = a.Tier.CompareTo(b.Tier);
                return byTier != 0 ? byTier : string.CompareOrdinal(a.Id, b.Id);
            });
            return result;
        }

        public static int RankOf(IReadOnlyDictionary<string, int> ranks, string nodeId)
        {
            if (ranks == null || nodeId == null) return 0;
            return ranks.TryGetValue(nodeId, out int rank) ? Math.Max(0, rank) : 0;
        }

        /// <summary>Points sunk into one branch. Crossroads nodes count toward neither.</summary>
        public static int PointsIn(IReadOnlyDictionary<string, int> ranks, SkillBranch branch)
        {
            if (ranks == null) return 0;
            int total = 0;
            foreach (KeyValuePair<string, int> pair in ranks)
            {
                SkillNode node = Find(pair.Key);
                if (node != null && node.Branch == branch) total += Math.Max(0, pair.Value);
            }
            return total;
        }

        /// <summary>
        /// Points sunk into a branch STRICTLY ABOVE a tier — the currency a tier gate is
        /// paid in.
        ///
        /// Counting the whole branch instead would make a gate self-satisfying, and worse,
        /// it would make the tree impossible to unwind: two tier-3 nodes sitting on exactly
        /// eight branch points block each other's refund forever, and the only way out is
        /// FORGET ALL. Measuring only what is BELOW a node means the deepest thing a player
        /// holds is always refundable, so any tree can be walked back one rank at a time.
        /// </summary>
        public static int PointsBelowTier(IReadOnlyDictionary<string, int> ranks,
            SkillBranch branch, int tier)
        {
            if (ranks == null) return 0;
            int total = 0;
            foreach (KeyValuePair<string, int> pair in ranks)
            {
                SkillNode node = Find(pair.Key);
                if (node != null && node.Branch == branch && node.Tier < tier)
                    total += Math.Max(0, pair.Value);
            }
            return total;
        }

        /// <summary>Every point sunk into the tree, for refunds and for paragon's gate.</summary>
        public static int PointsSpent(IReadOnlyDictionary<string, int> ranks)
        {
            if (ranks == null) return 0;
            int total = 0;
            foreach (KeyValuePair<string, int> pair in ranks)
                if (Find(pair.Key) != null) total += Math.Max(0, pair.Value);
            return total;
        }

        /// <summary>True once any keystone is held — the gate paragon opens behind.</summary>
        public static bool AnyKeystoneTaken(IReadOnlyDictionary<string, int> ranks)
        {
            if (ranks == null) return false;
            foreach (KeyValuePair<string, int> pair in ranks)
            {
                if (pair.Value <= 0) continue;
                SkillNode node = Find(pair.Key);
                if (node != null && node.Kind == SkillKind.Keystone) return true;
            }
            return false;
        }

        /// <summary>Why the next rank of a node cannot be bought, or null when it can.</summary>
        public static string BlockedReason(string nodeId, IReadOnlyDictionary<string, int> ranks,
            int unspentPoints)
        {
            SkillNode node = Find(nodeId);
            if (node == null) return "Unknown talent";
            if (RankOf(ranks, nodeId) >= node.MaxRanks) return "Fully ranked";
            if (unspentPoints < PointCost) return "No points to spend";

            // Exclusions are a bar-separated list so a keystone can rule out its two rivals.
            if (node.Excludes != null)
            {
                foreach (string other in node.Excludes.Split('|'))
                {
                    if (RankOf(ranks, other) <= 0) continue;
                    SkillNode blocker = Find(other);
                    return $"{(blocker != null ? blocker.DisplayName : other)} rules it out";
                }
            }

            if (node.Branch == SkillBranch.Crossroads)
            {
                // A hybrid asks for real investment on BOTH sides, which is what stops it
                // from being a free extra node everyone takes.
                int need = node.Tier * TierUnlockCost;
                SkillBranch also = node.RequiresAlso ?? SkillBranch.Warlord;
                SkillBranch primary = PrimaryOf(node);
                if (PointsIn(ranks, primary) < need || PointsIn(ranks, also) < need)
                    return $"Needs {need} points in {Name(primary)} and {Name(also)}";
                return null;
            }

            int required = (node.Tier - 1) * TierUnlockCost;
            if (PointsBelowTier(ranks, node.Branch, node.Tier) < required)
                return $"Needs {required} points in {Name(node.Branch)}";

            return null;
        }

        public static bool CanRank(string nodeId, IReadOnlyDictionary<string, int> ranks,
            int unspentPoints) => BlockedReason(nodeId, ranks, unspentPoints) == null;

        /// <summary>
        /// Why a rank cannot be handed back, or null when it can.
        ///
        /// Removal is leaf-first: dropping a point can close a tier the player is standing
        /// on, so the check is "would anything I hold stop qualifying", and the offender is
        /// named so they know what to unlearn first. Unlearning stays free and unlimited —
        /// a tree this size met three talents in is a trap if the player cannot walk it back.
        /// </summary>
        public static string UnlearnBlockedReason(string nodeId,
            IReadOnlyDictionary<string, int> ranks)
        {
            SkillNode node = Find(nodeId);
            if (node == null) return "Unknown talent";
            if (RankOf(ranks, nodeId) <= 0) return "Not learned";
            if (node.Branch == SkillBranch.Crossroads) return null;

            // The rank comes out of exactly one tier, so it can only ever starve something
            // DEEPER in the same branch, or a Crossroads node leaning on the branch total.
            // Nothing at this tier or above it is affected, which is what guarantees the
            // deepest thing held is always refundable and the tree can never lock up.
            int branchAfter = PointsIn(ranks, node.Branch) - 1;
            SkillNode blocker = null;
            foreach (KeyValuePair<string, int> pair in ranks)
            {
                if (pair.Value <= 0) continue;
                SkillNode held = Find(pair.Key);
                if (held == null) continue;

                if (held.Branch == node.Branch)
                {
                    if (held.Tier <= node.Tier) continue;
                    if (PointsBelowTier(ranks, node.Branch, held.Tier) - 1
                        >= (held.Tier - 1) * TierUnlockCost) continue;
                }
                else if (held.Branch == SkillBranch.Crossroads)
                {
                    SkillBranch primary = PrimaryOf(held);
                    SkillBranch also = held.RequiresAlso ?? primary;
                    if (primary != node.Branch && also != node.Branch) continue;
                    if (branchAfter >= held.Tier * TierUnlockCost) continue;
                }
                else continue;

                if (blocker == null || held.Tier > blocker.Tier) blocker = held;
            }

            return blocker == null ? null : $"Unlearn {blocker.DisplayName} first";
        }

        public static bool CanUnlearn(string nodeId, IReadOnlyDictionary<string, int> ranks) =>
            UnlearnBlockedReason(nodeId, ranks) == null;

        /// <summary>Every modifier the held ranks contribute, for StatSheet.Resolve.</summary>
        public static List<StatModifier> ModifiersFor(IReadOnlyDictionary<string, int> ranks)
        {
            var mods = new List<StatModifier>();
            if (ranks == null) return mods;
            foreach (KeyValuePair<string, int> pair in ranks)
            {
                SkillNode node = Find(pair.Key);
                if (node == null) continue;
                int rank = Math.Min(Math.Max(0, pair.Value), node.MaxRanks);
                for (int r = 0; r < rank; r++) mods.AddRange(node.PerRank);
            }
            return mods;
        }

        /// <summary>
        /// A Crossroads node stores its two requirements in RequiresAlso plus this — the
        /// id prefix names the branch it leans on first, which keeps the table readable.
        /// </summary>
        public static SkillBranch PrimaryOf(SkillNode node)
        {
            if (node == null || node.Branch != SkillBranch.Crossroads) return SkillBranch.Warlord;
            SkillBranch also = node.RequiresAlso ?? SkillBranch.Warlord;
            // The pairing is unordered, so "primary" is simply the other one of the pair.
            // Warden/Zealot hybrids lean Warden; everything else leans Warlord.
            if (also == SkillBranch.Zealot)
                return node.Id.StartsWith("cr_pilgrim", StringComparison.Ordinal)
                    || node.Id.StartsWith("cr_shepherd", StringComparison.Ordinal)
                    ? SkillBranch.Warden : SkillBranch.Warlord;
            return SkillBranch.Warlord;
        }

        public static string Name(SkillBranch branch) => branch switch
        {
            SkillBranch.Warlord => "Warlord",
            SkillBranch.Warden => "Warden",
            SkillBranch.Zealot => "Zealot",
            _ => "Crossroads"
        };
    }
}
