using System;
using System.Collections.Generic;
using BattleRunner.Core.Stats;

namespace BattleRunner.Core.Loot
{
    /// <summary>
    /// Collapses an item to one scalar so Auto-Equip is a one-tap deterministic
    /// comparison (doc 01, R6). Weights come from BalanceSettings in the Data layer.
    /// </summary>
    public static class ItemPower
    {
        /// <param name="statWeights">Per-stat weight; a stat missing from the map contributes nothing.</param>
        /// <param name="percentScale">How many flat points one 100% percent-modifier is worth per stat weight unit.</param>
        public static float Compute(
            GearItemModel item,
            IReadOnlyDictionary<string, float> statWeights,
            float percentScale = 100f)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (statWeights == null) throw new ArgumentNullException(nameof(statWeights));

            float power = 0f;
            foreach (StatModifier m in item.Modifiers)
            {
                if (!statWeights.TryGetValue(m.StatId, out float weight)) continue;
                float normalized = m.Kind == ModifierKind.Flat ? m.Value : m.Value * percentScale;
                power += weight * normalized;
            }
            return power;
        }
    }
}
