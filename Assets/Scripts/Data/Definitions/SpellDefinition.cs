using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Spell", fileName = "Spell")]
    public sealed class SpellDefinition : ScriptableObject
    {
        [Header("Spell (flick up)")]
        [Tooltip("Base cooldown in seconds; reduced by the Cooldown stat.")]
        public float CooldownSeconds = 6f;
        [Tooltip("Damage dealt to the boss per cast, scaled by the Damage stat.")]
        public float BossDamageMultiplier = 5f;
        [Tooltip("Runner phase: enemy packs within this many meters ahead are destroyed.")]
        public float ClearRangeMeters = 15f;

        [Header("Shield (flick down)")]
        public float ShieldCooldownSeconds = 8f;
        public float ShieldDurationSeconds = 2f;
    }
}
