using System;
using System.Collections.Generic;
using BattleRunner.Core.Loot;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Loot Table", fileName = "LootTable")]
    public sealed class LootTableDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public GearItemDefinition Item;
            public float Weight;
        }

        public Entry[] Entries;

        [Tooltip("Consecutive non-legendary rolls before a legendary is forced. 0 disables pity.")]
        public int PityLegendaryFloor = 20;

        public LootTableModel ToModel()
        {
            var entries = new List<LootEntry>();
            foreach (Entry e in Entries)
                if (e.Item != null)
                    entries.Add(new LootEntry(e.Item.Id, e.Weight));
            return new LootTableModel(entries.ToArray(), PityLegendaryFloor);
        }

        public Dictionary<string, GearItemModel> BuildItemLookup()
        {
            var lookup = new Dictionary<string, GearItemModel>();
            foreach (Entry e in Entries)
                if (e.Item != null && !lookup.ContainsKey(e.Item.Id))
                    lookup[e.Item.Id] = e.Item.ToModel();
            return lookup;
        }
    }
}
