using System;
using System.Collections.Generic;
using BattleRunner.Core.Run;
using BattleRunner.Data.Definitions;
using BattleRunner.Gameplay.Crowd;
using UnityEngine;

namespace BattleRunner.Gameplay.Track
{
    /// <summary>
    /// Lays out a finite level from pooled chunk content and resolves gate/enemy/finish
    /// crossings against the crowd centroid — arithmetic in lane space, no physics (R3).
    /// </summary>
    public sealed class TrackController : MonoBehaviour
    {
        public event Action<GateOp, int> GateApplied;
        public event Action<int> EnemyContact;
        public event Action FinishReached;

        private ObjectPool<GateBehaviour> _gatePool;
        private ObjectPool<EnemyPackBehaviour> _enemyPool;
        private readonly List<GateBehaviour> _activeGates = new List<GateBehaviour>();
        private readonly List<EnemyPackBehaviour> _activeEnemies = new List<EnemyPackBehaviour>();
        private readonly List<GameObject> _groundStrips = new List<GameObject>();

        private Transform _trackRoot;
        private float _laneWidth = 2.2f;
        private float _finishZ;
        private bool _finishRaised;
        private Material _groundMaterial;
        private Material _finishMaterial;

        public float FinishZ => _finishZ;

        public void Initialize(Material baseMaterial, Material enemyMaterial, Mesh unitMesh, Font font, float laneWidth)
        {
            _laneWidth = laneWidth;
            _trackRoot = new GameObject("TrackRoot").transform;
            _trackRoot.SetParent(transform, false);

            var poolRoot = new GameObject("TrackPools").transform;
            poolRoot.SetParent(transform, false);

            GateBehaviour.SetSharedMaterials(baseMaterial);
            _gatePool = new ObjectPool<GateBehaviour>(() => GateBehaviour.Build(font), poolRoot);
            _enemyPool = new ObjectPool<EnemyPackBehaviour>(
                () => EnemyPackBehaviour.Build(unitMesh, enemyMaterial, font), poolRoot);
            _gatePool.Prewarm(14);
            _enemyPool.Prewarm(20);

            _groundMaterial = new Material(baseMaterial);
            _groundMaterial.SetColor("_BaseColor", new Color(0.10f, 0.09f, 0.12f));
            _groundMaterial.SetColor("_EmissionColor", Color.black);

            _finishMaterial = new Material(baseMaterial);
            _finishMaterial.SetColor("_BaseColor", new Color(0.9f, 0.65f, 0.2f) * 0.5f);
            _finishMaterial.SetColor("_EmissionColor", new Color(1.2f, 0.85f, 0.25f));
        }

        public void BuildLevel(LevelDefinition level)
        {
            ClearLevel();
            _finishRaised = false;

            float z = 12f; // breathing room before the first chunk
            if (level.Chunks != null)
            {
                foreach (ChunkDefinition chunk in level.Chunks)
                {
                    if (chunk == null) continue;
                    SpawnChunk(chunk, z);
                    z += chunk.LengthMeters;
                }
            }

            _finishZ = z + 8f;
            SpawnGroundStrip(-6f, _finishZ + 40f);
            SpawnFinishLine(_finishZ);
        }

        private void SpawnChunk(ChunkDefinition chunk, float startZ)
        {
            if (chunk.Gates != null)
            {
                foreach (ChunkDefinition.GateSpec spec in chunk.Gates)
                {
                    GateBehaviour gate = _gatePool.Get(_trackRoot);
                    gate.Setup(spec.Op, spec.Value, spec.Lane,
                        new Vector3(spec.Lane * _laneWidth, 0f, startZ + spec.Position));
                    _activeGates.Add(gate);
                }
            }

            if (chunk.Enemies != null)
            {
                foreach (ChunkDefinition.EnemySpec spec in chunk.Enemies)
                {
                    EnemyPackBehaviour pack = _enemyPool.Get(_trackRoot);
                    pack.Setup(spec.ForceCost,
                        new Vector3(spec.Lane * _laneWidth, 0f, startZ + spec.Position));
                    _activeEnemies.Add(pack);
                }
            }
        }

        private void SpawnGroundStrip(float fromZ, float toZ)
        {
            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(_trackRoot, false);
            float length = toZ - fromZ;
            ground.transform.position = new Vector3(0f, -0.1f, fromZ + length * 0.5f);
            ground.GetComponent<MeshFilter>().sharedMesh =
                ProceduralMeshes.BuildBox(Vector3.zero, new Vector3(_laneWidth * 3.6f, 0.2f, length));
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _groundMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundStrips.Add(ground);
        }

        private void SpawnFinishLine(float z)
        {
            var finish = new GameObject("FinishLine", typeof(MeshFilter), typeof(MeshRenderer));
            finish.transform.SetParent(_trackRoot, false);
            finish.transform.position = new Vector3(0f, 0.05f, z);
            finish.GetComponent<MeshFilter>().sharedMesh =
                ProceduralMeshes.BuildBox(Vector3.zero, new Vector3(_laneWidth * 3.4f, 0.1f, 1.2f));
            var renderer = finish.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _finishMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundStrips.Add(finish);
        }

        public void ClearLevel()
        {
            foreach (GateBehaviour gate in _activeGates) _gatePool.Release(gate);
            _activeGates.Clear();
            foreach (EnemyPackBehaviour pack in _activeEnemies) _enemyPool.Release(pack);
            _activeEnemies.Clear();
            foreach (GameObject strip in _groundStrips) Destroy(strip);
            _groundStrips.Clear();
        }

        /// <summary>Crossing checks against the crowd centroid. Called once per frame during RunnerLoop.</summary>
        public void Tick(CrowdController crowd)
        {
            float crowdZ = crowd.CenterZ;
            float crowdX = crowd.CenterX;
            float halfLane = _laneWidth * 0.75f;

            for (int i = _activeGates.Count - 1; i >= 0; i--)
            {
                GateBehaviour gate = _activeGates[i];
                if (gate.transform.position.z > crowdZ + 0.6f) continue;

                if (!gate.Consumed && Mathf.Abs(crowdX - gate.transform.position.x) <= halfLane)
                {
                    gate.Consume();
                    GateApplied?.Invoke(gate.Op, gate.Value);
                }
                _activeGates.RemoveAt(i);
                _gatePool.Release(gate);
            }

            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                if (pack.transform.position.z > crowdZ + 0.8f) continue;

                if (!pack.Defeated && Mathf.Abs(crowdX - pack.transform.position.x) <= halfLane)
                {
                    pack.Defeat();
                    EnemyContact?.Invoke(pack.ForceCost);
                }
                _activeEnemies.RemoveAt(i);
                _enemyPool.Release(pack);
            }

            if (!_finishRaised && crowdZ >= _finishZ)
            {
                _finishRaised = true;
                FinishReached?.Invoke();
            }
        }

        /// <summary>Spell effect in the runner phase: destroys enemy packs within range ahead.</summary>
        public int ClearEnemiesAhead(float fromZ, float rangeMeters)
        {
            int cleared = 0;
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                float z = pack.transform.position.z;
                if (z < fromZ || z > fromZ + rangeMeters) continue;
                _activeEnemies.RemoveAt(i);
                _enemyPool.Release(pack);
                cleared++;
            }
            return cleared;
        }
    }
}
