using System.Collections.Generic;
using BattleRunner.Core.Stats;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Balance Settings", fileName = "Balance")]
    public sealed class BalanceSettings : ScriptableObject
    {
        [Header("Force (doc 01, R4)")]
        public long SoftCap = 100_000;

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
            [StatIds.Cooldown] = BaseCooldownReduction
        };
    }
}
