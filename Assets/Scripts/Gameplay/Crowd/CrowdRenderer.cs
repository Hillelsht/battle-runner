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
        private const int MaxInstances = 512;

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
            _instancingSupported = SystemInfo.supportsInstancing;
            if (!_instancingSupported)
                Debug.LogWarning("[Crowd] GPU instancing unsupported on this device; crowd rendering disabled (greybox fallback).");
            if (_material != null && !_material.enableInstancing)
                _material.enableInstancing = true;
        }

        private void LateUpdate()
        {
            if (!_instancingSupported || _crowd == null || _material == null || _mesh == null) return;

            int count = Mathf.Min(_crowd.VisibleUnits, MaxInstances);
            if (count <= 0) return;

            for (int i = 0; i < count; i++)
                _matrices[i] = Matrix4x4.TRS(_crowd.UnitPosition(i), Quaternion.identity, Vector3.one);

            var rp = new RenderParams(_material)
            {
                worldBounds = _crowd.ComputeBounds(),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMeshInstanced(rp, _mesh, 0, _matrices, count);
        }
    }
}
