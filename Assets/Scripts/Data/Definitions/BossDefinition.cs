using BattleRunner.Core.Boss;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Boss", fileName = "Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        public string DisplayName = "Bone Colossus";

        [Tooltip("What this boss DOES and what it looks like — the two are deliberately coupled.")]
        public BossArchetype Archetype = BossArchetype.Slam;
        [Tooltip("HP at level 0; grows exponentially per level (BossSim.BossHp).")]
        public float BaseHp = 500f;
        [Range(0f, 1f)] public float PerLevelGrowth = 0.25f;
        [Tooltip("Seconds between boss attacks.")]
        public float AttackIntervalSeconds = 4f;
        [Tooltip("Seconds of telegraph before each attack lands — the shield window.")]
        public float TelegraphSeconds = 1.2f;
        [Range(0f, 1f)] public float HitFraction = 0.3f;
        public Color TintColor = new Color(0.6f, 0.2f, 0.2f);

        [Tooltip("Emission the boss heats toward while winding up. Its own colour, so a player learns whose telegraph they are watching.")]
        [ColorUsage(false, true)] public Color TelegraphColor = new Color(1.4f, 0.5f, 0.2f);
    }
}
