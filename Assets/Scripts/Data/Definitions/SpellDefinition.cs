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
        [Tooltip("Runner phase: ambushes (packs AND red gates) within this many metres of the army's FRONT are destroyed.")]
        // 34, up from 15, and measured from the army's front line rather than its centre.
        // The crowd's leading plane stands up to 7 m ahead of the centroid, so the old sweep
        // reached about eight metres past the men — under one second of road at the run
        // speed, which is less time than it takes to see an ambush and flick at it. That is
        // the whole of "the spell doesn't destroy enemy packs": it did, for anything already
        // close enough to be unavoidable.
        public float ClearRangeMeters = 34f;

        [Header("Shield (flick down)")]
        public float ShieldCooldownSeconds = 8f;
        public float ShieldDurationSeconds = 2f;
    }
}
