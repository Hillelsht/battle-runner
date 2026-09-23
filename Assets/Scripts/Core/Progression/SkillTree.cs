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
        /// <summary>
        /// The name and the description are KEYS, resolved on read.
        ///
        /// Properties rather than stored strings so the talent screen, the refusal messages and
        /// the stat summary all keep reading `.DisplayName` and get it in whatever language is
        /// on screen. NodeTable is a static table built once; resolved text here would be baked
        /// at construction and could never change again.
        /// </summary>
        public Text.LocKey NameKey { get; }
        public Text.LocKey DescKey { get; }

        public string DisplayName => Text.Loc.Get(NameKey);
        /// <summary>Reads per RANK for a minor, or the whole effect for a notable/keystone.</summary>
        public string Description => Text.Loc.Get(DescKey);
        /// <summary>Node in the same tier and branch this one locks out, if any.</summary>
        public string Excludes { get; }
        /// <summary>Applied once per rank taken.</summary>
        public StatModifier[] PerRank { get; }
        /// <summary>Crossroads only: the second branch that must also be invested in.</summary>
        public SkillBranch? RequiresAlso { get; }

        public SkillNode(string id, SkillBranch branch, int tier, SkillKind kind, int maxRanks,
            Text.LocKey nameKey, Text.LocKey descKey, string excludes,
            SkillBranch? requiresAlso, params StatModifier[] perRank)
        {
            Id = id;
            Branch = branch;
            Tier = tier;
            Kind = kind;
            MaxRanks = Math.Max(1, maxRanks);
            NameKey = nameKey;
            DescKey = descKey;
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
            Text.LocKey name, Text.LocKey desc, params StatModifier[] perRank) =>
            new SkillNode(id, b, tier, SkillKind.Minor, ranks, name, desc, null, null, perRank);

        private static SkillNode Notable(string id, SkillBranch b, int tier,
            Text.LocKey name, Text.LocKey desc, string excludes, params StatModifier[] perRank) =>
            new SkillNode(id, b, tier, SkillKind.Notable, 1, name, desc, excludes, null, perRank);

        private static SkillNode Keystone(string id, SkillBranch b,
            Text.LocKey name, Text.LocKey desc, string excludes, params StatModifier[] perRank) =>
            new SkillNode(id, b, MaxTier, SkillKind.Keystone, 1, name, desc, excludes, null, perRank);

        private static SkillNode Hybrid(string id, SkillBranch primary, SkillBranch also, int tier,
            Text.LocKey name, Text.LocKey desc, params StatModifier[] perRank) =>
            new SkillNode(id, SkillBranch.Crossroads, tier, SkillKind.Notable, 1, name, desc,
                null, also, perRank);

        // Every fraction-valued stat is granted FLAT, and that is load-bearing. StatSheet
        // resolves final = (base + flat) * (1 + percent), and these stats have a base of 0 —
        // a Percent modifier would multiply nothing and the node would be silently inert.
        // Only Damage and Health, which have real bases, take Percent modifiers.
        private static readonly SkillNode[] NodeTable =
        {
            // ================= WARLORD — end the fight ==============================
            Minor("wl_edge", SkillBranch.Warlord, 1, 5, Text.LocKey.TalentWlEdgeName, Text.LocKey.TalentWlEdgeDesc,
                Flat(StatIds.Damage, 3f)),
            Minor("wl_focus", SkillBranch.Warlord, 1, 5, Text.LocKey.TalentWlFocusName, Text.LocKey.TalentWlFocusDesc,
                Flat(StatIds.Cooldown, 0.03f)),
            Minor("wl_spite", SkillBranch.Warlord, 1, 5, Text.LocKey.TalentWlSpiteName, Text.LocKey.TalentWlSpiteDesc,
                Flat(StatIds.SpellPower, 0.06f)),

            Minor("wl_cleave", SkillBranch.Warlord, 2, 5, Text.LocKey.TalentWlCleaveName, Text.LocKey.TalentWlCleaveDesc,
                Pct(StatIds.Damage, 0.04f)),
            Minor("wl_temper", SkillBranch.Warlord, 2, 5, Text.LocKey.TalentWlTemperName, Text.LocKey.TalentWlTemperDesc,
                Flat(StatIds.SpellPower, 0.08f)),
            Notable("wl_echo", SkillBranch.Warlord, 2, Text.LocKey.TalentWlEchoName,
                Text.LocKey.TalentWlEchoDesc, "wl_lance|wl_quiver",
                Flat(StatIds.SpellEcho, 0.12f)),
            Notable("wl_lance", SkillBranch.Warlord, 2, Text.LocKey.TalentWlLanceName,
                Text.LocKey.TalentWlLanceDesc, "wl_echo|wl_quiver",
                Flat(StatIds.SpellPower, 0.22f), Flat(StatIds.Damage, 6f)),
            // THE SPELL IS A MAGAZINE NOW, and this is the line that buys it. A second cast
            // matters most on the road, where the spell destroys red gates as well as packs
            // and a single chunk can put three ambushes in front of you at once.
            Notable("wl_quiver", SkillBranch.Warlord, 2, Text.LocKey.TalentWlQuiverName,
                Text.LocKey.TalentWlQuiverDesc, "wl_echo|wl_lance",
                Flat(StatIds.SpellCharges, 1f)),

            Minor("wl_grind", SkillBranch.Warlord, 3, 5, Text.LocKey.TalentWlGrindName, Text.LocKey.TalentWlGrindDesc,
                Flat(StatIds.Damage, 5f)),
            Minor("wl_haste", SkillBranch.Warlord, 3, 5, Text.LocKey.TalentWlHasteName, Text.LocKey.TalentWlHasteDesc,
                Flat(StatIds.Cooldown, 0.04f)),
            Minor("wl_wrath", SkillBranch.Warlord, 3, 4, Text.LocKey.TalentWlWrathName, Text.LocKey.TalentWlWrathDesc,
                Pct(StatIds.Damage, 0.05f)),
            Notable("wl_execute", SkillBranch.Warlord, 3, Text.LocKey.TalentWlExecuteName,
                Text.LocKey.TalentWlExecuteDesc, null,
                Flat(StatIds.Execute, 0.08f)),

            Minor("wl_ruin", SkillBranch.Warlord, 4, 5, Text.LocKey.TalentWlRuinName, Text.LocKey.TalentWlRuinDesc,
                Flat(StatIds.Damage, 7f)),
            Minor("wl_surge", SkillBranch.Warlord, 4, 5, Text.LocKey.TalentWlSurgeName, Text.LocKey.TalentWlSurgeDesc,
                Flat(StatIds.SpellPower, 0.10f)),
            Notable("wl_reap", SkillBranch.Warlord, 4, Text.LocKey.TalentWlReapName,
                Text.LocKey.TalentWlReapDesc, "wl_swift",
                Flat(StatIds.Execute, 0.07f)),
            Notable("wl_swift", SkillBranch.Warlord, 4, Text.LocKey.TalentWlSwiftName,
                Text.LocKey.TalentWlSwiftDesc, "wl_reap|wl_arsenal",
                Flat(StatIds.Cooldown, 0.09f), Flat(StatIds.SpellEcho, 0.10f)),
            Notable("wl_arsenal", SkillBranch.Warlord, 4, Text.LocKey.TalentWlArsenalName,
                Text.LocKey.TalentWlArsenalDesc, "wl_reap|wl_swift",
                Flat(StatIds.SpellCharges, 1f)),

            Minor("wl_malice", SkillBranch.Warlord, 5, 5, Text.LocKey.TalentWlMaliceName, Text.LocKey.TalentWlMaliceDesc,
                Pct(StatIds.Damage, 0.06f)),
            Minor("wl_venom", SkillBranch.Warlord, 5, 5, Text.LocKey.TalentWlVenomName, Text.LocKey.TalentWlVenomDesc,
                Flat(StatIds.SpellPower, 0.12f)),
            Minor("wl_urgency", SkillBranch.Warlord, 5, 4, Text.LocKey.TalentWlUrgencyName, Text.LocKey.TalentWlUrgencyDesc,
                Flat(StatIds.Cooldown, 0.05f)),
            Notable("wl_carnage", SkillBranch.Warlord, 5, Text.LocKey.TalentWlCarnageName,
                Text.LocKey.TalentWlCarnageDesc, null,
                Flat(StatIds.Damage, 14f), Flat(StatIds.SpellPower, 0.20f)),

            Keystone("wl_annihilation", SkillBranch.Warlord, Text.LocKey.TalentWlAnnihilationName,
                Text.LocKey.TalentWlAnnihilationDesc, "wl_headsman|wl_tempest",
                Pct(StatIds.Damage, 0.40f), Flat(StatIds.SpellPower, 0.60f)),
            Keystone("wl_headsman", SkillBranch.Warlord, Text.LocKey.TalentWlHeadsmanName,
                Text.LocKey.TalentWlHeadsmanDesc, "wl_annihilation|wl_tempest",
                Flat(StatIds.Execute, 0.20f)),
            Keystone("wl_tempest", SkillBranch.Warlord, Text.LocKey.TalentWlTempestName,
                Text.LocKey.TalentWlTempestDesc, "wl_annihilation|wl_headsman",
                Flat(StatIds.SpellEcho, 0.45f), Flat(StatIds.Cooldown, 0.25f),
                Flat(StatIds.SpellCharges, 2f)),

            // ================= WARDEN — survive ====================================
            Minor("wd_hide", SkillBranch.Warden, 1, 5, Text.LocKey.TalentWdHideName, Text.LocKey.TalentWdHideDesc,
                Flat(StatIds.Health, 18f)),
            Minor("wd_guard", SkillBranch.Warden, 1, 5, Text.LocKey.TalentWdGuardName, Text.LocKey.TalentWdGuardDesc,
                Flat(StatIds.EnemyResist, 0.04f)),
            Minor("wd_brace", SkillBranch.Warden, 1, 5, Text.LocKey.TalentWdBraceName, Text.LocKey.TalentWdBraceDesc,
                Flat(StatIds.ShieldDuration, 0.2f)),

            Minor("wd_plate", SkillBranch.Warden, 2, 5, Text.LocKey.TalentWdPlateName, Text.LocKey.TalentWdPlateDesc,
                Pct(StatIds.Health, 0.04f)),
            Minor("wd_bramble", SkillBranch.Warden, 2, 5, Text.LocKey.TalentWdBrambleName, Text.LocKey.TalentWdBrambleDesc,
                Flat(StatIds.EnemyResist, 0.05f)),
            Notable("wd_shatter", SkillBranch.Warden, 2, Text.LocKey.TalentWdShatterName,
                Text.LocKey.TalentWdShatterDesc, "wd_bulwark",
                Flat(StatIds.PackShatter, 0.14f)),
            // THE SHIELD IS A MAGAZINE NOW, so how many raises you hold is a separate thing
            // to buy from how long each one lasts and how fast they return. One extra raise
            // at tier 2, a second at tier 4, a third on the keystone: three points across a
            // whole branch, because a charge is worth far more than a tenth of a second of
            // uptime and pricing them the same would make duration dead.
            Notable("wd_doubleguard", SkillBranch.Warden, 2, Text.LocKey.TalentWdDoubleguardName,
                Text.LocKey.TalentWdDoubleguardDesc, "wd_bulwark|wd_shatter",
                Flat(StatIds.ShieldCharges, 1f)),
            Notable("wd_bulwark", SkillBranch.Warden, 2, Text.LocKey.TalentWdBulwarkName,
                Text.LocKey.TalentWdBulwarkDesc, "wd_shatter",
                Flat(StatIds.ShieldDuration, 1.0f), Flat(StatIds.Health, 30f)),

            Minor("wd_stone", SkillBranch.Warden, 3, 5, Text.LocKey.TalentWdStoneName, Text.LocKey.TalentWdStoneDesc,
                Flat(StatIds.Health, 24f)),
            Minor("wd_ward", SkillBranch.Warden, 3, 5, Text.LocKey.TalentWdWardName, Text.LocKey.TalentWdWardDesc,
                Flat(StatIds.ShieldDuration, 0.25f)),
            Minor("wd_scar", SkillBranch.Warden, 3, 4, Text.LocKey.TalentWdScarName, Text.LocKey.TalentWdScarDesc,
                Flat(StatIds.EnemyResist, 0.05f)),
            Notable("wd_reflect", SkillBranch.Warden, 3, Text.LocKey.TalentWdReflectName,
                Text.LocKey.TalentWdReflectDesc, null,
                Flat(StatIds.ShieldReflect, 0.25f)),
            Minor("wd_ready", SkillBranch.Warden, 3, 5, Text.LocKey.TalentWdReadyName, Text.LocKey.TalentWdReadyDesc,
                Flat(StatIds.Cooldown, 0.04f)),

            Minor("wd_bastion", SkillBranch.Warden, 4, 5, Text.LocKey.TalentWdBastionName, Text.LocKey.TalentWdBastionDesc,
                Pct(StatIds.Health, 0.06f)),
            Minor("wd_thorns", SkillBranch.Warden, 4, 5, Text.LocKey.TalentWdThornsName, Text.LocKey.TalentWdThornsDesc,
                Flat(StatIds.ShieldReflect, 0.12f)),
            Notable("wd_wind", SkillBranch.Warden, 4, Text.LocKey.TalentWdWindName,
                Text.LocKey.TalentWdWindDesc, "wd_aegis",
                Flat(StatIds.SecondWind, 0.30f)),
            Notable("wd_aegis", SkillBranch.Warden, 4, Text.LocKey.TalentWdAegisName,
                Text.LocKey.TalentWdAegisDesc, "wd_wind|wd_triguard",
                Flat(StatIds.ShieldDuration, 1.4f), Flat(StatIds.PackShatter, 0.18f)),
            Notable("wd_triguard", SkillBranch.Warden, 4, Text.LocKey.TalentWdTriguardName,
                Text.LocKey.TalentWdTriguardDesc, "wd_wind|wd_aegis",
                Flat(StatIds.ShieldCharges, 1f)),

            Minor("wd_iron", SkillBranch.Warden, 5, 5, Text.LocKey.TalentWdIronName, Text.LocKey.TalentWdIronDesc,
                Flat(StatIds.Health, 34f)),
            Minor("wd_deny", SkillBranch.Warden, 5, 5, Text.LocKey.TalentWdDenyName, Text.LocKey.TalentWdDenyDesc,
                Flat(StatIds.EnemyResist, 0.06f)),
            Minor("wd_shell", SkillBranch.Warden, 5, 4, Text.LocKey.TalentWdShellName, Text.LocKey.TalentWdShellDesc,
                Flat(StatIds.PackShatter, 0.10f)),
            Notable("wd_unbroken", SkillBranch.Warden, 5, Text.LocKey.TalentWdUnbrokenName,
                Text.LocKey.TalentWdUnbrokenDesc, null,
                Flat(StatIds.Health, 70f), Flat(StatIds.EnemyResist, 0.12f)),

            Keystone("wd_undying", SkillBranch.Warden, Text.LocKey.TalentWdUndyingName,
                Text.LocKey.TalentWdUndyingDesc, "wd_immovable|wd_mirror",
                Flat(StatIds.Health, 120f), Flat(StatIds.SecondWind, 0.55f)),
            Keystone("wd_immovable", SkillBranch.Warden, Text.LocKey.TalentWdImmovableName,
                Text.LocKey.TalentWdImmovableDesc, "wd_undying|wd_mirror",
                Flat(StatIds.EnemyResist, 0.40f), Flat(StatIds.PackShatter, 0.30f)),
            // The charge payoff is folded into an EXISTING keystone rather than added as a
            // fourth. Three mutually exclusive keystones per branch is a design rule with a
            // test on it, and quietly making it four to fit a new stat in would be changing
            // the shape of every build in the game to avoid an edit.
            Keystone("wd_mirror", SkillBranch.Warden, Text.LocKey.TalentWdMirrorName,
                Text.LocKey.TalentWdMirrorDesc, "wd_undying|wd_immovable",
                Flat(StatIds.ShieldReflect, 0.90f), Flat(StatIds.ShieldCharges, 2f)),

            // ================= ZEALOT — grow the army ==============================
            Minor("zl_avarice", SkillBranch.Zealot, 1, 5, Text.LocKey.TalentZlAvariceName, Text.LocKey.TalentZlAvariceDesc,
                Flat(StatIds.GateYield, 0.03f)),
            Minor("zl_stride", SkillBranch.Zealot, 1, 5, Text.LocKey.TalentZlStrideName, Text.LocKey.TalentZlStrideDesc,
                Flat(StatIds.RunSpeed, 0.02f)),
            Minor("zl_luck", SkillBranch.Zealot, 1, 5, Text.LocKey.TalentZlLuckName, Text.LocKey.TalentZlLuckDesc,
                Flat(StatIds.Fortune, 0.06f)),

            Minor("zl_greed", SkillBranch.Zealot, 2, 5, Text.LocKey.TalentZlGreedName, Text.LocKey.TalentZlGreedDesc,
                Flat(StatIds.GateYield, 0.04f)),
            Minor("zl_lodestone", SkillBranch.Zealot, 2, 5, Text.LocKey.TalentZlLodestoneName,
                Text.LocKey.TalentZlLodestoneDesc, Flat(StatIds.Magnetism, 0.12f)),
            Notable("zl_crit", SkillBranch.Zealot, 2, Text.LocKey.TalentZlCritName,
                Text.LocKey.TalentZlCritDesc, "zl_zeal",
                Flat(StatIds.GateCrit, 0.10f)),
            Notable("zl_zeal", SkillBranch.Zealot, 2, Text.LocKey.TalentZlZealName,
                Text.LocKey.TalentZlZealDesc, "zl_crit",
                Flat(StatIds.RunSpeed, 0.12f), Flat(StatIds.GateYield, 0.08f)),

            Minor("zl_tithe", SkillBranch.Zealot, 3, 5, Text.LocKey.TalentZlTitheName, Text.LocKey.TalentZlTitheDesc,
                Flat(StatIds.GateYield, 0.05f)),
            Minor("zl_omen", SkillBranch.Zealot, 3, 5, Text.LocKey.TalentZlOmenName, Text.LocKey.TalentZlOmenDesc,
                Flat(StatIds.Fortune, 0.08f)),
            Minor("zl_fervour", SkillBranch.Zealot, 3, 4, Text.LocKey.TalentZlFervourName, Text.LocKey.TalentZlFervourDesc,
                Flat(StatIds.GateCrit, 0.04f)),
            Notable("zl_chain", SkillBranch.Zealot, 3, Text.LocKey.TalentZlChainName,
                Text.LocKey.TalentZlChainDesc, null,
                Flat(StatIds.ChainMultiply, 0.15f)),

            Minor("zl_hunger", SkillBranch.Zealot, 4, 5, Text.LocKey.TalentZlHungerName, Text.LocKey.TalentZlHungerDesc,
                Flat(StatIds.GateYield, 0.06f)),
            Minor("zl_pull", SkillBranch.Zealot, 4, 5, Text.LocKey.TalentZlPullName, Text.LocKey.TalentZlPullDesc,
                Flat(StatIds.Magnetism, 0.15f)),
            Notable("zl_hoard", SkillBranch.Zealot, 4, Text.LocKey.TalentZlHoardName,
                Text.LocKey.TalentZlHoardDesc, "zl_swarm",
                Flat(StatIds.OverflowBank, 0.25f)),
            Notable("zl_swarm", SkillBranch.Zealot, 4, Text.LocKey.TalentZlSwarmName,
                Text.LocKey.TalentZlSwarmDesc, "zl_hoard",
                Flat(StatIds.GateYield, 0.18f), Flat(StatIds.GateCrit, 0.12f)),

            Minor("zl_rapture", SkillBranch.Zealot, 5, 5, Text.LocKey.TalentZlRaptureName, Text.LocKey.TalentZlRaptureDesc,
                Flat(StatIds.GateYield, 0.07f)),
            Minor("zl_frenzy", SkillBranch.Zealot, 5, 5, Text.LocKey.TalentZlFrenzyName, Text.LocKey.TalentZlFrenzyDesc,
                Flat(StatIds.RunSpeed, 0.03f)),
            Minor("zl_cascade", SkillBranch.Zealot, 5, 4, Text.LocKey.TalentZlCascadeName, Text.LocKey.TalentZlCascadeDesc,
                Flat(StatIds.ChainMultiply, 0.08f)),
            Notable("zl_legion", SkillBranch.Zealot, 5, Text.LocKey.TalentZlLegionName,
                Text.LocKey.TalentZlLegionDesc, null,
                Flat(StatIds.GateYield, 0.25f), Flat(StatIds.Fortune, 0.20f)),

            Keystone("zl_multiplication", SkillBranch.Zealot, Text.LocKey.TalentZlMultiplicationName,
                Text.LocKey.TalentZlMultiplicationDesc, "zl_avalanche|zl_covetous",
                Flat(StatIds.GateYield, 0.55f), Flat(StatIds.GateCrit, 0.25f)),
            Keystone("zl_avalanche", SkillBranch.Zealot, Text.LocKey.TalentZlAvalancheName,
                Text.LocKey.TalentZlAvalancheDesc, "zl_multiplication|zl_covetous",
                Flat(StatIds.ChainMultiply, 0.60f), Flat(StatIds.Magnetism, 0.5f)),
            Keystone("zl_covetous", SkillBranch.Zealot, Text.LocKey.TalentZlCovetousName,
                Text.LocKey.TalentZlCovetousDesc, "zl_multiplication|zl_avalanche",
                Flat(StatIds.OverflowBank, 0.90f), Flat(StatIds.Fortune, 0.60f)),

            // ================= CROSSROADS — hybrids ================================
            // Gated on investment in TWO branches, so they reward committing rather than
            // dabbling. Tier here means "points needed in EACH of the two branches".
            Hybrid("cr_warpriest", SkillBranch.Warlord, SkillBranch.Zealot, 2, Text.LocKey.TalentCrWarpriestName,
                Text.LocKey.TalentCrWarpriestDesc,
                Flat(StatIds.Damage, 10f), Flat(StatIds.GateYield, 0.08f)),
            Hybrid("cr_crusader", SkillBranch.Warlord, SkillBranch.Warden, 2, Text.LocKey.TalentCrCrusaderName,
                Text.LocKey.TalentCrCrusaderDesc,
                Flat(StatIds.Health, 40f), Flat(StatIds.SpellPower, 0.12f)),
            Hybrid("cr_pilgrim", SkillBranch.Warden, SkillBranch.Zealot, 2, Text.LocKey.TalentCrPilgrimName,
                Text.LocKey.TalentCrPilgrimDesc,
                Flat(StatIds.EnemyResist, 0.10f), Flat(StatIds.RunSpeed, 0.06f)),
            Hybrid("cr_martyr", SkillBranch.Warlord, SkillBranch.Warden, 4, Text.LocKey.TalentCrMartyrName,
                Text.LocKey.TalentCrMartyrDesc,
                Flat(StatIds.ShieldReflect, 0.30f), Flat(StatIds.Damage, 12f)),
            Hybrid("cr_prophet", SkillBranch.Warlord, SkillBranch.Zealot, 4, Text.LocKey.TalentCrProphetName,
                Text.LocKey.TalentCrProphetDesc,
                Flat(StatIds.OverflowBank, 0.30f), Flat(StatIds.SpellPower, 0.18f)),
            Hybrid("cr_shepherd", SkillBranch.Warden, SkillBranch.Zealot, 4, Text.LocKey.TalentCrShepherdName,
                Text.LocKey.TalentCrShepherdDesc,
                Flat(StatIds.PackShatter, 0.18f), Flat(StatIds.GateYield, 0.14f)),
            Hybrid("cr_apostle", SkillBranch.Warlord, SkillBranch.Zealot, 5, Text.LocKey.TalentCrApostleName,
                Text.LocKey.TalentCrApostleDesc,
                Flat(StatIds.Execute, 0.06f), Flat(StatIds.ChainMultiply, 0.20f)),
            Hybrid("cr_paladin", SkillBranch.Warlord, SkillBranch.Warden, 5, Text.LocKey.TalentCrPaladinName,
                Text.LocKey.TalentCrPaladinDesc,
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
            if (node == null) return Text.Loc.Get(Text.LocKey.TalentUnknown);
            if (RankOf(ranks, nodeId) >= node.MaxRanks)
                return Text.Loc.Get(Text.LocKey.TalentFullyRanked);
            if (unspentPoints < PointCost) return Text.Loc.Get(Text.LocKey.TalentNoPoints);

            // Exclusions are a bar-separated list so a keystone can rule out its two rivals.
            if (node.Excludes != null)
            {
                foreach (string other in node.Excludes.Split('|'))
                {
                    if (RankOf(ranks, other) <= 0) continue;
                    SkillNode blocker = Find(other);
                    return Text.Loc.Format(Text.LocKey.TalentRuledOut,
                        blocker != null ? blocker.DisplayName : other);
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
                    return Text.Loc.Format(Text.LocKey.TalentNeedsBoth, need, Name(primary), Name(also));
                return null;
            }

            int required = (node.Tier - 1) * TierUnlockCost;
            if (PointsBelowTier(ranks, node.Branch, node.Tier) < required)
                return Text.Loc.Format(Text.LocKey.TalentNeedsBranch, required, Name(node.Branch));

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
            if (node == null) return Text.Loc.Get(Text.LocKey.TalentUnknown);
            if (RankOf(ranks, nodeId) <= 0) return Text.Loc.Get(Text.LocKey.TalentNotLearned);
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

            return blocker == null
                ? null
                : Text.Loc.Format(Text.LocKey.TalentUnlearnFirst, blocker.DisplayName);
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
            SkillBranch.Warlord => Text.Loc.Get(Text.LocKey.BranchWarlord),
            SkillBranch.Warden => Text.Loc.Get(Text.LocKey.BranchWarden),
            SkillBranch.Zealot => Text.Loc.Get(Text.LocKey.BranchZealot),
            _ => Text.Loc.Get(Text.LocKey.BranchCrossroads)
        };
    }
}
