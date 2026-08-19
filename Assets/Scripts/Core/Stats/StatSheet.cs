using System;
using System.Collections.Generic;

namespace BattleRunner.Core.Stats
{
    /// <summary>
    /// Resolved stat values for one actor. Resolution order per stat:
    /// final = (base + sum of flat modifiers) * (1 + sum of percent modifiers).
    /// Resolved once per equip change / run start — never per frame.
    /// </summary>
    public sealed class StatSheet
    {
        private readonly Dictionary<string, float> _values;

        private StatSheet(Dictionary<string, float> values)
        {
            _values = values;
        }

        public float Get(string statId) =>
            _values.TryGetValue(statId, out float v) ? v : 0f;

        public IReadOnlyDictionary<string, float> Values => _values;

        public static StatSheet Resolve(
            IReadOnlyDictionary<string, float> baseValues,
            IEnumerable<StatModifier> modifiers)
        {
            if (baseValues == null) throw new ArgumentNullException(nameof(baseValues));

            var flat = new Dictionary<string, float>();
            var percent = new Dictionary<string, float>();
            if (modifiers != null)
            {
                foreach (StatModifier m in modifiers)
                {
                    if (string.IsNullOrEmpty(m.StatId)) continue;
                    var bucket = m.Kind == ModifierKind.Flat ? flat : percent;
                    bucket.TryGetValue(m.StatId, out float acc);
                    bucket[m.StatId] = acc + m.Value;
                }
            }

            var final = new Dictionary<string, float>();
            var statIds = new HashSet<string>(baseValues.Keys);
            statIds.UnionWith(flat.Keys);
            statIds.UnionWith(percent.Keys);
            foreach (string id in statIds)
            {
                baseValues.TryGetValue(id, out float baseV);
                flat.TryGetValue(id, out float flatV);
                percent.TryGetValue(id, out float pctV);
                final[id] = (baseV + flatV) * (1f + pctV);
            }

            return new StatSheet(final);
        }
    }
}
