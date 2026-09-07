using System;
using System.Collections.Generic;
using BattleRunner.Core.Loot;

namespace BattleRunner.Core.Save
{
    /// <summary>An owned roll of a gear definition.</summary>
    [Serializable]
    public class GearItemInstance
    {
        public string InstanceId;
        public string DefinitionId;
        /// <summary>Future-proofing for affix rolls; always 0 in the MVP.</summary>
        public int RolledTier;
    }

    [Serializable]
    public class EquippedSlot
    {
        public GearSlot Slot;
        public string InstanceId;
    }

    /// <summary>One id and how many ranks of it are held. See PlayerProfile.SkillRanks.</summary>
    [Serializable]
    public class RankEntry
    {
        public string Id;
        public int Rank;
    }

    [Serializable]
    public class StatSpend
    {
        public string StatId;
        public int Points;
    }

    /// <summary>
    /// The persistent save model. Deliberately list-based (no dictionaries) so
    /// Unity's JsonUtility can serialize it; helpers below give map-style access.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        // Defaults to 1, not CurrentVersion: a JSON payload that lacks the field must
        // deserialize as OLD so every migration (and its null-healing) still runs.
        public int SchemaVersion = 1;
        public List<GearItemInstance> Inventory = new List<GearItemInstance>();
        public List<EquippedSlot> Equipped = new List<EquippedSlot>();
        public List<StatSpend> StatPoints = new List<StatSpend>();
        public int UnspentStatPoints;
        public int CurrentLevelIndex;
        public long SoftCurrency;
        public int Keys;
        public int PityCounter;
        /// <summary>Bitmask of TutorialStep values already taught. 0 = brand new player.</summary>
        public int TutorialMask;
        /// <summary>
        /// Legacy: the flat set of node ids learned under schema v4, when every node was a
        /// single point. Kept as the migration source only — v5 onward reads SkillRanks.
        /// </summary>
        public List<string> SkillNodes = new List<string>();

        /// <summary>
        /// Ranks held per skill node, and per paragon track. Lists of id/count pairs rather
        /// than dictionaries because Unity's JsonUtility cannot serialize a Dictionary, which
        /// is the same reason Inventory and Equipped are shaped this way.
        /// </summary>
        public List<RankEntry> SkillRanks = new List<RankEntry>();
        public List<RankEntry> ParagonRanks = new List<RankEntry>();

        /// <summary>Map view of SkillRanks for the Core progression rules.</summary>
        public Dictionary<string, int> SkillRankMap() => ToMap(SkillRanks);

        /// <summary>Map view of ParagonRanks.</summary>
        public Dictionary<string, int> ParagonRankMap() => ToMap(ParagonRanks);

        private static Dictionary<string, int> ToMap(List<RankEntry> entries)
        {
            var map = new Dictionary<string, int>();
            if (entries == null) return map;
            foreach (RankEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Rank <= 0) continue;
                // Sum rather than overwrite: a save hand-edited or merged into holding an id
                // twice should resolve to something sane instead of silently losing points.
                map[entry.Id] = map.TryGetValue(entry.Id, out int held) ? held + entry.Rank : entry.Rank;
            }
            return map;
        }

        /// <summary>Writes a map back, dropping empty ranks so the file stays small.</summary>
        public static void WriteMap(List<RankEntry> entries, Dictionary<string, int> map)
        {
            entries.Clear();
            if (map == null) return;
            foreach (KeyValuePair<string, int> pair in map)
                if (pair.Value > 0) entries.Add(new RankEntry { Id = pair.Key, Rank = pair.Value });
        }

        public string GetEquipped(GearSlot slot)
        {
            foreach (EquippedSlot e in Equipped)
                if (e.Slot == slot) return e.InstanceId;
            return null;
        }

        public void SetEquipped(GearSlot slot, string instanceId)
        {
            foreach (EquippedSlot e in Equipped)
            {
                if (e.Slot != slot) continue;
                e.InstanceId = instanceId;
                return;
            }
            Equipped.Add(new EquippedSlot { Slot = slot, InstanceId = instanceId });
        }

        public int GetStatPoints(string statId)
        {
            foreach (StatSpend s in StatPoints)
                if (s.StatId == statId) return s.Points;
            return 0;
        }

        public void AddStatPoint(string statId)
        {
            foreach (StatSpend s in StatPoints)
            {
                if (s.StatId != statId) continue;
                s.Points++;
                return;
            }
            StatPoints.Add(new StatSpend { StatId = statId, Points = 1 });
        }

        public GearItemInstance FindInstance(string instanceId)
        {
            foreach (GearItemInstance i in Inventory)
                if (i.InstanceId == instanceId) return i;
            return null;
        }
    }
}
