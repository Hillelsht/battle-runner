using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Level", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        /// <summary>The level's name, as a key.</summary>
        public Core.Text.LocKey NameKey = Core.Text.LocKey.LevelAshenRoad;

        public string DisplayName => Core.Text.Loc.Get(NameKey);
        [Tooltip("Ordered chunk sequence; total length defines the run.")]
        public ChunkDefinition[] Chunks;
        public BossDefinition Boss;
        public LootTableDefinition LootTable;
        [Tooltip("Army a par player is expected to hold at the finish — advisory, from the seed muster.")]
        public double ParForceAtFinish = 150;
    }
}
