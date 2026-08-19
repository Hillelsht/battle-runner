using System;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Loot
{
    public enum GearSlot
    {
        Weapon = 0,
        Armor = 1,
        Relic = 2
    }

    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    /// <summary>Engine-free mirror of a GearItemDefinition — what the core math needs to know about an item.</summary>
    [Serializable]
    public sealed class GearItemModel
    {
        public string Id;
        public GearSlot Slot;
        public Rarity Rarity;
        public StatModifier[] Modifiers;

        public GearItemModel(string id, GearSlot slot, Rarity rarity, StatModifier[] modifiers)
        {
            Id = id;
            Slot = slot;
            Rarity = rarity;
            Modifiers = modifiers ?? Array.Empty<StatModifier>();
        }
    }

    [Serializable]
    public struct LootEntry
    {
        public string ItemId;
        public float Weight;

        public LootEntry(string itemId, float weight)
        {
            ItemId = itemId;
            Weight = weight;
        }
    }

    /// <summary>Engine-free mirror of a LootTableDefinition.</summary>
    [Serializable]
    public sealed class LootTableModel
    {
        public LootEntry[] Entries;

        /// <summary>After this many consecutive rolls without a Legendary, the next roll is forced Legendary.</summary>
        public int PityLegendaryFloor;

        public LootTableModel(LootEntry[] entries, int pityLegendaryFloor)
        {
            Entries = entries ?? Array.Empty<LootEntry>();
            PityLegendaryFloor = pityLegendaryFloor;
        }
    }
}
