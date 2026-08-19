using System;
using System.Collections.Generic;

namespace BattleRunner.Core.Loot
{
    /// <summary>
    /// Weighted loot rolls with a legendary pity counter. Deterministic under an
    /// injected Random so distributions and the pity floor are provable in tests.
    /// </summary>
    public static class LootRoller
    {
        /// <param name="pityCounter">Consecutive non-legendary rolls so far; updated by this call.</param>
        /// <param name="luckMultiplier">Scales the weight of Epic+Legendary entries (overflow bonus, ads). 1 = neutral.</param>
        public static GearItemModel Roll(
            LootTableModel table,
            IReadOnlyDictionary<string, GearItemModel> itemLookup,
            Random random,
            ref int pityCounter,
            float luckMultiplier = 1f)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (itemLookup == null) throw new ArgumentNullException(nameof(itemLookup));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (table.Entries.Length == 0) throw new ArgumentException("Loot table has no entries.", nameof(table));

            bool pityTriggered = table.PityLegendaryFloor > 0 && pityCounter >= table.PityLegendaryFloor;

            GearItemModel rolled = RollWeighted(table, itemLookup, random, luckMultiplier,
                legendaryOnly: pityTriggered);

            // A table can legitimately contain no legendaries; pity then has nothing to force.
            rolled ??= RollWeighted(table, itemLookup, random, luckMultiplier, legendaryOnly: false);

            if (rolled == null)
                throw new InvalidOperationException("Loot table entries reference no known items.");

            pityCounter = rolled.Rarity == Rarity.Legendary ? 0 : pityCounter + 1;
            return rolled;
        }

        private static GearItemModel RollWeighted(
            LootTableModel table,
            IReadOnlyDictionary<string, GearItemModel> itemLookup,
            Random random,
            float luckMultiplier,
            bool legendaryOnly)
        {
            double totalWeight = 0;
            foreach (LootEntry entry in table.Entries)
            {
                GearItemModel item = Lookup(itemLookup, entry.ItemId);
                if (item == null || (legendaryOnly && item.Rarity != Rarity.Legendary)) continue;
                totalWeight += EffectiveWeight(entry, item, luckMultiplier);
            }

            if (totalWeight <= 0) return null;

            double pick = random.NextDouble() * totalWeight;
            foreach (LootEntry entry in table.Entries)
            {
                GearItemModel item = Lookup(itemLookup, entry.ItemId);
                if (item == null || (legendaryOnly && item.Rarity != Rarity.Legendary)) continue;
                pick -= EffectiveWeight(entry, item, luckMultiplier);
                if (pick <= 0) return item;
            }

            // Floating-point tail: return the last eligible entry.
            for (int i = table.Entries.Length - 1; i >= 0; i--)
            {
                GearItemModel item = Lookup(itemLookup, table.Entries[i].ItemId);
                if (item != null && (!legendaryOnly || item.Rarity == Rarity.Legendary)) return item;
            }
            return null;
        }

        private static double EffectiveWeight(LootEntry entry, GearItemModel item, float luckMultiplier)
        {
            double w = Math.Max(0f, entry.Weight);
            if (item.Rarity >= Rarity.Epic && luckMultiplier > 1f) w *= luckMultiplier;
            return w;
        }

        private static GearItemModel Lookup(IReadOnlyDictionary<string, GearItemModel> lookup, string id) =>
            id != null && lookup.TryGetValue(id, out GearItemModel item) ? item : null;
    }
}
