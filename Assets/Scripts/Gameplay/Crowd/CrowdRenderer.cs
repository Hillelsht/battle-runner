using UnityEngine;

namespace BattleRunner.Gameplay.Crowd
{
    /// <summary>
    /// Draws the whole crowd with Graphics.RenderMeshInstanced — one draw call, zero
    /// per-unit GameObjects. The material ships in Resources with instancing enabled
    /// so the Android build never strips the shader or its instancing variants (doc 04).
    /// </summary>
    public sealed class CrowdRenderer : MonoBehaviour
    {
        /// <summary>
        /// Which soldier a slot is. A cheap integer hash rather than i % 4, so the four
        /// kinds scatter through the phyllotaxis spiral instead of banding into four
        /// interleaved arms of it.
        ///
        /// Banners are deliberately rare — one in eight, not one in four. A banner over
        /// every fourth man is a parade; a banner here and there over a mass of spears is
        /// an army.
        /// </summary>
        private static int Archetype(int slot)
        {
            uint h = (uint)slot * 2654435761u;
            h ^= h >> 15;
            int bucket = (int)(h % 8u);
            switch (bucket)
            {
                case 0: return 3;               // banner, 1 in 8
                case 1:
                case 2: return 1;               // shield, 2 in 8
                case 3:
                case 4: return 2;               // axe, 2 in 8
                default: return 0;              // spear, 3 in 8
            }
        }

        // One number governs the array length, the draw count and the tier clamp.
        private const int MaxInstances = CrowdController.MaxSimulated;

        // The formation envelope is pinned by the ROAD — halfWidthMax is 0.355 of a
        // 2.2 m lane, so the crowd is never wider than 1.56 m — and depth cannot separate
        // bodies at the rig's 11 degree pitch. The only free variable is how big each body
        // is drawn. At the old 0.94-1.06 a unit was 0.60 m across the pauldrons against a
        // lateral neighbour pitch of 0.157 m at n=90: bodies drawn nearly four widths into
        // each other, a solid slab of overlapping boxes. At 0.44-0.50 ranks still overlap,
        // which is what an army looks like, but individual figures resolve.
        //
        // CrowdInstanced.shader recovers the bob phase from this scale — CROWD_SCALE_MIN
        // and CROWD_SCALE_SPAN there must match these two exactly.
        //
        // PUBLIC because SquadRenderer draws bodies through the same shader and has to encode
        // a phase into the same window. It drew every one of them at a flat 0.47 — dead centre
        // of the window — so the shader decoded phase 0.5 for the whole line and an enemy
        // squad marched as one synchronised band with every leg at the same point of the same
        // stride. tooling/lint_unity_yaml.py:247 still matches these declarations.
        public const float ScaleMin = 0.44f;
        public const float ScaleSpan = 0.06f;

        // One bucket per soldier archetype. Four instanced draws instead of one is still
        // nothing on any GPU, and it is the difference between an army and a photocopy.
        // Each bucket is sized for the worst case where every unit lands in it — 4 x 512
        // matrices is 128 KB, allocated once and never touched again.
        private static readonly ProceduralMeshes.SoldierKind[] Kinds =
        {
            ProceduralMeshes.SoldierKind.Spear,
            ProceduralMeshes.SoldierKind.Shield,
            ProceduralMeshes.SoldierKind.Axe,
            ProceduralMeshes.SoldierKind.Banner
        };

        private readonly Matrix4x4[][] _buckets = new Matrix4x4[Kinds.Length][];
        private readonly int[] _bucketCounts = new int[Kinds.Length];
        private readonly Mesh[] _meshes = new Mesh[Kinds.Length];

        private CrowdController _crowd;
        private Mesh _mesh;
        private Material _material;
        private bool _instancingSupported;

        public void Initialize(CrowdController crowd, Mesh unitMesh, Material crowdMaterial)
        {
            _crowd = crowd;
            _mesh = unitMesh;
            _material = crowdMaterial;

            for (int k = 0; k < Kinds.Length; k++)
            {
                _buckets[k] = new Matrix4x4[MaxInstances];
                _meshes[k] = ProceduralMeshes.Soldier(Kinds[k]);
            }
            if (_material != null && !_material.enableInstancing)
                _material.enableInstancing = true;

            // SystemInfo.supportsInstancing alone was not enough: if the MATERIAL's shader
            // has no instancing variants (stripped, or no SRP active), RenderMeshInstanced
            // silently draws nothing and the crowd vanishes entirely.
            _instancingSupported = SystemInfo.supportsInstancing
                                   && _material != null
                                   && _material.enableInstancing
                                   && _material.shader != null
                                   && _material.shader.isSupported;

            if (!_instancingSupported)
                Debug.LogError("[Crowd] GPU instancing unavailable (shader '" +
                               (_material != null && _material.shader != null ? _material.shader.name : "null") +
                               "'); falling back to individual draws.");
        }

        private void LateUpdate()
        {
            if (_crowd == null || _material == null || _mesh == null) return;

            int count = Mathf.Min(_crowd.VisibleUnits, MaxInstances);
            if (count <= 0) return;

            for (int k = 0; k < _bucketCounts.Length; k++) _bucketCounts[k] = 0;

            // Per-instance yaw and scale: identical boxes in a regular lattice read as a
            // texture, not a crowd. The phase was already computed and never used.
            for (int i = 0; i < count; i++)
            {
                // Scale carries the bob phase into the shader (see CrowdInstanced.shader):
                // keep ScaleMin + phase*ScaleSpan in step with the decode there.
                float phase = _crowd.UnitPhase(i);
                float scale = ScaleMin + phase * ScaleSpan;
                var trs = Matrix4x4.TRS(
                    _crowd.UnitPosition(i),
                    Quaternion.Euler(0f, (phase - 0.5f) * 24f, 0f),
                    new Vector3(scale, scale, scale));

                // Archetype from the SLOT INDEX, not from position or from a random draw.
                // Slots are stable for the life of a run — a unit keeps its identity as the
                // army grows around it, instead of the whole crowd reshuffling its weapons
                // every time a gate is passed.
                int kind = Archetype(i);
                _buckets[kind][_bucketCounts[kind]++] = trs;
            }

            var rp = new RenderParams(_material)
            {
                worldBounds = _crowd.ComputeBounds(),
                // The crowd is ONE instanced draw, so casting costs one more instanced draw
                // into the shadow map — cheap, and the difference between an army marching
                // on the road and an army hovering over it.
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true
            };

            for (int k = 0; k < Kinds.Length; k++)
            {
                int n = _bucketCounts[k];
                if (n <= 0) continue;
                Mesh mesh = _meshes[k] != null ? _meshes[k] : _mesh;

                if (_instancingSupported)
                {
                    Graphics.RenderMeshInstanced(rp, mesh, 0, _buckets[k], n);
                    continue;
                }

                // A few hundred individual draws is affordable at greybox scale, and an
                // ugly-but-visible crowd beats an invisible one.
                for (int i = 0; i < n; i++) Graphics.RenderMesh(rp, mesh, 0, _buckets[k][i]);
            }
        }
    }
}
