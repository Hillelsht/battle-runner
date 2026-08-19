using System.Collections.Generic;
using BattleRunner.Core.Loot;
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
            BossDefinition[] bosses = BuildBosses();
            config.Levels = BuildLevels(bosses, lootTable);

            return config;
        }

        public static StatDefinition[] BuildStats()
        {
            return new[]
            {
                Stat(StatIds.Damage, "Might", "Raw damage dealt by your hero and horde."),
                Stat(StatIds.Health, "Vigor", "Cushions your force against boss blows."),
                Stat(StatIds.Cooldown, "Focus", "Shortens spell and shield cooldowns.")
            };
        }

        private static StatDefinition Stat(string id, string displayName, string description)
        {
            var stat = ScriptableObject.CreateInstance<StatDefinition>();
            stat.name = $"Stat_{displayName}";
            stat.Id = id;
            stat.DisplayName = displayName;
            stat.Description = description;
            return stat;
        }

        public static GearItemDefinition[] BuildGear()
        {
            var gear = new List<GearItemDefinition>
            {
                // Weapons
                Item("wpn_rusted_cleaver", "Rusted Cleaver", GearSlot.Weapon, Rarity.Common,
                    "Still remembers the war it lost.", Flat(StatIds.Damage, 2f)),
                Item("wpn_gravedigger_axe", "Gravedigger's Axe", GearSlot.Weapon, Rarity.Common,
                    "Blunt from honest, grim work.", Flat(StatIds.Damage, 3f)),
                Item("wpn_cinder_blade", "Cinder Blade", GearSlot.Weapon, Rarity.Rare,
                    "Warm to the touch. Always.", Flat(StatIds.Damage, 6f), Percent(StatIds.Damage, 0.05f)),
                Item("wpn_soulreaver", "Soulreaver", GearSlot.Weapon, Rarity.Epic,
                    "It drinks. You wield.", Flat(StatIds.Damage, 10f), Percent(StatIds.Damage, 0.10f)),
                Item("wpn_doombringer", "Doombringer", GearSlot.Weapon, Rarity.Legendary,
                    "The last blade its forger ever made.", Flat(StatIds.Damage, 16f), Percent(StatIds.Damage, 0.25f)),

                // Armor
                Item("arm_tattered_hauberk", "Tattered Hauberk", GearSlot.Armor, Rarity.Common,
                    "More gaps than mail.", Flat(StatIds.Health, 10f)),
                Item("arm_bone_vest", "Bone-Studded Vest", GearSlot.Armor, Rarity.Common,
                    "The bones are not decorative.", Flat(StatIds.Health, 15f)),
                Item("arm_ironbark_plate", "Ironbark Plate", GearSlot.Armor, Rarity.Rare,
                    "Grown, not forged.", Flat(StatIds.Health, 30f)),
                Item("arm_wraithmail", "Wraithmail", GearSlot.Armor, Rarity.Epic,
                    "Weightless. Whispering.", Flat(StatIds.Health, 50f), Percent(StatIds.Health, 0.10f)),
                Item("arm_aegis_fallen_king", "Aegis of the Fallen King", GearSlot.Armor, Rarity.Legendary,
                    "He fell. It didn't.", Flat(StatIds.Health, 80f), Percent(StatIds.Health, 0.25f)),

                // Relics
                Item("rel_cracked_skull", "Cracked Skull Charm", GearSlot.Relic, Rarity.Common,
                    "Its previous owner had worse luck.", Flat(StatIds.Cooldown, 0.02f)),
                Item("rel_ember_talisman", "Ember Talisman", GearSlot.Relic, Rarity.Common,
                    "A pocketful of dying fire.", Flat(StatIds.Damage, 2f), Flat(StatIds.Cooldown, 0.01f)),
                Item("rel_hollow_idol", "Hollow Idol", GearSlot.Relic, Rarity.Rare,
                    "Something used to live inside.", Flat(StatIds.Cooldown, 0.05f)),
                Item("rel_eye_of_abyss", "Eye of the Abyss", GearSlot.Relic, Rarity.Epic,
                    "It blinks when you cast.", Flat(StatIds.Cooldown, 0.08f), Percent(StatIds.Damage, 0.05f)),
                Item("rel_crown_of_embers", "Crown of Embers", GearSlot.Relic, Rarity.Legendary,
                    "Rule nothing. Burn everything.", Flat(StatIds.Cooldown, 0.12f), Percent(StatIds.Damage, 0.10f))
            };
            return gear.ToArray();
        }

        private static GearItemDefinition Item(string id, string displayName, GearSlot slot, Rarity rarity,
            string flavor, params StatModifier[] modifiers)
        {
            var item = ScriptableObject.CreateInstance<GearItemDefinition>();
            item.name = $"Gear_{id}";
            item.Id = id;
            item.DisplayName = displayName;
            item.Slot = slot;
            item.Rarity = rarity;
            item.Modifiers = modifiers;
            item.Flavor = flavor;
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

        public static BossDefinition[] BuildBosses()
        {
            var colossus = ScriptableObject.CreateInstance<BossDefinition>();
            colossus.name = "Boss_BoneColossus";
            colossus.DisplayName = "Bone Colossus";
            colossus.BaseHp = 500f;
            colossus.PerLevelGrowth = 0.25f;
            colossus.AttackIntervalSeconds = 4f;
            colossus.TelegraphSeconds = 1.2f;
            colossus.HitFraction = 0.3f;
            colossus.TintColor = new Color(0.55f, 0.5f, 0.45f);

            var lich = ScriptableObject.CreateInstance<BossDefinition>();
            lich.name = "Boss_EmberLich";
            lich.DisplayName = "Ember Lich";
            lich.BaseHp = 800f;
            lich.PerLevelGrowth = 0.28f;
            lich.AttackIntervalSeconds = 3.2f;
            lich.TelegraphSeconds = 1.0f;
            lich.HitFraction = 0.25f;
            lich.TintColor = new Color(0.9f, 0.4f, 0.15f);

            return new[] { colossus, lich };
        }

        public static LevelDefinition[] BuildLevels(BossDefinition[] bosses, LootTableDefinition lootTable)
        {
            string[] names = { "The Ashen Road", "Gallows Mire", "The Sunken Crypt", "Ember Fields", "Throne of Dust" };
            var levels = new LevelDefinition[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var level = ScriptableObject.CreateInstance<LevelDefinition>();
                level.name = $"Level_{i + 1:00}";
                level.DisplayName = names[i];
                level.Chunks = BuildChunksForLevel(i);
                level.Boss = bosses[i % bosses.Length];
                level.LootTable = lootTable;
                level.StartingForce = 5;
                level.ParForceAtFinish = EstimateParForce(level.Chunks, level.StartingForce);
                levels[i] = level;
            }
            return levels;
        }

        /// <summary>
        /// Authored chunk patterns per level: adds early, one multiplier mid-chunk-set,
        /// subtract gates and enemy packs as pressure. Multiplier placement follows the
        /// par-force rule (doc 01, R4): never two x-gates in the same chunk.
        /// </summary>
        public static ChunkDefinition[] BuildChunksForLevel(int levelIndex)
        {
            int chunkCount = 5 + Mathf.Min(3, levelIndex);
            var chunks = new ChunkDefinition[chunkCount];
            for (int c = 0; c < chunkCount; c++)
            {
                var chunk = ScriptableObject.CreateInstance<ChunkDefinition>();
                chunk.name = $"Chunk_L{levelIndex + 1:00}_{c + 1:00}";
                chunk.LengthMeters = 30f;

                var gates = new List<ChunkDefinition.GateSpec>();
                var enemies = new List<ChunkDefinition.EnemySpec>();
                int addValue = 4 + levelIndex * 2 + c;

                // Two add gates in different lanes — steering earns force.
                gates.Add(new ChunkDefinition.GateSpec
                {
                    Op = GateOp.Add, Value = addValue, Lane = (c % 3) - 1, Position = 8f
                });
                gates.Add(new ChunkDefinition.GateSpec
                {
                    Op = GateOp.Add, Value = addValue + 2, Lane = ((c + 1) % 3) - 1, Position = 16f
                });

                // Every third chunk offers one multiplier opposite a subtract trap.
                if (c % 3 == 2)
                {
                    int lane = (c % 2 == 0) ? -1 : 1;
                    gates.Add(new ChunkDefinition.GateSpec
                    {
                        Op = GateOp.Multiply, Value = 2, Lane = lane, Position = 24f
                    });
                    gates.Add(new ChunkDefinition.GateSpec
                    {
                        Op = GateOp.Subtract, Value = addValue * 2, Lane = -lane, Position = 24f
                    });
                }

                // Enemy pressure in the middle lane, scaling with progress.
                if (c > 0)
                {
                    enemies.Add(new ChunkDefinition.EnemySpec
                    {
                        ForceCost = 3 + levelIndex * 2 + c * 2, Lane = 0, Position = 22f
                    });
                }

                chunk.Gates = gates.ToArray();
                chunk.Enemies = enemies.ToArray();
                chunks[c] = chunk;
            }
            return chunks;
        }

        /// <summary>Optimistic path (hit every add/multiply, dodge subtracts and enemies) scaled to a realistic par.</summary>
        public static long EstimateParForce(ChunkDefinition[] chunks, int startingForce)
        {
            long force = startingForce;
            foreach (ChunkDefinition chunk in chunks)
            {
                foreach (ChunkDefinition.GateSpec gate in chunk.Gates)
                {
                    if (gate.Op == GateOp.Subtract) continue;
                    force = GateMath.ApplyGate(force, gate.Op, gate.Value, long.MaxValue - 1, out _);
                }
            }
            return (long)(force * 0.6f);
        }
    }
}
