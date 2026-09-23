using BattleRunner.Core.Text;
using System.Collections.Generic;
using BattleRunner.Core.Boss;
using BattleRunner.Core.Loot;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Run;
using BattleRunner.Core.Stats;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    /// <summary>
    /// Builds the complete greybox content set as in-memory ScriptableObjects.
    /// Two consumers: the runtime fallback when Resources/GameConfig is missing
    /// (the game must play on first open — plan decision 4), and the editor
    /// ContentBootstrap, which saves the same objects as editable .asset files.
    /// </summary>
    public static class ContentFactory
    {
        public static GameConfig BuildConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = "GameConfig";

            config.Balance = ScriptableObject.CreateInstance<BalanceSettings>();
            config.Balance.name = "Balance";

            config.Input = ScriptableObject.CreateInstance<InputSettingsSO>();
            config.Input.name = "InputSettings";

            config.Spells = ScriptableObject.CreateInstance<SpellDefinition>();
            config.Spells.name = "Spells";

            config.Stats = BuildStats();
            config.AllGear = BuildGear();

            LootTableDefinition lootTable = BuildLootTable(config.AllGear);
            config.Bosses = BuildBosses();
            config.Levels = BuildLevels(config.Bosses, lootTable);

            return config;
        }

        public static StatDefinition[] BuildStats()
        {
            return new[]
            {
                Stat(StatIds.Damage, LocKey.StatDamage, LocKey.StatMightDesc),
                Stat(StatIds.Health, LocKey.StatHealth, LocKey.StatVigorDesc),
                Stat(StatIds.Cooldown, LocKey.StatCooldown, LocKey.StatFocusDesc)
            };
        }

        private static StatDefinition Stat(string id, LocKey displayName, LocKey description)
        {
            var stat = ScriptableObject.CreateInstance<StatDefinition>();
            stat.name = $"Stat_{displayName}";
            stat.Id = id;
            stat.NameKey = displayName;
            stat.DescriptionKey = description;
            return stat;
        }

        public static GearItemDefinition[] BuildGear()
        {
            var gear = new List<GearItemDefinition>
            {
                // Weapons
                Item("wpn_rusted_cleaver", LocKey.GearWpnRustedCleaverName, GearSlot.Weapon, Rarity.Common,
                    LocKey.GearWpnRustedCleaverFlavor, Flat(StatIds.Damage, 2f)),
                Item("wpn_gravedigger_axe", LocKey.GearWpnGravediggerAxeName, GearSlot.Weapon, Rarity.Common,
                    LocKey.GearWpnGravediggerAxeFlavor, Flat(StatIds.Damage, 3f)),
                Item("wpn_cinder_blade", LocKey.GearWpnCinderBladeName, GearSlot.Weapon, Rarity.Rare,
                    LocKey.GearWpnCinderBladeFlavor, Flat(StatIds.Damage, 6f), Percent(StatIds.Damage, 0.05f)),
                Item("wpn_soulreaver", LocKey.GearWpnSoulreaverName, GearSlot.Weapon, Rarity.Epic,
                    LocKey.GearWpnSoulreaverFlavor, Flat(StatIds.Damage, 10f), Percent(StatIds.Damage, 0.10f)),
                Item("wpn_doombringer", LocKey.GearWpnDoombringerName, GearSlot.Weapon, Rarity.Legendary,
                    LocKey.GearWpnDoombringerFlavor, Flat(StatIds.Damage, 16f), Percent(StatIds.Damage, 0.25f)),

                // Armor
                Item("arm_tattered_hauberk", LocKey.GearArmTatteredHauberkName, GearSlot.Armor, Rarity.Common,
                    LocKey.GearArmTatteredHauberkFlavor, Flat(StatIds.Health, 10f)),
                Item("arm_bone_vest", LocKey.GearArmBoneVestName, GearSlot.Armor, Rarity.Common,
                    LocKey.GearArmBoneVestFlavor, Flat(StatIds.Health, 15f)),
                Item("arm_ironbark_plate", LocKey.GearArmIronbarkPlateName, GearSlot.Armor, Rarity.Rare,
                    LocKey.GearArmIronbarkPlateFlavor, Flat(StatIds.Health, 30f)),
                Item("arm_wraithmail", LocKey.GearArmWraithmailName, GearSlot.Armor, Rarity.Epic,
                    LocKey.GearArmWraithmailFlavor, Flat(StatIds.Health, 50f), Percent(StatIds.Health, 0.10f)),
                Item("arm_aegis_fallen_king", LocKey.GearArmAegisFallenKingName, GearSlot.Armor, Rarity.Legendary,
                    LocKey.GearArmAegisFallenKingFlavor, Flat(StatIds.Health, 80f), Percent(StatIds.Health, 0.25f)),

                // Relics
                Item("rel_cracked_skull", LocKey.GearRelCrackedSkullName, GearSlot.Relic, Rarity.Common,
                    LocKey.GearRelCrackedSkullFlavor, Flat(StatIds.Cooldown, 0.02f)),
                Item("rel_ember_talisman", LocKey.GearRelEmberTalismanName, GearSlot.Relic, Rarity.Common,
                    LocKey.GearRelEmberTalismanFlavor, Flat(StatIds.Damage, 2f), Flat(StatIds.Cooldown, 0.01f)),
                Item("rel_hollow_idol", LocKey.GearRelHollowIdolName, GearSlot.Relic, Rarity.Rare,
                    LocKey.GearRelHollowIdolFlavor, Flat(StatIds.Cooldown, 0.05f)),
                Item("rel_eye_of_abyss", LocKey.GearRelEyeOfAbyssName, GearSlot.Relic, Rarity.Epic,
                    LocKey.GearRelEyeOfAbyssFlavor, Flat(StatIds.Cooldown, 0.08f), Percent(StatIds.Damage, 0.05f)),
                Item("rel_crown_of_embers", LocKey.GearRelCrownOfEmbersName, GearSlot.Relic, Rarity.Legendary,
                    LocKey.GearRelCrownOfEmbersFlavor, Flat(StatIds.Cooldown, 0.12f), Percent(StatIds.Damage, 0.10f))
            };
            return gear.ToArray();
        }

        private static GearItemDefinition Item(string id, LocKey displayName, GearSlot slot, Rarity rarity,
            LocKey flavor, params StatModifier[] modifiers)
        {
            var item = ScriptableObject.CreateInstance<GearItemDefinition>();
            item.name = $"Gear_{id}";
            item.Id = id;
            item.NameKey = displayName;
            item.Slot = slot;
            item.Rarity = rarity;
            item.Modifiers = modifiers;
            item.FlavorKey = flavor;
            return item;
        }

        private static StatModifier Flat(string statId, float value) =>
            new StatModifier(statId, ModifierKind.Flat, value);

        private static StatModifier Percent(string statId, float value) =>
            new StatModifier(statId, ModifierKind.Percent, value);

        public static LootTableDefinition BuildLootTable(GearItemDefinition[] allGear)
        {
            var table = ScriptableObject.CreateInstance<LootTableDefinition>();
            table.name = "LootTable_Main";
            table.PityLegendaryFloor = 12;

            var entries = new List<LootTableDefinition.Entry>();
            foreach (GearItemDefinition item in allGear)
            {
                float weight = item.Rarity switch
                {
                    Rarity.Common => 30f,
                    Rarity.Rare => 12f,
                    Rarity.Epic => 4f,
                    _ => 1f
                };
                entries.Add(new LootTableDefinition.Entry { Item = item, Weight = weight });
            }
            table.Entries = entries.ToArray();
            return table;
        }

        /// <summary>
        /// The roster. SIX bosses, one per archetype, because an archetype is a silhouette
        /// AND a power and the player only learns "the hunched one drains you" if the
        /// hunched one always drains.
        ///
        /// The game shipped with two, identical but for tint and stats, drawn from the same
        /// mesh and running the same telegraph-then-hit pattern — and a level lookup that
        /// clamped, so round six onward was the same fight forever. These differ in what
        /// they do, what they look like, how fast they swing and what colour they heat to.
        ///
        /// Stats climb across the list on purpose. GameConfig.BossFor rotates by round, so
        /// the order here IS the order a new player meets them, and the first one has to be
        /// the one that teaches the shield.
        /// </summary>
        public static BossDefinition[] BuildBosses()
        {
            return new[]
            {
                // Bone, and bright enough to survive the pipeline. These are sRGB values in
                // a LINEAR project, so they are gamma-expanded on upload: 0.55 arrives as
                // 0.263 linear, and BossView used to halve it first, landing the boss on a
                // 5% reflectance — darker than the road it stands on. See BossView.Show.
                Boss("Boss_BoneColossus", LocKey.BossBoneColossus, BossArchetype.Slam,
                    690f, 0.036f, 4.0f, 1.20f, 0.30f,
                    new Color(0.78f, 0.74f, 0.66f), new Color(1.40f, 0.50f, 0.20f)),

                Boss("Boss_EmberLich", LocKey.BossEmberLich, BossArchetype.Volley,
                    798f, 0.040f, 4.4f, 1.10f, 0.30f,
                    new Color(0.95f, 0.52f, 0.22f), new Color(1.60f, 0.62f, 0.18f)),

                Boss("Boss_GraveWarden", LocKey.BossGraveWarden, BossArchetype.Warded,
                    870f, 0.044f, 3.8f, 1.30f, 0.34f,
                    new Color(0.52f, 0.62f, 0.72f), new Color(0.45f, 1.10f, 1.55f)),

                Boss("Boss_HollowLeech", LocKey.BossHollowLeech, BossArchetype.Drain,
                    792f, 0.042f, 5.0f, 1.10f, 0.26f,
                    new Color(0.44f, 0.66f, 0.50f), new Color(0.55f, 1.50f, 0.62f)),

                Boss("Boss_PaleShepherd", LocKey.BossPaleShepherd, BossArchetype.Summoner,
                    834f, 0.047f, 4.6f, 1.25f, 0.22f,
                    new Color(0.72f, 0.60f, 0.86f), new Color(1.15f, 0.55f, 1.60f)),

                Boss("Boss_GoreHound", LocKey.BossGoreHound, BossArchetype.Enrage,
                    906f, 0.051f, 3.6f, 0.95f, 0.28f,
                    new Color(0.80f, 0.34f, 0.30f), new Color(1.70f, 0.30f, 0.22f))
            };
        }

        private static BossDefinition Boss(string assetName, LocKey displayName,
            BossArchetype archetype, float baseHp, float growth, float interval,
            float telegraph, float hitFraction, Color tint, Color telegraphColor)
        {
            var boss = ScriptableObject.CreateInstance<BossDefinition>();
            boss.name = assetName;
            boss.NameKey = displayName;
            boss.Archetype = archetype;
            boss.BaseHp = baseHp;
            boss.PerLevelGrowth = growth;
            boss.AttackIntervalSeconds = interval;
            boss.TelegraphSeconds = telegraph;
            boss.HitFraction = hitFraction;
            boss.TintColor = tint;
            boss.TelegraphColor = telegraphColor;
            return boss;
        }

        public static LevelDefinition[] BuildLevels(BossDefinition[] bosses, LootTableDefinition lootTable)
        {
            LocKey[] names =
            {
                LocKey.LevelAshenRoad, LocKey.LevelGallowsMire, LocKey.LevelSunkenCrypt,
                LocKey.LevelEmberFields, LocKey.LevelThroneOfDust, LocKey.LevelWeepingGate
            };
            var levels = new LevelDefinition[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var level = ScriptableObject.CreateInstance<LevelDefinition>();
                level.name = $"Level_{i + 1:00}";
                level.NameKey = names[i];
                level.Chunks = BuildChunksForLevel(i);
                level.Boss = bosses[i % bosses.Length];
                level.LootTable = lootTable;
                // Advisory only: the runtime computes par from the round it is actually
                // building AND from the army that walks into it (GameContext.CurrentPar),
                // because a level asset can know neither. Quoted from the seed muster so the
                // designer-facing number still means something concrete.
                level.ParForceAtFinish = ChunkLayouts.EstimateParForce(
                    ChunkLayouts.BuildRound(RoundPlan.For(i)), StandingArmy.Seed);
                levels[i] = level;
            }
            return levels;
        }

        /// <summary>
        /// Authored chunk patterns per level: adds early, one multiplier mid-chunk-set,
        /// subtract gates and enemy packs as pressure. Multiplier placement follows the
        /// par-force rule (doc 01, R4): never two x-gates in the same chunk.
        /// </summary>
        /// <summary>
        /// A round's chunks, as editable assets.
        ///
        /// This DELEGATES to Core rather than authoring its own layout. It used to be a
        /// formula with exactly three outcomes — an add at 12 m, an add at 28 m, and on every
        /// third chunk a x2 opposite a -N at 40 m — cycled forever, which is the literal
        /// reason the report was "every round the doors are the same". The runtime no longer
        /// reads these at all (TrackController builds from ChunkLayouts per round), but the
        /// editor still materialises them, and content a designer opens must match what the
        /// game actually plays or it is worse than no content at all.
        /// </summary>
        public static ChunkDefinition[] BuildChunksForLevel(int levelIndex)
        {
            RoundPlan plan = RoundPlan.For(levelIndex);
            ChunkLayout[] layouts = ChunkLayouts.BuildRound(plan);
            var chunks = new ChunkDefinition[layouts.Length];

            for (int c = 0; c < layouts.Length; c++)
            {
                ChunkLayout layout = layouts[c];
                var chunk = ScriptableObject.CreateInstance<ChunkDefinition>();
                // The shape is in the asset name on purpose: it is the one thing that makes a
                // generated chunk readable in the Project window.
                chunk.name = $"Chunk_L{levelIndex + 1:00}_{c + 1:00}_{layout.Shape}";
                chunk.LengthMeters = ChunkLayouts.ChunkMeters;

                var gates = new ChunkDefinition.GateSpec[layout.Gates.Count];
                for (int i = 0; i < gates.Length; i++)
                    gates[i] = new ChunkDefinition.GateSpec
                    {
                        Op = layout.Gates[i].Op,
                        Value = layout.Gates[i].Value,
                        Lane = layout.Gates[i].Lane,
                        Position = layout.Gates[i].Position
                    };

                var enemies = new ChunkDefinition.EnemySpec[layout.Packs.Count];
                for (int i = 0; i < enemies.Length; i++)
                    enemies[i] = new ChunkDefinition.EnemySpec
                    {
                        ForceCost = layout.Packs[i].ForceCost,
                        Lane = layout.Packs[i].Lane,
                        Position = layout.Packs[i].Position,
                        // CARRIED, not dropped. These two were silently lost here: a champion
                        // baked into a ChunkDefinition came back as an ordinary squad, and a
                        // barricade as a squad in one lane. Nothing reads EnemySpec today, so
                        // it was dormant rather than broken — and dormant is exactly how the
                        // next person to revive this path would have inherited it.
                        Elite = layout.Packs[i].Elite,
                        BlocksAllLanes = layout.Packs[i].BlocksAllLanes
                    };

                chunk.Gates = gates;
                chunk.Enemies = enemies;
                chunks[c] = chunk;
            }

            return chunks;
        }
    }
}
