using System.Collections.Generic;
using BattleRunner.Core.Loot;
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
            var modifiers = new List<StatModifier>
            {
                new StatModifier(StatIds.Damage, ModifierKind.Flat,
                    profile.GetStatPoints(StatIds.Damage) * balance.DamagePerPoint),
                new StatModifier(StatIds.Health, ModifierKind.Flat,
                    profile.GetStatPoints(StatIds.Health) * balance.HealthPerPoint),
                new StatModifier(StatIds.Cooldown, ModifierKind.Flat,
                    profile.GetStatPoints(StatIds.Cooldown) * balance.CooldownPerPoint)
            };

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

            return $"Might {stats.Get(StatIds.Damage):0.#}   Vigor {stats.Get(StatIds.Health):0.#}   " +
                   $"Focus -{stats.Get(StatIds.Cooldown):P0}\n" +
                   $"{GearLine(GearSlot.Weapon)}  |  {GearLine(GearSlot.Armor)}  |  {GearLine(GearSlot.Relic)}";
        }

        public static string DescribeModifiers(GearItemDefinition def)
        {
            if (def.Modifiers == null || def.Modifiers.Length == 0) return "No bonuses";
            var lines = new List<string>();
            foreach (StatModifier m in def.Modifiers)
            {
                string statName = m.StatId switch
                {
                    StatIds.Damage => "Might",
                    StatIds.Health => "Vigor",
                    StatIds.Cooldown => "Focus",
                    _ => m.StatId
                };
                lines.Add(m.Kind == ModifierKind.Flat
                    ? $"+{m.Value:0.##} {statName}"
                    : $"+{m.Value:P0} {statName}");
            }
            return string.Join("\n", lines);
        }
    }
}
