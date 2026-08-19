using System;
using BattleRunner.Core.Run;
using UnityEngine;

namespace BattleRunner.Data.Definitions
{
    [CreateAssetMenu(menuName = "BattleRunner/Track Chunk", fileName = "Chunk")]
    public sealed class ChunkDefinition : ScriptableObject
    {
        [Serializable]
        public struct GateSpec
        {
            public GateOp Op;
            [Min(0)] public int Value;
            [Range(-1, 1)] public int Lane;
            [Tooltip("Meters from the chunk start.")]
            public float Position;
        }

        [Serializable]
        public struct EnemySpec
        {
            [Min(1)] public int ForceCost;
            [Range(-1, 1)] public int Lane;
            [Tooltip("Meters from the chunk start.")]
            public float Position;
        }

        [Min(10f)] public float LengthMeters = 30f;
        public GateSpec[] Gates;
        public EnemySpec[] Enemies;
    }
}
