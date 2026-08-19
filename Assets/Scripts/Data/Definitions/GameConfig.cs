using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    /// <summary>
    /// Root content reference, loaded from Resources/GameConfig at boot. When the asset
    /// is missing (fresh clone before content generation runs), the runtime builds an
    /// equivalent config in memory from DefaultContent so the game always plays.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleRunner/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public BalanceSettings Balance;
        public InputSettingsSO Input;
        public SpellDefinition Spells;
        public LevelDefinition[] Levels;
        public StatDefinition[] Stats;

        [Tooltip("Every gear definition in the game — the id->definition registry for equip and save resolution.")]
        public GearItemDefinition[] AllGear;

        public LevelDefinition LevelFor(int levelIndex)
        {
            if (Levels == null || Levels.Length == 0) return null;
            // Past the authored list, loop the last level; the boss keeps scaling by index.
            return Levels[Mathf.Min(levelIndex, Levels.Length - 1)];
        }
    }
}
