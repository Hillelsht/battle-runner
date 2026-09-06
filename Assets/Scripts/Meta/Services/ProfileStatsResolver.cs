using System.Collections.Generic;
using BattleRunner.Core.Loot;
using BattleRunner.Core.Progression;
using BattleRunner.Core.Save;
using BattleRunner.Core.Stats;
using BattleRunner.Data.Definitions;

namespace BattleRunner.Meta.Services
{
    /// <summary>
    /// Turns profile (stat points + equipped gear) into a resolved StatSheet.
    /// Called on equip changes and run start — never per frame (doc 03).
    /// </summary>
    public static class ProfileStatsResolver
    {
        public static Dictionary<string, GearItemDefinition> GearById(GameConfig config)
        {
            var byId = new Dictionary<string, GearItemDefinition>();
            if (config.AllGear != null)
                foreach (GearItemDefinition item in config.AllGear)
                    if (item != null && !byId.ContainsKey(item.Id))
                        byId[item.Id] = item;
            return byId;
        }

        public static StatSheet Resolve(PlayerProfile profile, GameConfig config)
        {
            BalanceSettings balance = config.Balance;

            // Talents and gear feed the same resolution path, so a node and an affix compose
            // exactly as two affixes do — no second set of rules to keep in step.
            var modifiers = new List<StatModifier>(SkillTree.ModifiersFor(profile.SkillNodes));

            Dictionary<string, GearItemDefinition> gearById = GearById(config);
            foreach (GearSlot slot in new[] { GearSlot.Weapon, GearSlot.Armor, GearSlot.Relic })
            {
                string instanceId = profile.GetEquipped(slot);
                if (instanceId == null) continue;
                GearItemInstance instance = profile.FindInstance(instanceId);
                if (instance == null) continue;
                if (!gearById.TryGetValue(instance.DefinitionId, out GearItemDefinition def)) continue;
                if (def.Modifiers != null) modifiers.AddRange(def.Modifiers);
            }

            return StatSheet.Resolve(balance.BaseStats(), modifiers);
        }

        public static string Summary(PlayerProfile profile, GameConfig config, StatSheet stats)
        {
            Dictionary<string, GearItemDefinition> gearById = GearById(config);
            string GearLine(GearSlot slot)
            {
                string instanceId = profile.GetEquipped(slot);
                GearItemInstance instance = instanceId == null ? null : profile.FindInstance(instanceId);
                if (instance != null && gearById.TryGetValue(instance.DefinitionId, out GearItemDefinition def))
                    return def.DisplayName;
                return "—";
            }

            // Every stat now goes through StatFormat, which knows which ids are fractions.
            // The hand-rolled version carried that knowledge in two places with two
            // different rules, and printed a base Cooldown of zero as "Focus -0 %" from a
            // hard-coded minus sign.
            var runLine = new List<string>();
            void Note(string statId)
            {
                float v = stats.Get(statId);
                // ShowsAsNonZero shares the formatter's own epsilon, so this gate cannot
                // drift from what Total() prints and let a "Gates 0%" through.
                if (v > 0f && StatFormat.ShowsAsNonZero(statId, v))
                    runLine.Add(StatFormat.Total(statId, v));
            }
            Note(StatIds.GateYield);
            Note(StatIds.RunSpeed);
            Note(StatIds.EnemyResist);
            Note(StatIds.SpellPower);
            Note(StatIds.Fortune);
            Note(StatIds.ShieldDuration);

            string second = runLine.Count > 0 ? string.Join("   ", runLine) + "\n" : string.Empty;

            // The gear row used to be three bare em-dashes when nothing was equipped:
            // "—  |  —  |  —" under the stats, which reads as a rendering fault rather
            // than as three empty slots. Name it.
            string gear = $"{GearLine(GearSlot.Weapon)}  |  {GearLine(GearSlot.Armor)}  |  " +
                          $"{GearLine(GearSlot.Relic)}";
            if (gear.Replace("—", string.Empty).Replace("|", string.Empty).Trim().Length == 0)
                gear = "no gear yet";

            return $"{StatFormat.Total(StatIds.Damage, stats.Get(StatIds.Damage))}   " +
                   $"{StatFormat.Total(StatIds.Health, stats.Get(StatIds.Health))}   " +
                   $"{StatFormat.Total(StatIds.Cooldown, stats.Get(StatIds.Cooldown))}\n" +
                   second + gear;
        }

        public static string DescribeModifiers(GearItemDefinition def)
        {
            if (def.Modifiers == null || def.Modifiers.Length == 0) return "No bonuses";
            // Units come from the STAT, never from the ModifierKind. Kind says how a
            // modifier composes — added into the base, or multiplied over the total — and
            // nothing about units. Choosing units by kind is what printed the Ember
            // Talisman's flat 0.01 Cooldown affix as "+0.01 Focus" instead of "+1% Focus".
            var lines = new List<string>();
            foreach (StatModifier m in def.Modifiers)
                lines.Add(StatFormat.Affix(m));
            return string.Join("\n", lines);
        }
    }
}
