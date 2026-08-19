using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Stat Definition", fileName = "Stat")]
    public sealed class StatDefinition : ScriptableObject
    {
        [Tooltip("Canonical id from BattleRunner.Core.Stats.StatIds")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
    }
}
