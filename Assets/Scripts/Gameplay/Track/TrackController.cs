using System;
using System.Collections.Generic;
using BattleRunner.Core.Crowd;
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

        /// <summary>Gates beyond this hide their label; 45 m chunks put the next decision well inside it.</summary>
        private const float LabelVisibleMeters = 34f;

        /// <summary>Extra clearance past the camera before a passed gate is recycled.</summary>
        private const float DespawnMarginMeters = 4f;

        private Transform _trackRoot;
        private float _laneWidth = 2.2f;
        private float _finishZ;
        private bool _finishRaised;
        private Material _groundMaterial;
        private Material _finishMaterial;
        private Material _markingMaterial;
        private Material _railMaterial;

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

            _groundMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _groundMaterial.SetColorSafe("_BaseColor", new Color(0.10f, 0.09f, 0.12f));
            _groundMaterial.SetColorSafe("_EmissionColor", Color.black);
            _groundMaterial.SetFloatSafe("_BobAmount", 0f); // static geometry must not run-bob

            _finishMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _finishMaterial.SetColorSafe("_BaseColor", new Color(0.9f, 0.65f, 0.2f) * 0.5f);
            _finishMaterial.SetColorSafe("_EmissionColor", new Color(1.2f, 0.85f, 0.25f));
            _finishMaterial.SetFloatSafe("_BobAmount", 0f);

            _markingMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _markingMaterial.SetColorSafe("_BaseColor", new Color(0.14f, 0.16f, 0.22f));
            _markingMaterial.SetColorSafe("_EmissionColor", new Color(0.30f, 0.34f, 0.45f));
            _markingMaterial.SetFloatSafe("_BobAmount", 0f);

            _railMaterial = ShaderSafety.CreateMaterial(baseMaterial);
            _railMaterial.SetColorSafe("_BaseColor", new Color(0.12f, 0.14f, 0.26f));
            _railMaterial.SetColorSafe("_EmissionColor", new Color(0.25f, 0.30f, 0.55f));
            _railMaterial.SetFloatSafe("_BobAmount", 0f);
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
                    pack.Setup(spec.ForceCost, spec.Lane,
                        new Vector3(spec.Lane * _laneWidth, 0f, startZ + spec.Position));
                    _activeEnemies.Add(pack);
                }
            }
        }

        private void SpawnGroundStrip(float fromZ, float toZ)
        {
            float length = toZ - fromZ;
            float midZ = fromZ + length * 0.5f;

            // Every dimension below comes off the lane pitch, so the road the player reads
            // is exactly the partition CrowdMath.LaneIndex scores against. The old version
            // used loose multiples of lane width (ground 4.8w, rails 2.3w) and drew lane
            // lines only at +/-0.5w, which left the outer lanes bounded by the rails at
            // 2.3w: the centre lane rendered 2.20 m and the outer two 3.96 m each.
            float roadHalf = CrowdMath.RoadHalfWidth(_laneWidth);   // 3.30 at a 2.2 m lane
            float shoulder = _laneWidth * 0.14f;                    // visible verge
            float railHalfThickness = 0.15f;
            float railCentre = roadHalf + shoulder + railHalfThickness;
            float groundHalf = railCentre + railHalfThickness + 0.25f;

            SpawnStatic("Ground", new Vector3(0f, -0.1f, midZ),
                new Vector3(groundHalf * 2f, 0.2f, length), _groundMaterial);

            // All FOUR lane edges, so each of the three lanes is bounded by a real line and
            // they read as equal. Without the outer pair the road has no visible edge and
            // the eye takes the rails as the boundary instead.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                SpawnStatic("LaneLineInner", new Vector3(edge * _laneWidth * 0.5f, 0.005f, midZ),
                    new Vector3(0.10f, 0.02f, length), _markingMaterial);
                SpawnStatic("LaneLineOuter", new Vector3(edge * roadHalf, 0.005f, midZ),
                    new Vector3(0.10f, 0.02f, length), _markingMaterial);

                SpawnStatic("Rail", new Vector3(edge * railCentre, 0.45f, midZ),
                    new Vector3(railHalfThickness * 2f, 0.9f, length), _railMaterial);
            }

            // Speed rungs span the road itself, not the verge.
            for (float z = fromZ; z < toZ; z += 6f)
            {
                SpawnStatic("Rung", new Vector3(0f, 0.006f, z),
                    new Vector3(roadHalf * 2f, 0.02f, 0.35f), _markingMaterial);
            }
        }

        private void SpawnStatic(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_trackRoot, false);
            go.transform.position = position;
            go.GetComponent<MeshFilter>().sharedMesh = ProceduralMeshes.BuildBox(Vector3.zero, size);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundStrips.Add(go);
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

        /// <summary>
        /// Crossing checks against the crowd's leading plane. Called once per frame during RunnerLoop.
        ///
        /// Resolution and despawn are deliberately separate events. They used to be the same
        /// one, so a gate in another lane correctly did not score but also POPPED out of
        /// existence level with the player instead of sliding past them. A gate now scores
        /// (or does not) at the crowd's plane and keeps drawing until it is behind the camera.
        /// </summary>
        public void Tick(CrowdController crowd)
        {
            // Resolve where the player can SEE the crowd touching things, not at an
            // arbitrary offset from the centroid.
            float frontZ = crowd.FrontZ;
            int crowdLane = CrowdMath.LaneIndex(crowd.CenterX, _laneWidth);
            float despawnZ = TrackVisibility.DespawnPlane(
                crowd.CenterZ, CameraRig.SetbackMeters, DespawnMarginMeters);

            for (int i = _activeGates.Count - 1; i >= 0; i--)
            {
                GateBehaviour gate = _activeGates[i];
                float z = gate.transform.position.z;

                // Resolved is a latch, and it has to be: FrontZ is derived from the crowd's
                // envelope, which SHRINKS when force drops, so a big subtract gate can pull
                // the leading plane backwards up to 1.8 m in one frame against an anchor that
                // advances 0.167 m. Without the latch a spent gate slides back in front of the
                // plane and its label pops on again.
                if (!gate.Resolved && z > frontZ)
                {
                    // Every gate in the level exists from BuildLevel onward and its label
                    // draws through all geometry, so without this the far ones stack into
                    // an unreadable pile on the horizon.
                    gate.SetLabelVisible(z - frontZ <= LabelVisibleMeters);
                    continue;
                }

                if (!gate.Resolved && z <= frontZ)
                {
                    gate.Resolve();
                    // The label reads through everything, so a passed gate would otherwise
                    // paint its number over the crowd on the way by.
                    gate.SetLabelVisible(false);
                    if (gate.Lane == crowdLane)
                    {
                        gate.Consume();
                        GateApplied?.Invoke(gate.Op, gate.Value);
                    }
                }

                if (z < despawnZ)
                {
                    _activeGates.RemoveAt(i);
                    _gatePool.Release(gate);
                }
            }

            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                float z = pack.transform.position.z;

                if (!pack.Resolved && z > frontZ)
                {
                    pack.SetLabelVisible(z - frontZ <= LabelVisibleMeters);
                    continue;
                }

                if (!pack.Resolved && z <= frontZ)
                {
                    pack.Resolve();
                    pack.SetLabelVisible(false);
                    if (pack.Lane == crowdLane)
                    {
                        pack.Defeat();
                        EnemyContact?.Invoke(pack.ForceCost);
                    }
                }

                // A pack the crowd fought is dead and goes immediately; one in another lane
                // was dodged and slides past like a gate does. Lingering only makes sense for
                // the things the player went around.
                if (pack.Defeated || z < despawnZ)
                {
                    _activeEnemies.RemoveAt(i);
                    _enemyPool.Release(pack);
                }
            }

            if (!_finishRaised && frontZ >= _finishZ)
            {
                _finishRaised = true;
                FinishReached?.Invoke();
            }
        }

        /// <summary>
        /// Metres from <paramref name="fromZ"/> to the nearest enemy pack still ahead, for the
        /// tutorial to time its prompt. Negative when nothing is ahead.
        /// </summary>
        public float DistanceToNextEnemy(float fromZ)
        {
            float best = -1f;
            foreach (EnemyPackBehaviour pack in _activeEnemies)
            {
                // Spent packs linger until they are behind the camera, and a retreating
                // FrontZ can briefly put one "ahead" again — which would mis-time a prompt.
                if (pack.Resolved) continue;
                float ahead = pack.transform.position.z - fromZ;
                if (ahead <= 0f) continue;
                if (best < 0f || ahead < best) best = ahead;
            }
            return best;
        }

        /// <summary>Metres to the nearest unconsumed gate still ahead; negative when none.</summary>
        public float DistanceToNextGate(float fromZ)
        {
            float best = -1f;
            foreach (GateBehaviour gate in _activeGates)
            {
                if (gate.Resolved) continue;
                float ahead = gate.transform.position.z - fromZ;
                if (ahead <= 0f) continue;
                if (best < 0f || ahead < best) best = ahead;
            }
            return best;
        }

        /// <summary>Spell effect in the runner phase: destroys enemy packs within range ahead.</summary>
        public int ClearEnemiesAhead(float fromZ, float rangeMeters)
        {
            int cleared = 0;
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyPackBehaviour pack = _activeEnemies[i];
                // A pack the crowd already passed lingers in the list until it is behind the
                // camera; a spell must not reach back and pop it.
                if (pack.Resolved) continue;
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
