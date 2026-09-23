using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Stat Definition", fileName = "Stat")]
    public sealed class StatDefinition : ScriptableObject
    {
        [Tooltip("Canonical id from BattleRunner.Core.Stats.StatIds")]
        public string Id;

        public Core.Text.LocKey NameKey;
        public Core.Text.LocKey DescriptionKey;

        public string DisplayName => Core.Text.Loc.Get(NameKey);
        public string Description => Core.Text.Loc.Get(DescriptionKey);
    }
}
