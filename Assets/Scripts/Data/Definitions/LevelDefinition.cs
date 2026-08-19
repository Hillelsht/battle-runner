using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Level", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public string DisplayName = "The Ashen Road";
        [Tooltip("Ordered chunk sequence; total length defines the run.")]
        public ChunkDefinition[] Chunks;
        public BossDefinition Boss;
        public LootTableDefinition LootTable;
        [Tooltip("Force a par player is expected to hold at the finish line — used to validate gate authoring against R4.")]
        public long ParForceAtFinish = 150;
        [Min(1)] public int StartingForce = 5;
    }
}
