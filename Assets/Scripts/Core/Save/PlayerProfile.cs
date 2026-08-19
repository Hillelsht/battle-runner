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
