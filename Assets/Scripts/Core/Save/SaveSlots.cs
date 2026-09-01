using System;

namespace BattleRunner.Core.Save
{
    /// <summary>What the slot picker needs to know about one save, without opening it twice.</summary>
    public readonly struct SaveSlotSummary
    {
        public int Index { get; }
        public bool Occupied { get; }
        public int LevelIndex { get; }
        public int TalentsLearned { get; }
        public int UnspentPoints { get; }

        public SaveSlotSummary(int index, bool occupied, int levelIndex, int talentsLearned, int unspentPoints)
        {
            Index = index;
            Occupied = occupied;
            LevelIndex = levelIndex;
            TalentsLearned = talentsLearned;
            UnspentPoints = unspentPoints;
        }

        /// <summary>One line for the slot button. Empty slots read as an invitation, not a blank.</summary>
        public string Describe()
        {
            if (!Occupied) return $"SLOT {Index + 1}\nNew game";

            string talents = TalentsLearned == 1 ? "1 talent" : $"{TalentsLearned} talents";
            string unspent = UnspentPoints > 0 ? $" · {UnspentPoints} unspent" : string.Empty;
            return $"SLOT {Index + 1}\nLevel {LevelIndex + 1} · {talents}{unspent}";
        }
    }

    /// <summary>
    /// Three independent saves, addressed by file name.
    ///
    /// The genre convention is one cloud-synced profile per device, and slots are a
    /// console-RPG idea — but they are what this project asked for, and the cost is small
    /// because FileSaveService already takes its file name as a constructor argument.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 3;

        /// <summary>The single-profile file every build before slots wrote to.</summary>
        public const string LegacyFileName = "profile.sav";

        public static string FileName(int slot)
        {
            if (slot < 0 || slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
            return $"profile_{slot}.sav";
        }

        /// <summary>A profile that has never been played, so an empty slot is not mistaken for a save.</summary>
        public static bool IsUntouched(PlayerProfile profile) =>
            profile == null ||
            (profile.CurrentLevelIndex == 0 &&
             profile.UnspentStatPoints == 0 &&
             (profile.SkillNodes == null || profile.SkillNodes.Count == 0) &&
             (profile.Inventory == null || profile.Inventory.Count == 0) &&
             profile.TutorialMask == 0);

        public static SaveSlotSummary Summarize(int index, PlayerProfile profile, bool fileExists)
        {
            if (!fileExists || profile == null)
                return new SaveSlotSummary(index, false, 0, 0, 0);

            int talents = profile.SkillNodes?.Count ?? 0;
            return new SaveSlotSummary(index, true, profile.CurrentLevelIndex, talents,
                profile.UnspentStatPoints);
        }
    }
}
