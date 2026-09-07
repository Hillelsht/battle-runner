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
                // The kind alone does NOT decide the units, and this is the second place
                // that assumption has been wrong. Fraction-valued stats — Focus, Fortune,
                // gate yield, run speed, cooldown — are granted as Flat because StatSheet
                // resolves final = (base + flat) * (1 + percent) and their base is 0, so a
                // Percent modifier would multiply nothing. Scoring them raw made a "+1%
                // Focus" affix worth 0.01 points instead of 1: an Ember Talisman with
                // +2 Might and +1% Focus scored exactly 2, the same as if its second affix
                // did not exist, and Auto-Equip ranked every fraction-stat relic as junk.
                // StatFormat.IsFraction is the single source of truth for this and is
                // already engine-free.
                bool asPercent = m.Kind == ModifierKind.Percent || StatFormat.IsFraction(m.StatId);
                float normalized = asPercent ? m.Value * percentScale : m.Value;
                power += weight * normalized;
            }
            return power;
        }
    }
}
