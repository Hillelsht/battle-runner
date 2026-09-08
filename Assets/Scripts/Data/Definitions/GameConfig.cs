using BattleRunner.Core.Progression;
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

        [Tooltip("The boss roster, rotated by round index so consecutive rounds never repeat one.")]
        public BossDefinition[] Bosses;
        public StatDefinition[] Stats;

        [Tooltip("Every gear definition in the game — the id->definition registry for equip and save resolution.")]
        public GearItemDefinition[] AllGear;

        /// <summary>
        /// The level for a round. Past the authored list this CYCLES rather than clamping.
        ///
        /// Clamping meant every round from the sixth onward replayed the last level against
        /// the last boss, forever, with only the HP curve moving. Cycling costs nothing and
        /// means the road, the gate pattern and the boss all keep changing while the numbers
        /// climb — which is the difference between a long game and a long wait.
        /// </summary>
        public LevelDefinition LevelFor(int levelIndex)
        {
            if (Levels == null || Levels.Length == 0) return null;
            return Levels[Wrap(levelIndex, Levels.Length)];
        }

        /// <summary>
        /// The boss for a round, chosen by ROUND rather than by level.
        ///
        /// Taking it from the level asset tied the two cycles together, so a player who saw
        /// level 3 twice fought its boss twice. Rotating the boss on its own index means two
        /// coprime-ish cycles and a fresh pairing for a long time — and it guarantees the
        /// first N rounds are N different bosses, which is the actual complaint.
        /// </summary>
        public BossDefinition BossFor(int levelIndex)
        {
            if (Bosses != null && Bosses.Length > 0)
            {
                // The ACT's boss, not the round's. An act's boss looms at the finish line of
                // every round in it before it is finally fought on the last one, so the
                // creature that threatens on round two has to be the one that swings on round
                // four — picking per round would show the player a different monster each
                // time and make the whole build-up meaningless.
                RoundPlan plan = RoundPlan.For(levelIndex);
                return Bosses[RoundPlan.BossSlot(plan.ActIndex, Bosses.Length)];
            }

            LevelDefinition level = LevelFor(levelIndex);
            return level != null ? level.Boss : null;
        }

        private static int Wrap(int index, int length)
        {
            if (length <= 0) return 0;
            int wrapped = index % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }
    }
}
