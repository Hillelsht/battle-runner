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
        // One number governs the array length, the draw count and the tier clamp.
        private const int MaxInstances = CrowdController.MaxSimulated;

        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxInstances];
        private CrowdController _crowd;
        private Mesh _mesh;
        private Material _material;
        private bool _instancingSupported;

        public void Initialize(CrowdController crowd, Mesh unitMesh, Material crowdMaterial)
        {
            _crowd = crowd;
            _mesh = unitMesh;
            _material = crowdMaterial;
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

            // Per-instance yaw and scale: identical boxes in a regular lattice read as a
            // texture, not a crowd. The phase was already computed and never used.
            for (int i = 0; i < count; i++)
            {
                // Scale carries the bob phase into the shader (see CrowdInstanced.shader):
                // keep 0.94 + phase*0.12 in step with the decode there.
                float phase = _crowd.UnitPhase(i);
                float scale = 0.94f + phase * 0.12f;
                _matrices[i] = Matrix4x4.TRS(
                    _crowd.UnitPosition(i),
                    Quaternion.Euler(0f, (phase - 0.5f) * 24f, 0f),
                    new Vector3(scale, scale, scale));
            }

            var rp = new RenderParams(_material)
            {
                worldBounds = _crowd.ComputeBounds(),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };

            if (_instancingSupported)
            {
                Graphics.RenderMeshInstanced(rp, _mesh, 0, _matrices, count);
                return;
            }

            // A few hundred individual draws is affordable at greybox scale, and an
            // ugly-but-visible crowd beats an invisible one.
            for (int i = 0; i < count; i++)
                Graphics.RenderMesh(rp, _mesh, 0, _matrices[i]);
        }
    }
}
