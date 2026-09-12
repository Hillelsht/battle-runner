using System.Collections.Generic;
using BattleRunner.Core.Stats;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Balance Settings", fileName = "Balance")]
    public sealed class BalanceSettings : ScriptableObject
    {
        // THE FORCE SOFT CAP IS GONE. It was 100,000, and it was correct for a game that
        // re-mustered five men every round: a ceiling was the only thing stopping a multiply
        // chain from running away inside one run. With the army continuous it stops being a
        // safety rail and becomes the whole game — simulated with the army carried over, a
        // player is pinned at the cap by ROUND FIVE and every round after it is a flat 1.00x,
        // with every gate on the road doing nothing measurable. Nothing needs capping now
        // because nothing is absolute: a gate is a share, so it cannot run away in the first
        // place. See Core/Run/GateMath.

        [Header("Crowd rendering tier caps (doc 04)")]
        public int TierCapLow = 100;
        public int TierCapMid = 200;
        public int TierCapHigh = 300;

        [Header("Hero base stats")]
        public float BaseDamage = 10f;
        public float BaseHealth = 100f;
        public float BaseCooldownReduction = 0f;

        [Header("Per stat point granted between runs")]
        public float DamagePerPoint = 2f;
        public float HealthPerPoint = 15f;
        public float CooldownPerPoint = 0.04f;

        [Header("Item Power weights (doc 01, R6)")]
        public float DamageWeight = 1f;
        public float HealthWeight = 0.5f;
        public float CooldownWeight = 0.8f;

        [Header("Runner")]
        public float RunSpeedMetersPerSec = 10f;
        public float LaneWidthMeters = 2.2f;
        [Min(1)] public int StatPointsPerBossKill = 3;

        public Dictionary<string, float> StatWeights() => new Dictionary<string, float>
        {
            [StatIds.Damage] = DamageWeight,
            [StatIds.Health] = HealthWeight,
            [StatIds.Cooldown] = CooldownWeight
        };

        public Dictionary<string, float> BaseStats() => new Dictionary<string, float>
        {
            [StatIds.Damage] = BaseDamage,
            [StatIds.Health] = BaseHealth,
            [StatIds.Cooldown] = BaseCooldownReduction,
            // The run axes start at zero: they are pure upside bought from the tree, so a
            // player who has spent nothing plays exactly the game they played before.
            [StatIds.SpellPower] = 0f,
            [StatIds.GateYield] = 0f,
            [StatIds.RunSpeed] = 0f,
            [StatIds.EnemyResist] = 0f,
            [StatIds.ShieldDuration] = 0f,
            [StatIds.Fortune] = 0f
        };
    }
}
