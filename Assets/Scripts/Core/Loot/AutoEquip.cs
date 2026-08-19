using System;
using System.Collections.Generic;

namespace BattleRunner.Core.Loot
{
    /// <summary>One owned item instance paired with its resolved model.</summary>
    public readonly struct OwnedItem
    {
        public readonly string InstanceId;
        public readonly GearItemModel Model;

        public OwnedItem(string instanceId, GearItemModel model)
        {
            InstanceId = instanceId;
            Model = model;
        }
    }

    public static class AutoEquip
    {
        /// <summary>
        /// Picks the highest-Item-Power instance per slot. Deterministic: ties break
        /// toward the earliest instance in enumeration order, so repeated calls with
        /// the same inventory always produce the same loadout.
        /// </summary>
        public static Dictionary<GearSlot, string> PickBest(
            IEnumerable<OwnedItem> inventory,
            IReadOnlyDictionary<string, float> statWeights)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (statWeights == null) throw new ArgumentNullException(nameof(statWeights));

            var bestPower = new Dictionary<GearSlot, float>();
            var bestInstance = new Dictionary<GearSlot, string>();

            foreach (OwnedItem owned in inventory)
            {
                if (owned.Model == null || string.IsNullOrEmpty(owned.InstanceId)) continue;
                float power = ItemPower.Compute(owned.Model, statWeights);
                GearSlot slot = owned.Model.Slot;
                if (!bestPower.TryGetValue(slot, out float current) || power > current)
                {
                    bestPower[slot] = power;
                    bestInstance[slot] = owned.InstanceId;
                }
            }

            return bestInstance;
        }
    }
}
